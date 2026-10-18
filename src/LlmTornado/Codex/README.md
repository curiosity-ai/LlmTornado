# OpenAI Codex with a ChatGPT subscription

LLMTornado supports Codex access through a ChatGPT Plus, Pro, Business, Edu, or Enterprise subscription.

This is separate from the normal OpenAI API:

- OpenAI API access uses an API key and supports the normal text and image endpoints.
- Codex subscription access uses ChatGPT login, a separate model catalog, and Codex threads with text and image input.
- `ChatModel.OpenAi.Codex` contains static models for the OpenAI API. Do not use it as the subscription model catalog.
- Always call `ListModelsAsync()` to get the models available to the signed-in ChatGPT account.

## Choose a connection

LLMTornado provides two independent ways to connect:

| Connection | Codex installation | Token owner | Use it when |
| --- | --- | --- | --- |
| Codex app-server | Required | Codex | The official Codex application should manage login, tokens, refresh, and agent execution. |
| Direct browser OAuth | Not required | Your application through LLMTornado | The application should provide browser login and store the rotating refresh token itself. |

Both connections use the same `TornadoApi.Codex` entry point. They use different session, thread, turn, and login types.

## Codex app-server

The `codex` executable must be available on `PATH`. You can also set an explicit path through `CodexAppServerOptions.ExecutablePath`.

```csharp
using System.Diagnostics;
using LlmTornado;
using LlmTornado.Codex;

TornadoApi api = new TornadoApi();

await using CodexSession codex = await api.Codex.ConnectAsync();

CodexAccountResult account = await codex.GetAccountAsync();
if (account.Account is null)
{
    CodexBrowserLogin login = await codex.StartBrowserLoginAsync();
    Process.Start(new ProcessStartInfo(login.AuthorizationUrl.AbsoluteUri)
    {
        UseShellExecute = true
    });

    CodexLoginResult result = await login.WaitAsync();
    if (!result.Success)
    {
        throw new InvalidOperationException(result.Error);
    }
}

IReadOnlyList<CodexModel> models = await codex.ListModelsAsync();
CodexModel model = models.First(x => x.IsDefault);

CodexThread thread = await codex.StartThreadAsync(new CodexThreadOptions
{
    Model = model.Model
});

CodexTurnResult turn = await thread.RunAsync(
    "Explain this project in three sentences.");

Console.WriteLine(turn.FinalResponse);
```

The app-server owns token storage and refresh. LLMTornado does not read or store the ChatGPT refresh token in this mode.

Call `codex.LogoutAsync()` to sign out through the app-server.

## Direct browser OAuth

Direct OAuth does not require a Codex installation. LLMTornado starts a localhost callback listener and returns the official `auth.openai.com` authorization URL.

```csharp
using System.Diagnostics;
using LlmTornado;
using LlmTornado.Codex;

TornadoApi api = new TornadoApi();

await using CodexOAuthSession codex = await api.Codex.ConnectOAuthAsync();

CodexAccountResult account = await codex.GetAccountAsync();
if (account.Account is null)
{
    CodexOAuthBrowserLogin login = await codex.StartBrowserLoginAsync();
    Process.Start(new ProcessStartInfo(login.AuthorizationUrl.AbsoluteUri)
    {
        UseShellExecute = true
    });

    CodexOAuthLoginResult result = await login.WaitAsync();
    if (!result.Success)
    {
        throw new InvalidOperationException(result.Error);
    }
}

IReadOnlyList<CodexModel> models = await codex.ListModelsAsync();
CodexModel model = models.First(x => x.IsDefault);

CodexOAuthThread thread = await codex.StartThreadAsync(new CodexOAuthThreadOptions
{
    Model = model.Model
});

CodexOAuthTurnResult turn = await thread.RunAsync(
    "Explain this project in three sentences.");

Console.WriteLine(turn.FinalResponse);
```

### Direct OAuth function tools

Direct OAuth threads accept the same function `Tool`, `FunctionCall`, and `FunctionResult` types as normal chat conversations:

