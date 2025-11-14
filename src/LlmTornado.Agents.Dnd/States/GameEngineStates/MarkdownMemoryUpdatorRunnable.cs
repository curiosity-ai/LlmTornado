using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
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
    PersistentConversation _conversationHistory;
    Conversation _conv;
    string instructions;

    public MarkdownMemoryUpdatorRunnable(TornadoApi client, FantasyWorldState worldState, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        _worldState = worldState;

        instructions = $"""
            You are an expert Game State manager and markdown specialist that keeps track of the adventure that unfolds from the narration.
            This will be used to help the AI Dungeon Master remember important details about the adventure as it progresses.
            If the progress seemed stalled Add a temporary note to suggest possible new objectives or directions for the adventure to take.
            You save information in markdown format, so maintain proper markdown syntax.
            Memory File Name : {_worldState.MemoryFile}
            Consider a section for:
                - Current Act
                - Current Objective
                - Global Counters or debuff stacks to worry about
                - Secondary Objectives
                - Player Inventory
                - Summary of player relations/Interactions with NPCs
                - stats that are relevant to the adventure
            If the objective is multifaceted, break it down into sub-objectives using bullet points or numbered lists.
            Ensure that the memory is easy to read and navigate.
            Check off completed objectives while adding new ones as they arise.
            Use the provided markdown editing tools to make updates to the memory file. Memory File Name : {_worldState.MemoryFile}
            Keep the memory file organized with clear headings and sections for different types of information (e.g., Objectives, Inventory, Stats).
            Keep the memory concise and relevant to the current state of the adventure max 2000 words.
            When a objective is fully completed, or if the information is no longer relevant, move it to the log file : {_worldState.CompletedObjectivesFile}
            When finished Summarize the changes made to the file in a concise manner.
            """;

        _agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Mini,"Mark", instructions);

        _conversationHistory = new PersistentConversation($"DM_{_worldState.AdventureFile.Replace(".json", "_")}LongTermMemoryRecorder.json", true);

    }

    public override async ValueTask InitializeRunnable()
    {
        if(!_initialized)
        {
            CheckMemoryFileExists();

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
        if(string.IsNullOrEmpty(_worldState.MemoryFile))
        {
            _worldState.MemoryFile = _worldState.AdventureFile.Replace(".json", "_progress.md");
        }

        if(string.IsNullOrEmpty(_worldState.CompletedObjectivesFile))
        {
            _worldState.CompletedObjectivesFile = _worldState.AdventureFile.Replace(".json", "_completed.md");
        }

        if (!File.Exists(_worldState.MemoryFile))
        {
            if(!Directory.Exists(Path.GetDirectoryName(_worldState.MemoryFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_worldState.MemoryFile)!);
            }

            File.WriteAllText(_worldState.MemoryFile, "# Objectives\n\n");
        }

        if(!File.Exists(_worldState.CompletedObjectivesFile))
        {
            string? dir = Path.GetDirectoryName(_worldState.CompletedObjectivesFile);
            if (!string.IsNullOrEmpty(dir)  && !Directory.Exists(dir) )
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_worldState.CompletedObjectivesFile, "# Completed Objectives Log\n\n");
        }
    }

    public override async ValueTask<FantasyDMResult> Invoke(RunnableProcess<FantasyDMResult, FantasyDMResult> input)
    {
        FantasyDMResult dMResult = input.Input;

        string currentMemory = File.ReadAllText(_worldState.MemoryFile);

        _conversationHistory.AppendMessage(new ChatMessage(Code.ChatMessageRoles.User, @$"
Update the memory with the following information: 
Info Log:

From DM Result:
Scene Complete: {dMResult.SceneCompletionPercentage}%
Current Location: {dMResult.CurrentLocation}
Current Act: {dMResult.CurrentAct}
Current Scene: {dMResult.CurrentScene}
Current Scene Turn: {_worldState.CurrentSceneTurns}

Narration: 
{input.Input.Narration}


Possible Player Actions:
{string.Join("\n\n", input.Input.NextActions.Select(a => $"- {a.Action} (min required roll: {a.MinimumSuccessThreshold}) \n Success Outcome: {a.SuccessOutcome} \n Failure Outcome: {a.FailureOutcome}"))}

From GameState Result:
Current Turns Taken this scene (want to limit to 15): {_worldState.CurrentSceneTurns}

Adventure Overview:
{_worldState.Adventure.Overview}

Current Act:
{_worldState.Adventure.Acts[_worldState.CurrentAct].Title}

Current Overview:
{_worldState.Adventure.Acts[_worldState.CurrentAct].Overview}

Act Progression:
{_worldState.CurrentScene / _worldState.Adventure.Acts[_worldState.CurrentAct].Scenes.Count()}

Current Scene:
{_worldState.Adventure.Acts[_worldState.CurrentAct].Scenes[_worldState.CurrentScene]}

"));

        //Current Actions Taken: {"Actions Taken:\n" + string.Join("\n", input.Input.Actions.Select(a => $"- {a}"
        _conv = await _agent.Run(appendMessages: _conversationHistory.Messages.TakeLast(10).ToList());

        _conversationHistory.AppendMessage(_conv.Messages.Last());

        return input.Input;
    }
}
