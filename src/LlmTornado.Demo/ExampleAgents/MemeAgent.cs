using LlmTornado;
using LlmTornado.Agents;
using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Mcp;
using System.Diagnostics;

namespace LlmTornado.Demo.ExampleAgents.MemeAgent;

public class MemeAgent
{
    public static async Task Run()
    {
        // Setup: TornadoApi configured with provider(s)
        TornadoApi api = Program.Connect();

        // User input
        Console.Write("Meme theme: ");
        string theme = Console.ReadLine() ?? "AI Agents";

        // Define strongly-typed state
        MemeState initialState = new() { Theme = theme, Api = api, Iteration = 0 };

        // Create runnables (orchestrator will be set automatically during compilation)
        var entry = new EntryRunnable();
        var selector = new TemplateSelectorRunnable(api);
        var textGen = new TextGeneratorRunnable(api);
        var validator = new ValidatorRunnable(api);
        var retry = new RetryDecisionRunnable();
        var complete = new CompletionRunnable();

        // Build graph
        var graph = new OrchestrationGraphBuilder<MemeState>()
            .WithInitialState(initialState)
            .AddRunnable(entry)
            .AddRunnable(selector)
            .AddRunnable(textGen)
            .AddRunnable(validator)
            .AddRunnable(retry)
            .AddRunnable(complete)
            .SetEntryRunnable(entry)
            .SetOutputRunnable(complete, withDeadEnd: true)
            .AddEdge<string>(entry, selector, (t) => !string.IsNullOrEmpty(t))
            .AddEdge<TemplateInfo>(selector, textGen, (t) => t.TemplateId != null)
            .AddEdge<MemeData>(textGen, validator)
            .AddEdge<ValidationResult>(validator, complete, (v) => v.Approved)
            .AddEdge<ValidationResult>(validator, retry, (v) => !v.Approved)
            .AddEdge<TemplateInfo>(retry, textGen)
            .Build();

        // Compile graph (validates structure, types, state access)
        var compiler = new OrchestrationGraphCompiler<MemeState>();
        var compiledGraph = compiler.Compile(graph, initialState);

        // Create runtime from compiled graph
        ChatRuntime runtime = ChatRuntime.FromCompiledGraph(compiledGraph);

        // Execute the orchestration graph
        ChatMessage result = await runtime.InvokeAsync(new ChatMessage(ChatMessageRoles.User, theme));
        Console.WriteLine($"\n{result.Content}");

        Console.ReadLine();
    }
}

// --- Strongly-Typed State ---
class MemeState : IOrchestrationState
{
    public string Theme { get; set; } = "";
    public TornadoApi Api { get; set; } = null!;
    public int Iteration { get; set; }
    public TemplateInfo? Template { get; set; }
    public string? MemeUrl { get; set; }
}

// --- Graph Nodes (Runnables) ---
record TemplateInfo(string TemplateId, int LineCount);
record MemeData(string Url, string[] Text);
record ValidationResult(bool Approved, double Score, string[] Issues);

// Output schemas for structured responses
record MemeTextOutput(string[] TextLines);
record ValidationOutput(bool Approved, double Score, string[] Issues);

class EntryRunnable : OrchestrationRunnable<ChatMessage, string>
{
    public EntryRunnable() : base(null, "Entry") { }
    
    [RequiresStateProperty(nameof(MemeState.Theme))]
    public override ValueTask<string> Invoke(RunnableProcess<ChatMessage, string> process)
    {
        var state = GetState<MemeState>();
        return ValueTask.FromResult(state.Theme);
    }
}

class TemplateSelectorRunnable : OrchestrationRunnable<string, TemplateInfo>
{
    private readonly TornadoApi _api;
    private MCPServer? _mcpServer;
    
    public TemplateSelectorRunnable(TornadoApi api) : base(null, "Selector")
    {
        _api = api;
    }
    
    [RequiresStateProperty(nameof(MemeState.Api))]
    public override async ValueTask<TemplateInfo> Invoke(RunnableProcess<string, TemplateInfo> process)
    {
        var state = GetState<MemeState>();
        
        if (_mcpServer == null)
        {
            _mcpServer = MCPToolkits.Meme();
            await _mcpServer.InitializeAsync();
        }
        
        TornadoAgent agent = new(state.Api, ChatModel.OpenAi.Gpt4.O, "Selector",
            $"Pick a funny meme template for: {process.Input}");
        agent.AddTool(_mcpServer.AllowedTornadoTools.ToArray());
        
        string? templateId = null;
        int lineCount = 2;
        agent.AddTool(new Tool((string id, int lines) => {
            templateId = id; lineCount = lines; agent.Cancel();
            return "Selected";
        }, "confirm_template"));
        
        await agent.Run("Select template", maxTurns: 5);
        Console.WriteLine($"✓ Template: {templateId} ({lineCount} lines)");
        
        TemplateInfo template = new(templateId ?? "drake", lineCount);
        state.Template = template; // Store in state
        return template;
    }
}

class TextGeneratorRunnable : OrchestrationRunnable<TemplateInfo, MemeData>
{
    private readonly TornadoApi _api;
    
    public TextGeneratorRunnable(TornadoApi api) : base(null, "TextGen")
    {
        _api = api;
    }
    
