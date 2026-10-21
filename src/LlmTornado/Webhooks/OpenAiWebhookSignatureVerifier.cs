using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LlmTornado.Webhooks;

/// <summary>Verifies OpenAI project webhook signatures and their replay window before parsing JSON.</summary>
public static class OpenAiWebhookSignatureVerifier
{
    public static OpenAiWebhookEvent VerifyAndParse(string payload, IReadOnlyDictionary<string, string> headers,
        string signingSecret, TimeSpan? timestampTolerance = null) =>
        VerifyAndParse(Encoding.UTF8.GetBytes(payload), headers, signingSecret, timestampTolerance);

    /// <summary>Pass the original request body bytes, including all whitespace.</summary>
    public static OpenAiWebhookEvent VerifyAndParse(byte[] payload, IReadOnlyDictionary<string, string> headers,
        string signingSecret, TimeSpan? timestampTolerance = null)
    {
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        if (string.IsNullOrEmpty(signingSecret)) throw new OpenAiWebhookVerificationException("A signing secret is required.");
        string Header(string name) => headers.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
        string id = Header("webhook-id"), timestamp = Header("webhook-timestamp"), signatures = Header("webhook-signature");
        if (id.Length == 0 || timestamp.Length == 0 || signatures.Length == 0)
            throw new OpenAiWebhookVerificationException("Missing required webhook signature headers.");
        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds))
            throw new OpenAiWebhookVerificationException("Invalid webhook timestamp.");
        TimeSpan tolerance = timestampTolerance ?? TimeSpan.FromMinutes(5);
        if (tolerance < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timestampTolerance));
        if (Math.Abs((double)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) > tolerance.TotalSeconds)
            throw new OpenAiWebhookVerificationException("Webhook timestamp is outside the allowed window.");

        byte[] key;
        try
        {
            key = signingSecret.StartsWith("whsec_", StringComparison.Ordinal)
                ? Convert.FromBase64String(signingSecret.Substring(6)) : Encoding.UTF8.GetBytes(signingSecret);
        }
        catch (FormatException ex) { throw new OpenAiWebhookVerificationException("Invalid webhook signing secret.", ex); }
        if (key.Length == 0) throw new OpenAiWebhookVerificationException("The signing secret must not be empty.");
        byte[] prefix = Encoding.UTF8.GetBytes(id + "." + timestamp + ".");
        byte[] content = new byte[prefix.Length + payload.Length];
        Buffer.BlockCopy(prefix, 0, content, 0, prefix.Length);
        Buffer.BlockCopy(payload, 0, content, prefix.Length, payload.Length);
        using HMACSHA256 hmac = new(key);
        string expected = Convert.ToBase64String(hmac.ComputeHash(content));
        bool valid = false;
        foreach (string part in signatures.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = part.StartsWith("v1,", StringComparison.Ordinal) ? part.Substring(3) : part;
            if (candidate.Length != expected.Length) continue;
            int difference = 0;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ candidate[i];
            valid |= difference == 0;
        }
        if (!valid) throw new OpenAiWebhookVerificationException("Webhook signature is invalid.");
        try
        {
            return OpenAiWebhookEvent.ParseUnverified(new UTF8Encoding(false, true).GetString(payload))
                ?? throw new OpenAiWebhookVerificationException("Webhook payload is null.");
        }
        catch (Newtonsoft.Json.JsonException ex) { throw new OpenAiWebhookVerificationException("Webhook payload is invalid JSON.", ex); }
        catch (DecoderFallbackException ex) { throw new OpenAiWebhookVerificationException("Webhook payload is invalid UTF-8.", ex); }
    }
}

public class OpenAiWebhookVerificationException : Exception
{
    public OpenAiWebhookVerificationException(string message) : base(message) { }
    public OpenAiWebhookVerificationException(string message, Exception innerException) : base(message, innerException) { }
}