```csharp
using LlmTornado.ChatFunctions;
using LlmTornado.Common;

Tool weather = new Tool(
    (string location) => GetWeather(location),
    "get_weather",
    "Gets the current weather for a location.");

CodexOAuthTurnResult turn = await thread.RunAsync(
    "What is the weather in Prague?",
    async (calls, cancellationToken) =>
    {
        foreach (FunctionCall call in calls)
        {
            // Validate the requested operation before invoking host code.
            if (call.Tool != weather)
            {
                call.Result = new FunctionResult(call, new { error = "Tool denied." }, false);
                continue;
            }

            await call.Invoke(call.Arguments ?? "{}");
        }
    },
    new CodexOAuthTurnOptions
    {
        Tools = [weather],
        ToolChoice = OutboundToolChoice.Auto,
        ParallelToolCalls = false
    });

Console.WriteLine(turn.FinalResponse);
```

The handler runs before any attached delegate is invoked. LLMTornado supplies the function-calling protocol but does not authorize, sandbox, or grant filesystem access. Applications must validate each call and may return a failed `FunctionResult` for denied or unknown operations.

The default callback port is `1455`. Port `1457` is used as a fallback. Set `CallbackPort` and `FallbackCallbackPort` in `CodexOAuthOptions` only when the OAuth redirect configuration supports those ports.

Call `login.CancelAsync()` when the user cancels the browser flow. Call `codex.LogoutAsync()` to revoke the current refresh token when possible and clear the credential store.

## Text and image input

Both `CodexThread` and `CodexOAuthThread` accept an ordered list of `CodexInput` parts.
The existing `RunAsync(string, ...)` overloads still accept text requests.

```csharp
CodexInput[] input =
[
    CodexInput.Text("Describe this screenshot and suggest layout changes."),
    CodexInput.LocalImage("screenshots/settings.png")
];

// thread can be a CodexThread or a CodexOAuthThread.
var turn = await thread.RunAsync(input);
Console.WriteLine(turn.FinalResponse);
```

The input list can contain multiple images, interleaved text parts, or images without text.
LLMTornado rejects empty lists, null parts, and empty text parts before it sends a turn.

`CodexInput.Image(...)` accepts an absolute HTTP(S) URL or a Base64 image data URL:

```csharp
CodexInput[] input =
[
    CodexInput.Text("Compare these images."),
    CodexInput.Image("https://example.com/design.png"),
    CodexInput.Image($"data:image/png;base64,{Convert.ToBase64String(pngBytes)}")
];

var turn = await thread.RunAsync(input);
```

Remote URL support depends on the backend and installed Codex version.
If the backend rejects remote URLs, use a local image or a Base64 data URL.
Select a model with image input support from `ListModelsAsync()`.
`CodexModel.InputModalities` contains the advertised input types.

The two connections send images differently:

| Connection | Image handling |
| --- | --- |
| App-server | Sends `text`, `image`, and `localImage` entries in `turn/start.input`. The app-server reads local files on its host. |
| Direct OAuth | Sends `input_text` and `input_image` entries in a user message. Local files become Base64 image data URLs. |

`LocalImage` resolves relative paths when you create the input part.
For direct OAuth, local files must have a `.png`, `.jpg`, `.jpeg`, `.webp`, or `.gif` extension.
Direct OAuth reads local files asynchronously and closes each file before it sends the request.
Pass a cancellation token to `RunAsync` to cancel an active image read or the subsequent request.
The direct OAuth history retains the image data for later turns and tool-call continuations.
These requests do not reopen the original image file.
If an image read fails or is canceled, LLMTornado sends no turn request and leaves the history unchanged.

The direct OAuth function-call overloads also accept `IReadOnlyList<CodexInput>`.
To restore a user message with images, await `CodexOAuthHistoryItem.UserMessageAsync(input, cancellationToken)` before you set `InitialHistory`.
This factory reads local files asynchronously and copies their data into the history item.
`CodexOAuthHistoryItem.UserMessage(string)` remains available for text-only history items.
Use `CodexInput.Image(savedDataUrl)` to restore image data without the original file.

