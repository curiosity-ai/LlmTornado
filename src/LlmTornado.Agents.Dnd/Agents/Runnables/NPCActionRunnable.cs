using LlmTornado.Agents.OrchestrationRunnables;
using LlmTornado.Chat;
using LlmTornado.Chat.Messaging;
using LlmTornado.ChatEndpoint;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Game;
using System.Text.Json;

namespace LlmTornado.Agents.Dnd.Agents.Runnables;

/// <summary>
/// Handles AI companion autonomous actions during the adventuring phase.
/// AI companions use GPT-4o-mini to decide their actions based on context.
/// </summary>
public class NPCActionRunnable : OrchestrationRunnable<PhaseResult, PhaseResult>
{
    private readonly TornadoApi _client;
    private readonly GameState _gameState;
    private readonly MemoryManager? _memoryManager;
    private readonly RelationshipManager? _relationshipManager;
    private readonly ContextManager? _contextManager;

    public NPCActionRunnable(
        TornadoApi client,
        GameState gameState,
        MemoryManager? memoryManager = null,
        RelationshipManager? relationshipManager = null,
        ContextManager? contextManager = null)
    {
        _client = client;
        _gameState = gameState;
        _memoryManager = memoryManager;
        _relationshipManager = relationshipManager;
        _contextManager = contextManager;
    }

    protected override async Task<PhaseResult> Invoke(PhaseResult input, CancellationToken cancellationToken = default)
    {
        // Only process if we're in adventuring phase and have AI companions
        if (_gameState.CurrentPhase != GamePhase.Adventuring || 
            _gameState.Party == null || 
            _gameState.Party.Count == 0)
        {
            return input;
        }

        // Process each AI companion
        foreach (var companion in _gameState.Party)
        {
            Console.WriteLine($"\n{'='*70}");
            Console.WriteLine($"🤖 {companion.Name}'s Turn");
            Console.WriteLine($"{'='*70}");

            // Get AI companion's action decision
            var action = await DecideActionAsync(companion, cancellationToken);
            
            if (!string.IsNullOrEmpty(action))
            {
                // Display and execute the action
                await ExecuteActionAsync(companion, action, cancellationToken);
            }

            // Brief pause between AI actions for readability
            await Task.Delay(500, cancellationToken);
        }

        return input;
    }

