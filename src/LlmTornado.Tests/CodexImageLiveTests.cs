using System.Globalization;
using LlmTornado.Codex;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class CodexImageLiveTests
{
    private const string Prompt =
        "This is a visual input test. Look only at the attached image. Do not use tools or read files. " +
        "Name the main object, its main color, and the contrasting band color. Use English. " +
        "Reply with exactly one JSON object with keys object, main_color, band_color, without Markdown.";

    [TestCase(false)]
    [TestCase(true)]
    public async Task AppServer_RecognizesAttachedImage(bool useDataUrl)
    {
        RequireLiveTests();
        using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        CodexAppServerOptions options = new CodexAppServerOptions
        {
            ExecutablePath = Environment.GetEnvironmentVariable("LLMTORNADO_CODEX_EXECUTABLE") ?? "codex",
            WorkingDirectory = TestContext.CurrentContext.TestDirectory
        };
        string disabledServers = Environment.GetEnvironmentVariable("LLMTORNADO_CODEX_IMAGE_DISABLE_MCP_SERVERS") ?? string.Empty;
        foreach (string server in disabledServers.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            options.ConfigOverrides.Add($"mcp_servers.{server}.enabled=false");
        }

        await using CodexSession session = await new TornadoApi().Codex.ConnectAsync(options, cancellation.Token);
        CodexAccountResult account = await session.GetAccountAsync(cancellationToken: cancellation.Token);
        Assert.That(account.Account?.Type, Is.EqualTo("chatgpt"), "Sign in to the installed Codex application before this test.");
        CodexModel model = SelectImageModel(await session.ListModelsAsync(cancellationToken: cancellation.Token));
        CodexThread thread = await session.StartThreadAsync(new CodexThreadOptions
        {
            Model = model.Model,
            WorkingDirectory = TestContext.CurrentContext.TestDirectory,
            ApprovalPolicy = "never",
            Sandbox = "read-only",
            Ephemeral = true
        }, cancellation.Token);
        List<string> toolItems = [];
        session.NotificationReceived += notification =>
        {
            string? type = notification.Parameters["item"]?.Value<string>("type");
            if (notification.Method == "item/started" && type is "commandExecution" or "mcpToolCall" or "imageView")
            {
                toolItems.Add(type);
            }
        };

        CodexTurnResult result = await thread.RunAsync(CreateInput(useDataUrl), cancellationToken: cancellation.Token);

        TestContext.Out.WriteLine($"App-server; model={model.Model}; dataUrl={useDataUrl}; status={result.Status}; response={result.FinalResponse}");
        Assert.That(result.Status, Is.EqualTo("completed"));
        Assert.That(toolItems, Is.Empty, "The model must answer from the supplied image without reading files or invoking tools.");
        AssertImageContent(result.FinalResponse);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DirectOAuth_RecognizesAttachedImage(bool useDataUrl)
    {
        RequireLiveTests();
        string? accessToken = Environment.GetEnvironmentVariable("LLMTORNADO_CODEX_OAUTH_ACCESS_TOKEN");
        string? accountId = Environment.GetEnvironmentVariable("LLMTORNADO_CODEX_OAUTH_ACCOUNT_ID");
        string? expiresAt = Environment.GetEnvironmentVariable("LLMTORNADO_CODEX_OAUTH_EXPIRES_AT");
        Assert.That(!string.IsNullOrWhiteSpace(accessToken), Is.True, "Set LLMTORNADO_CODEX_OAUTH_ACCESS_TOKEN to a current ChatGPT access token.");
        Assert.That(!string.IsNullOrWhiteSpace(accountId), Is.True, "Set LLMTORNADO_CODEX_OAUTH_ACCOUNT_ID.");
        Assert.That(DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset expiration), Is.True,
            "Set LLMTORNADO_CODEX_OAUTH_EXPIRES_AT to the access token expiration in ISO 8601 format.");
        Assert.That(expiration, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(5)), "The access token expires too soon. Sign in again before this test.");
        using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        // Deliberately omit the refresh token: this test must not rotate another application's credentials.
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(new CodexOAuthCredentials
        {
            AccessToken = accessToken!,
            AccountId = accountId,
            ExpiresAtUtc = expiration,
            LastRefreshUtc = DateTimeOffset.UtcNow
        });
        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions { CredentialStore = store }, cancellation.Token);
        CodexModel model = SelectImageModel(await session.ListModelsAsync(cancellationToken: cancellation.Token));
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions { Model = model.Model }, cancellation.Token);

        CodexOAuthTurnResult result = await thread.RunAsync(CreateInput(useDataUrl), cancellationToken: cancellation.Token);

        TestContext.Out.WriteLine($"Direct OAuth; model={model.Model}; dataUrl={useDataUrl}; status={result.Status}; response={result.FinalResponse}");
        Assert.That(result.Status, Is.EqualTo("completed"));
        Assert.That(result.ToolCalls, Is.Empty);
        AssertImageContent(result.FinalResponse);
    }

    private static void RequireLiveTests()
    {
        if (Environment.GetEnvironmentVariable("LLMTORNADO_CODEX_IMAGE_LIVE_TEST") != "1")
        {
            Assert.Ignore("Set LLMTORNADO_CODEX_IMAGE_LIVE_TEST=1 to send real text/image requests through Codex subscription access.");
        }
    }

    private static CodexModel SelectImageModel(IReadOnlyList<CodexModel> models)
    {
        string? requested = Environment.GetEnvironmentVariable("LLMTORNADO_CODEX_IMAGE_MODEL");
        List<CodexModel> imageModels = models.Where(model => model.InputModalities.Contains("image")).ToList();
        CodexModel? selected = string.IsNullOrWhiteSpace(requested)
            ? imageModels.FirstOrDefault(model => model.IsDefault) ?? imageModels.FirstOrDefault()
            : imageModels.FirstOrDefault(model => model.Model == requested);
        Assert.That(selected, Is.Not.Null, "The model catalog must provide a model with image input support.");
        return selected!;
    }

    private static CodexInput[] CreateInput(bool useDataUrl)
    {
        string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Static", "Images", "hat.jpg");
        CodexInput image = useDataUrl
            ? CodexInput.Image($"data:image/jpeg;base64,{Convert.ToBase64String(File.ReadAllBytes(path))}")
            : CodexInput.LocalImage(path);
        return [CodexInput.Text(Prompt), image];
    }

    private static void AssertImageContent(string response)
    {
        JObject answer = JObject.Parse(response);
        Assert.Multiple(() =>
        {
            Assert.That(answer.Value<string>("object")?.ToLowerInvariant(), Does.Contain("hat"));
            Assert.That(answer.Value<string>("main_color")?.ToLowerInvariant(), Does.Contain("black"));
            Assert.That(answer.Value<string>("band_color")?.ToLowerInvariant(), Does.Contain("purple").Or.Contain("violet"));
        });
    }
}
