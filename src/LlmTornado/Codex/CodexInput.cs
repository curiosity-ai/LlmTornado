using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Codex;

/// <summary>
/// One text or image part of a Codex user message, shared by both connection types.
/// </summary>
public sealed class CodexInput
{
    private enum InputKind { Text, Image, LocalImage }

    private readonly InputKind kind;
    private readonly string value;

    private CodexInput(InputKind kind, string value)
    {
        this.kind = kind;
        this.value = value;
    }

    /// <summary>
    /// Creates a nonempty text part.
    /// </summary>
    public static CodexInput Text(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Codex text cannot be empty.", nameof(text));
        }

        return new CodexInput(InputKind.Text, text);
    }

    /// <summary>
    /// Creates an image part from an absolute HTTP(S) URL or a Base64 image data URL.
    /// Remote URL support depends on the connected backend and Codex version.
    /// </summary>
    public static CodexInput Image(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("Codex image URL cannot be empty.", nameof(imageUrl));
        }

        // Data URLs can exceed the URI length limit on older target frameworks.
        if (imageUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            int marker = imageUrl.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
            if (marker <= "data:image/".Length || marker + ";base64,".Length == imageUrl.Length)
            {
                throw new ArgumentException("Use a Base64 image data URL.", nameof(imageUrl));
            }
        }
        else if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? uri)
                 || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Use an absolute HTTP(S) URL or an image data URL.", nameof(imageUrl));
        }

        return new CodexInput(InputKind.Image, imageUrl);
    }

    /// <summary>
    /// Creates a local image part. Relative paths are resolved when this method is called.
    /// The app-server reads the path on its host. Direct OAuth reads PNG, JPEG, WebP, or GIF
    /// files when the message is prepared and retains their data in the thread history.
    /// </summary>
    public static CodexInput LocalImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Codex image path cannot be empty.", nameof(path));
        }

        return new CodexInput(InputKind.LocalImage, Path.GetFullPath(path));
    }

    internal static JArray ToAppServerInput(IReadOnlyList<CodexInput> input)
        => new JArray(Snapshot(input).Select(item => item.ToAppServerItem()));

    internal static async Task<JObject> CreateUserMessageAsync(
        CodexInput[] input,
        CancellationToken cancellationToken)
    {
        JArray content = new JArray();
        foreach (CodexInput item in input)
        {
            content.Add(await item.ToResponseContentAsync(cancellationToken).ConfigureAwait(false));
        }

        return new JObject
        {
            ["type"] = "message",
            ["role"] = "user",
            ["content"] = content
        };
    }

    internal static CodexInput[] Snapshot(IReadOnlyList<CodexInput> input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (input.Count == 0)
        {
            throw new ArgumentException("Codex input cannot be empty.", nameof(input));
        }

        CodexInput[] items = input.ToArray();
        if (items.Any(item => item is null))
        {
            throw new ArgumentException("Codex input cannot contain null parts.", nameof(input));
        }

        return items;
    }

    private JObject ToAppServerItem()
        => kind switch
        {
            InputKind.Text => new JObject { ["type"] = "text", ["text"] = value },
            InputKind.Image => new JObject { ["type"] = "image", ["url"] = value },
            _ => new JObject { ["type"] = "localImage", ["path"] = value }
        };

    private async Task<JObject> ToResponseContentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (kind == InputKind.Text)
        {
            return new JObject { ["type"] = "input_text", ["text"] = value };
        }

        string imageUrl = value;
        if (kind == InputKind.LocalImage)
        {
            string mediaType = GetImageMediaType();
            using FileStream stream = new FileStream(
                value, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            imageUrl = await ReadImageDataUrlAsync(stream, mediaType, cancellationToken).ConfigureAwait(false);
        }

        return new JObject { ["type"] = "input_image", ["image_url"] = imageUrl, ["detail"] = "auto" };
    }

    private string GetImageMediaType()
        => Path.GetExtension(value).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => throw new ArgumentException("Direct Codex OAuth local images must be PNG, JPEG, WebP, or GIF.")
        };

    // The caller owns the input stream. The buffer is private until the complete read succeeds.
    internal static async Task<string> ReadImageDataUrlAsync(
        Stream stream,
        string mediaType,
        CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return $"data:{mediaType};base64,{Convert.ToBase64String(buffer.GetBuffer(), 0, checked((int)buffer.Length))}";
    }
}
