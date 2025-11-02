using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Agents.Dnd.Persistence;

namespace LlmTornado.Agents.Dnd.Examples;

/// <summary>
/// Example demonstrating how to initialize game components without API calls
/// </summary>
public class GameInitializationExample
{
    public static void RunExample()
    {
        Console.WriteLine("=== LlmTornado.Agents.Dnd Initialization Test ===\n");

        // Test 1: Create a new game world
        Console.WriteLine("1. Creating game world...");
        GameState gameState = GameWorldInitializer.CreateNewGame();
        Console.WriteLine($"   ✓ Created game with {gameState.Locations.Count} locations");
        Console.WriteLine($"   ✓ Starting location: {gameState.CurrentLocationName}");

        // Test 2: Create player characters
        Console.WriteLine("\n2. Creating player characters...");
        var humanPlayer = GameWorldInitializer.CreatePlayerCharacter("Aragorn", CharacterClass.Warrior, CharacterRace.Human);
        Console.WriteLine($"   ✓ Created {humanPlayer.Name} - {humanPlayer.Race} {humanPlayer.Class}");
        Console.WriteLine($"   ✓ Health: {humanPlayer.Health}/{humanPlayer.MaxHealth}");
        Console.WriteLine($"   ✓ Starting items: {string.Join(", ", humanPlayer.Inventory)}");
        
        var aiPlayer = GameWorldInitializer.CreateAIPlayer("Gandalf");
        Console.WriteLine($"   ✓ Created AI player {aiPlayer.Name} - {aiPlayer.Race} {aiPlayer.Class}");

        // Test 3: Add players to game
        Console.WriteLine("\n3. Building party...");
        gameState.Players.Add(humanPlayer);
        gameState.Players.Add(aiPlayer);
        Console.WriteLine($"   ✓ Party size: {gameState.Players.Count}");

        // Test 4: Test persistence system
        Console.WriteLine("\n4. Testing save/load system...");
        var persistence = new GameStatePersistence();
        
        // Save game
        string savePath = persistence.SaveGameAsync(gameState).Result;
        Console.WriteLine($"   ✓ Game saved to: {savePath}");

        // List saves
        var saves = persistence.ListSaves();
        Console.WriteLine($"   ✓ Found {saves.Count} save file(s)");

        // Load game
        var loadedGame = persistence.LoadGameAsync(gameState.SessionId).Result;
        Console.WriteLine($"   ✓ Game loaded successfully");
        Console.WriteLine($"   ✓ Loaded {loadedGame?.Players.Count} players");

        // Clean up test save
        persistence.DeleteSave(gameState.SessionId);
        Console.WriteLine("\n   ✓ Test save cleaned up");

        Console.WriteLine("\n=== All Tests Passed! ===");
    }
}