    private async Task<string> DecideActionAsync(PlayerCharacter companion, CancellationToken cancellationToken)
    {
        try
        {
            // Build context for AI decision
            var context = BuildNPCContext(companion);

            // Get conversation history if available
            var messages = new List<ChatMessage>();
            if (_contextManager != null)
            {
                var history = await _contextManager.GetConversationHistoryAsync(
                    $"npc_{companion.Name}", 
                    cancellationToken);
                messages.AddRange(history);
            }

            // Add system prompt
            messages.Insert(0, new ChatMessage(ChatMessageRole.System, @$"You are {companion.Name}, a {companion.Race} {companion.Class} in a D&D adventure.

Personality: {GetCompanionPersonality(companion)}

Current Context:
{context}

Decide on ONE action to take this turn. Choose from:
- explore: Look around and investigate the area
- talk [npc name]: Initiate conversation with an NPC
- examine [object]: Investigate something specific
- use [item]: Use an item from inventory
- rest: Take a rest to recover
- suggest move [direction/location]: Suggest the party move somewhere

Respond with ONLY the action command (e.g., 'talk Innkeeper' or 'explore' or 'examine strange altar').
Keep it natural to your personality and the current situation."));

            // Get AI decision
            var chatRequest = new ChatRequest
            {
                Model = "gpt-4o-mini",
                Messages = messages.ToArray(),
                Temperature = 0.8,
                MaxTokens = 100
            };

            var response = await _client.ChatEndpoint.GetCompletionAsync(chatRequest, cancellationToken);
            var action = response.FirstChoice?.Message?.Content?.Trim() ?? "";

            // Store in context
            if (_contextManager != null)
            {
                await _contextManager.AddMessageAsync(
                    $"npc_{companion.Name}",
                    new ChatMessage(ChatMessageRole.Assistant, action),
                    cancellationToken);
            }

            return action;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  {companion.Name} couldn't decide on an action: {ex.Message}");
            return ""; // Skip this companion's turn
        }
    }

    private string BuildNPCContext(PlayerCharacter companion)
    {
        var context = $@"Location: {_gameState.CurrentLocation?.Name ?? "Unknown"}
Description: {_gameState.CurrentLocation?.Description ?? ""}

Your Stats:
- Health: {companion.CurrentHealth}/{companion.MaxHealth}
- Level: {companion.Level}
- Gold: {companion.Gold}

Party Members: {string.Join(", ", _gameState.Party?.Select(p => p.Name) ?? Array.Empty<string>())}";

        // Add available NPCs
        if (_gameState.CurrentLocation?.NPCs?.Any() == true)
        {
            context += $"\n\nNPCs Present: {string.Join(", ", _gameState.CurrentLocation.NPCs)}";
        }

        // Add recent memories if available
        if (_memoryManager != null)
        {
            try
            {
                var memories = _memoryManager.RetrieveMemoriesAsync(
                    "Recent party activities",
                    topK: 3,
                    filter: null,
                    CancellationToken.None).GetAwaiter().GetResult();

                if (memories.Any())
                {
                    context += "\n\nRecent Events:\n";
                    foreach (var memory in memories.Take(3))
                    {
                        context += $"- {memory.Content}\n";
                    }
                }
            }
            catch { /* Ignore memory retrieval errors */ }
        }

        return context;
    }

    private string GetCompanionPersonality(PlayerCharacter companion)
    {
        // Generate personality based on class and race
        var personality = companion.Class switch
        {
            CharacterClass.Warrior => "You are brave and direct, preferring action over words. You're protective of the party.",
            CharacterClass.Mage => "You are curious and analytical, seeking knowledge. You prefer understanding before acting.",
            CharacterClass.Rogue => "You are cautious and observant, looking for opportunities. You value information and stealth.",
            CharacterClass.Cleric => "You are compassionate and wise, seeking to help others. You value harmony and healing.",
            _ => "You are adventurous and cooperative."
        };

        var raceTrait = companion.Race switch
        {
            CharacterRace.Elf => " Your elven heritage gives you an appreciation for nature and beauty.",
            CharacterRace.Dwarf => " Your dwarven nature makes you practical and determined.",
            CharacterRace.Halfling => " Your halfling optimism keeps spirits high even in danger.",
            CharacterRace.Human => " Your human adaptability helps you relate to all kinds of people.",
            _ => ""
        };

        return personality + raceTrait;
    }

    private async Task ExecuteActionAsync(PlayerCharacter companion, string action, CancellationToken cancellationToken)
    {
        Console.WriteLine($"💭 {companion.Name} decides: {action}");

        var actionLower = action.ToLower();

        // Store memory of this action
        if (_memoryManager != null)
        {
            try
            {
                await _memoryManager.StoreMemoryAsync(
                    $"{companion.Name} performed action: {action}",
                    MemoryType.PlayerPlayerInteraction,
                    ImportanceLevel.Medium,
                    new[] { companion.Name },
                    _gameState.CurrentLocation?.Name ?? "Unknown",
                    _gameState.Turn,
                    cancellationToken);
            }
            catch { /* Ignore memory storage errors */ }
        }

        // Update relationships if talking to NPC
        if (actionLower.StartsWith("talk ") && _relationshipManager != null)
        {
            var npcName = action.Substring(5).Trim();
            try
            {
                await _relationshipManager.UpdateRelationshipAsync(
                    companion.Name,
                    npcName,
                    friendlinessChange: 3,
                    trustChange: 2,
                    note: $"{companion.Name} initiated conversation",
                    cancellationToken);
            }
            catch { /* Ignore relationship errors */ }
        }

        // Provide feedback
        Console.WriteLine($"✅ {companion.Name} has taken their action.");
    }
}