```csharp
CodexOAuthHistoryItem priorMessage = await CodexOAuthHistoryItem.UserMessageAsync(
    [CodexInput.Text("Describe this image."), CodexInput.LocalImage("screenshots/settings.png")],
    cancellationToken);

CodexOAuthThread restoredThread = await codex.StartThreadAsync(
    new CodexOAuthThreadOptions { Model = model.Model, InitialHistory = [priorMessage] },
    cancellationToken);
```

See the official [Codex input protocol](https://learn.chatgpt.com/docs/app-server#turns)
and [Responses image input format](https://developers.openai.com/api/docs/guides/images-vision#giving-a-model-images-as-input).
The public Responses guide describes the input format. Direct OAuth uses the ChatGPT Codex backend.

### Live image tests

`CodexImageLiveTests` sends real subscription requests through both connections.
Each connection receives text with a local JPEG and text with a Base64 data URL in separate tests.
The tests compare the model response with the image content.
The prompt does not contain the expected object or colors.

The app-server tests require an installed Codex executable and an existing ChatGPT sign-in.
The direct OAuth tests use these process environment variables:

| Variable | Value |
| --- | --- |
| `LLMTORNADO_CODEX_OAUTH_ACCESS_TOKEN` | A current ChatGPT OAuth access token. |
| `LLMTORNADO_CODEX_OAUTH_ACCOUNT_ID` | The account ID for that token. |
| `LLMTORNADO_CODEX_OAUTH_EXPIRES_AT` | The token expiration in ISO 8601 format. At least five minutes must remain. |

The direct tests keep the token in memory. They do not use or rotate a refresh token.
They do not start a browser login or write credentials to disk.

Optional variables:

| Variable | Value |
| --- | --- |
| `LLMTORNADO_CODEX_EXECUTABLE` | The path to the Codex executable. The default is `codex`. |
| `LLMTORNADO_CODEX_IMAGE_MODEL` | An image-capable model ID from the account catalog. Otherwise, the tests select an available image-capable model. |
| `LLMTORNADO_CODEX_IMAGE_DISABLE_MCP_SERVERS` | Semicolon-separated MCP server names to disable in the test app-server process. The saved Codex configuration stays unchanged. |

After you provide the access token variables, enable and run the tests from the repository root:

```powershell
$env:LLMTORNADO_CODEX_IMAGE_LIVE_TEST = '1'
dotnet test src/LlmTornado.Tests/LlmTornado.Tests.csproj --configuration Release --filter FullyQualifiedName~LlmTornado.Tests.CodexImageLiveTests -p:GeneratePackageOnBuild=false -p:SkipViteBuild=true
```

Without the opt-in variable, the live tests do not send requests.

## Credential storage

Direct OAuth access and refresh tokens are secrets.

The default `CodexOAuthFileCredentialStore` writes credentials to:

```text
%LOCALAPPDATA%\LlmTornado\codex-oauth.json
```

Applications with their own secure storage can implement `ICodexOAuthCredentialStore`:

```csharp
await using CodexOAuthSession codex = await api.Codex.ConnectOAuthAsync(
    new CodexOAuthOptions
    {
        CredentialStore = new MySecureCredentialStore()
    });
```

The credential store must replace the complete credential set after every login and refresh. OpenAI refresh tokens can rotate, so keeping only the old refresh token will break the next refresh.

Never log access tokens, ID tokens, refresh tokens, authorization codes, or complete credential objects.

## Models and service tiers

Subscription models are account-specific and can change over time:

```csharp
IReadOnlyList<CodexModel> models = await codex.ListModelsAsync();
CodexModel model = models.First(x => x.IsDefault);
```

Each model can advertise reasoning efforts, service tiers, and a default service tier. Keep service-tier IDs as returned by the catalog. Unknown future IDs are valid.

The `priority` tier is commonly shown as **Fast**:

```csharp
CodexServiceTier? fastTier =
    model.ServiceTiers.FirstOrDefault(x => x.Id == "priority");

CodexOAuthTurnResult turn = await thread.RunAsync(
    "Summarize the current changes.",
    new CodexOAuthTurnOptions
    {
        ReasoningEffort = "high",
        ServiceTier = fastTier?.Id
    });
```

Use `CodexTurnOptions` instead of `CodexOAuthTurnOptions` with an app-server thread. Omit `ServiceTier` to let the backend use the catalog default.

Ultrafast is available for GPT-6 Astra on Pro $500 and eligible Enterprise and Edu workspaces.
Access depends on the account and workspace permissions. See [Ultrafast availability](https://help.openai.com/en/articles/20001275-chatgpt-work-and-codex).

If the model catalog lists `ultrafast`, select that tier:

```csharp
CodexModel astra = models.First(x => x.Model == "gpt-6-astra");
CodexServiceTier ultrafast = astra.ServiceTiers.First(x => x.Id == "ultrafast");
CodexOAuthThread ultrafastThread = await codex.StartThreadAsync(
    new CodexOAuthThreadOptions { Model = astra.Model });

await ultrafastThread.RunAsync("Summarize the current changes.",
    new CodexOAuthTurnOptions { ServiceTier = ultrafast.Id });
```

For app-server sessions, use `CodexThreadOptions` and `CodexTurnOptions`.
API-key requests use separate API billing. For Responses requests, set `ServiceTier = ChatRequestServiceTiers.Ultrafast`.
See [API Ultrafast mode](https://developers.openai.com/api/docs/guides/ultrafast-mode).

## Streaming and multiple turns

Use `OnTextDelta` to update the UI while a response is generated:

```csharp
CodexOAuthTurnResult turn = await thread.RunAsync(
    "Explain the result.",
    new CodexOAuthTurnOptions
    {
        OnTextDelta = delta =>
        {
            Console.Write(delta.Delta);
            return Task.CompletedTask;
        }
    });
```

Reuse the same thread object for later turns:

```csharp
await thread.RunAsync("Give me a shorter version.");
```

The direct OAuth thread keeps the required response history on the client because subscription requests are not stored by the backend.

To restore a thread after restarting the application, provide the previously persisted items in protocol order:

```csharp
CodexOAuthThread restoredThread = await codex.StartThreadAsync(new CodexOAuthThreadOptions
{
    Model = model.Model,
    InitialHistory =
    [
        CodexOAuthHistoryItem.UserMessage("What time is it?"),
        CodexOAuthHistoryItem.FunctionCall("call-1", "datetime", "{}"),
        CodexOAuthHistoryItem.FunctionOutput("call-1", "2026-09-19T16:58:17Z"),
        CodexOAuthHistoryItem.AssistantMessage("It is 18:58 local time.")
    ]
});
```

Function outputs must follow their matching calls and use the same call identifier. Initial history is copied when the thread is created.

## Client and protocol versions

`CodexOAuthOptions.ClientVersion` identifies your application in request headers and the user agent. When it is omitted, LLMTornado uses its assembly version.

`CodexOAuthOptions.CodexProtocolVersion` is a separate value used as `client_version` during model discovery. Its default is `0.160.0`.

Override only the protocol version when the Codex backend requires a newer value:

```csharp
await using CodexOAuthSession codex = await api.Codex.ConnectOAuthAsync(
    new CodexOAuthOptions
    {
        ClientVersion = "1.0.0",
        CodexProtocolVersion = "0.160.0"
    });
```

Do not use your application or LLMTornado package version as the Codex protocol version.

## Lifetime and error handling

- Dispose `CodexSession` and `CodexOAuthSession`. `await using` is recommended.
- Keep a session alive while its threads are in use.
- Do not use a thread after its session has been disposed.
- Pass cancellation tokens to login, model, and turn operations.
- Treat `CodexOAuthException` as an OAuth or direct backend failure.
- Treat `CodexRpcException` as an app-server protocol failure.
- Show login errors from `CodexLoginResult.Error` or `CodexOAuthLoginResult.Error`.
- Retry authentication only after checking whether the stored credentials are still valid.

## Limits

- Image input requires a model that supports images. Backend limits and supported URL forms still apply.
- Codex subscription access does not enable LLMTornado image generation.
- Continue to use an OpenAI API key for normal OpenAI text, image, Realtime, and other API endpoints.
- App-server availability depends on the installed Codex version.
- Direct OAuth model availability depends on the signed-in ChatGPT account and current subscription.
