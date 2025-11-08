using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Mcp;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.GameEngineStates;

internal class MarkdownMemoryUpdatorRunnable : OrchestrationRunnable<FantasyDMResult, FantasyDMResult>
{
    TornadoApi _client;
    TornadoAgent _agent;
    MCPServer _markdownTool;
    bool _initialized = false;
    FantasyWorldState _worldState;
    List<ChatMessage> _conversationHistory = new List<ChatMessage>();
    Conversation _conv;
    public MarkdownMemoryUpdatorRunnable(TornadoApi client, FantasyWorldState worldState, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        _worldState = worldState;

        string instructions = $"""
            You are an assistant that updates the game memory stored in markdown format.
            Given the current memory and new information, you will update the memory by adding new details, removing outdated information, and ensuring consistency.
            The memory is in markdown format, so maintain proper markdown syntax.
            Focus on clarity and conciseness while preserving important details.
            """;
        _agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Mini,"Mark", instructions);
    }

    public override async ValueTask InitializeRunnable()
    {
        if(!_initialized)
        {
            if (string.IsNullOrEmpty(_worldState.AdventureFile) || string.IsNullOrEmpty(_worldState.MemoryFile))
            {
                throw new InvalidOperationException("Adventure file or memory file is not set in the world state.");
            }

            _markdownTool = new MCPServer(
             "markdown-editor", "uvx", arguments: new string[] { "--from", "quantalogic-markdown-mcp", "python", "-m", "quantalogic_markdown_mcp.mcp_server" },
             allowedTools: ["load_document", "insert_section", "delete_section", "update_section", "get_section", "list_sections", "move_section", "get_document", "save_document", "analyze_document"]);
            await _markdownTool.InitializeAsync();

            _agent.AddTool(_markdownTool.AllowedTornadoTools.ToArray());

            _initialized = true;
        }
    }

    public void CheckMemoryFileExists()
    {
        if (!File.Exists(_worldState.MemoryFile))
        {
            if(!Directory.Exists(Path.GetDirectoryName(_worldState.MemoryFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_worldState.MemoryFile)!);
            }

            File.WriteAllText(_worldState.MemoryFile, "# Game Memory\n\n");
        }
    }

    public override async ValueTask<FantasyDMResult> Invoke(RunnableProcess<FantasyDMResult, FantasyDMResult> input)
    {

        string currentMemory = File.ReadAllText(_worldState.MemoryFile);

        string prompt = $"""
            Given the following new information, update the game memory stored in markdown format using the markdown editing tools.
            Ensure that the memory remains coherent, relevant, and well-structured. After updating, save the changes to the memory file.
            When finished Summarize the changes made to the memory in a concise manner.

            Memory File Name : {_worldState.MemoryFile}
            """;

        List<ChatMessage> inputMessage = new List<ChatMessage>();

        inputMessage.AddRange(_conversationHistory);
        inputMessage.AddRange(new[] { new ChatMessage(Code.ChatMessageRoles.User, @$"
Update the memory with the following information: 
Current Location:
{input.Input.CurrentLocation}

Narration: 
{input.Input.Narration}
"
) });
        //Current Actions Taken: {"Actions Taken:\n" + string.Join("\n", input.Input.Actions.Select(a => $"- {a}"
        _conv = await _agent.Run(prompt, appendMessages: inputMessage);

        _conversationHistory.Add(_conv.Messages.Last());

        return input.Input;
    }
}