    [RequiresStateProperty(nameof(MemeState.Theme))]
    [RequiresStateProperty(nameof(MemeState.Iteration))]
    public override async ValueTask<MemeData> Invoke(RunnableProcess<TemplateInfo, MemeData> process)
    {
        var state = GetState<MemeState>();
        string feedback = state.Iteration > 0 ? "Previous failed validation. Be funnier!" : "";
        
        TornadoAgent agent = new(_api, ChatModel.OpenAi.Gpt4.O, "TextGen",
            $"Generate {process.Input.LineCount} SHORT meme lines about: {state.Theme}. Max 6 words/line. {feedback}",
            outputSchema: typeof(MemeTextOutput));
        
        Conversation conv = await agent.Run($"Create meme text for: {state.Theme}");
        var result = conv.Messages[^1].Content?.ParseJson<MemeTextOutput>();
        string[] lines = result?.TextLines ?? ["Default", "Text"];
        
        // Build meme URL using MemeCls helper
        string url = McpMemeCls.BuildMemeUrl(process.Input.TemplateId, lines);
        
        Console.WriteLine($"📝 Text: {string.Join(" / ", lines)}");
        state.MemeUrl = url;
        return new(url, lines);
    }
}

class ValidatorRunnable : OrchestrationRunnable<MemeData, ValidationResult>
{
    private readonly TornadoApi _api;
    
    public ValidatorRunnable(TornadoApi api) : base(null, "Validator")
    {
        _api = api;
    }
    
    public override async ValueTask<ValidationResult> Invoke(RunnableProcess<MemeData, ValidationResult> process)
    {
        using HttpClient http = new();
        byte[] bytes = await http.GetByteArrayAsync(process.Input.Url);
        string base64 = Convert.ToBase64String(bytes);
        
        TornadoAgent agent = new(_api, ChatModel.OpenAi.Gpt4.O, "Validator",
            "Rate 0.0-1.0. Approve if >= 0.7. Check: readable? funny? relevant?",
            outputSchema: typeof(ValidationOutput));
        
        Conversation conv = await agent.Run([
            new ChatMessagePart("Validate this meme:"),
            new ChatMessagePart(new ChatImage($"data:image/jpeg;base64,{base64}"))
        ]);
        
        // Check for API errors
        if (conv?.Error != null)
        {
            Console.WriteLine($"⚠️ API error: {conv.Error.Response}");
            return new(false, 0.0, [$"API error: {conv.Error.Response}"]);
        }
        
        var result = conv.Messages.Last().Content?.ParseJson<ValidationOutput>();
        
        if (result == null)
        {
            Console.WriteLine("⚠️ Validation failed to parse, defaulting to not approved");
            return new(false, 0.0, ["Failed to parse validation result"]);
        }
        
        Console.WriteLine($"🎯 Score: {result.Score:F2} | Approved: {result.Approved}");
        return new(result.Approved, result.Score, result.Issues);
    }
}

class RetryDecisionRunnable : OrchestrationRunnable<ValidationResult, TemplateInfo>
{
    public RetryDecisionRunnable() : base(null, "Retry") { }
    
    [RequiresStateProperty(nameof(MemeState.Iteration))]
    [RequiresStateProperty(nameof(MemeState.Template))]
    public override ValueTask<TemplateInfo> Invoke(RunnableProcess<ValidationResult, TemplateInfo> process)
    {
        var state = GetState<MemeState>();
        state.Iteration++;
        Console.WriteLine($"🔄 Retry {state.Iteration}/3");
        
        if (state.Template == null)
            throw new InvalidOperationException("Template not found in state");
            
        return ValueTask.FromResult(state.Template);
    }
}

class CompletionRunnable : OrchestrationRunnable<ValidationResult, ChatMessage>
{
    public CompletionRunnable() : base(null, "Complete") 
    {
        AllowDeadEnd = true; // Terminal node
    }
    
    [RequiresStateProperty(nameof(MemeState.MemeUrl))]
    public override ValueTask<ChatMessage> Invoke(RunnableProcess<ValidationResult, ChatMessage> process)
    {
        var state = GetState<MemeState>();
        
        // Display meme image in terminal if Chafa is available
        DisplayMemeImage(state.MemeUrl).GetAwaiter().GetResult();
        
        string msg = process.Input.Approved 
            ? $"✅ SUCCESS! Meme: {state.MemeUrl}" 
            : $"⚠️ Max retries. Best: {state.MemeUrl}";
        return ValueTask.FromResult(new ChatMessage(ChatMessageRoles.Assistant, msg));
    }
    
    private static async Task DisplayMemeImage(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        
        try
        {
            using HttpClient http = new();
            byte[] imageBytes = await http.GetByteArrayAsync(url);
            string tempFile = $"{Path.GetTempFileName()}.jpg";
            await File.WriteAllBytesAsync(tempFile, imageBytes);
            
            if (await ProgramExists("chafa"))
            {
                Process process = new Process();
                process.StartInfo.FileName = "chafa";
                process.StartInfo.Arguments = tempFile;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                await process.WaitForExitAsync();
            }
            
            try { File.Delete(tempFile); } catch { }
        }
        catch { /* Ignore display errors */ }
    }
    
    private static async Task<bool> ProgramExists(string name)
    {
        try
        {
            using Process checkProcess = new Process();
            checkProcess.StartInfo = new ProcessStartInfo
            {
                FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "where" : "which",
                Arguments = name,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            checkProcess.Start();
            await checkProcess.WaitForExitAsync();
            return checkProcess.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}