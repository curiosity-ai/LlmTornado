using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Uses AI to generate context-aware combat zones
/// </summary>
public class CombatZoneGenerator
{
    private readonly TornadoApi _client;

    public CombatZoneGenerator(TornadoApi client)
    {
        _client = client;
    }

    /// <summary>
    /// Generate a combat zone setup based on the context
    /// </summary>
    public async Task<CombatZoneSetup> GenerateCombatZoneAsync(
        string locationName,
        string locationDescription,
        List<string> playerNames,
        List<string> enemyNames,
        string encounterReason)
    {
        string instructions = """
            You are a tactical combat designer for a D&D game. Generate a strategic combat zone setup based on the given context.
            
            Consider:
            - The location and its features (indoor/outdoor, terrain type, obstacles)
            - Number of combatants and their roles
            - Tactical advantages and disadvantages
            - Environmental storytelling through terrain
            - Balance between challenge and fairness
            
            Create a 10x10 grid battlefield with:
            - Strategic positioning for players and enemies
            - Contextual obstacles (rocks, trees, walls, furniture, etc.)
            - Varied starting distances
            - Tactical cover and elevation opportunities
            
            Make the battlefield interesting and thematic to the location.
            """;

        var agent = new TornadoAgent(
            client: _client,
            model: ChatModel.OpenAi.Gpt4.OMini,
            name: "Combat Zone Designer",
            instructions: instructions,
            outputSchema: typeof(CombatZoneSetup));

        var contextMessage = $"""
            Location: {locationName}
            Environment: {locationDescription}
            
            Players ({playerNames.Count}): {string.Join(", ", playerNames)}
            Enemies ({enemyNames.Count}): {string.Join(", ", enemyNames)}
            
            Encounter Context: {encounterReason}
            
            Generate a tactical combat zone that makes sense for this scenario.
            Place players and enemies strategically, add appropriate obstacles and terrain features.
            Grid coordinates are from (0,0) to (9,9).
            """;

        var conversation = await agent.Run(
            appendMessages: new List<ChatMessage> 
            { 
                new ChatMessage(ChatMessageRoles.User, contextMessage) 
            });

        CombatZoneSetup? setupResult = await conversation.Messages.Last().Content?.SmartParseJsonAsync<CombatZoneSetup>(agent);

        if (setupResult == null || !setupResult.HasValue)
        {
            // Fallback to basic setup
            return CreateFallbackSetup(playerNames, enemyNames, locationName);
        }

        return setupResult.Value;
    }

    /// <summary>
    /// Create a basic fallback setup if AI fails
    /// </summary>
    private CombatZoneSetup CreateFallbackSetup(List<string> playerNames, List<string> enemyNames, string location)
    {
        var setup = new CombatZoneSetup
        {
            Terrain = "Open Ground",
            Description = $"A simple battlefield at {location}",
            GridWidth = 10,
            GridHeight = 10,
            PlayerPositions = new List<EntitySetup>(),
            EnemyPositions = new List<EntitySetup>(),
            Obstacles = new List<ObstacleSetup>()
        };

        // Position players on the left
        for (int i = 0; i < playerNames.Count; i++)
        {
            setup.PlayerPositions.Add(new EntitySetup(playerNames[i], 1, 4 + i, "normal"));
        }

        // Position enemies on the right
        for (int i = 0; i < enemyNames.Count; i++)
        {
            setup.EnemyPositions.Add(new EntitySetup(enemyNames[i], 8, 4 + i, "normal"));
        }

        // Add some basic obstacles
        setup.Obstacles.Add(new ObstacleSetup(5, 3, "rock", "Large rock"));
        setup.Obstacles.Add(new ObstacleSetup(4, 6, "rock", "Boulder"));
        setup.Obstacles.Add(new ObstacleSetup(6, 5, "tree", "Old tree"));

        return setup;
    }
}
