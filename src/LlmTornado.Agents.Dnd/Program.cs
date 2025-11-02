using LlmTornado;
using LlmTornado.Agents.Dnd.Agents;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Agents.Dnd.Persistence;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Common;
using ChatRuntimeClass = LlmTornado.Agents.ChatRuntime.ChatRuntime;

namespace LlmTornado.Agents.Dnd;

class Program
{
    private static GameStatePersistence? _persistence;
    private static TornadoApi? _client;

    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        LlmTornado D&D - AI-Powered Dungeon & Dragons Adventure        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Initialize API client
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: OPENAI_API_KEY environment variable not set.");
            Console.WriteLine("Please set your OpenAI API key:");
            Console.Write("API Key: ");
            apiKey = Console.ReadLine();
            
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Cannot start without API key. Exiting...");
                return;
            }
        }

        _client = new TornadoApi(apiKey, LLmProviders.OpenAi);
        _persistence = new GameStatePersistence();

        await ShowMainMenu();
    }

    static async Task ShowMainMenu()
    {
        while (true)
        {
            Console.WriteLine("\n" + new string('═', 80));
            Console.WriteLine("Main Menu:");
            Console.WriteLine("  1. Start New Adventure");
            Console.WriteLine("  2. Load Saved Game");
            Console.WriteLine("  3. List Saved Games");
            Console.WriteLine("  4. Exit");
            Console.WriteLine(new string('═', 80));
            Console.Write("Select option: ");

            string? choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await StartNewGame();
                    break;
                case "2":
                    await LoadGame();
                    break;
                case "3":
                    ListSavedGames();
                    break;
                case "4":
                    Console.WriteLine("Farewell, adventurer!");
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
    }

    static async Task StartNewGame()
    {
        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine("Character Creation");
        Console.WriteLine(new string('═', 80));

        Console.Write("Enter your character name: ");
        string? name = Console.ReadLine();
        if (string.IsNullOrEmpty(name)) name = "Adventurer";

        Console.WriteLine("\nChoose your class:");
        var classes = CharacterClassFactory.GetAllClasses().ToList();
        for (int i = 0; i < classes.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {classes[i].Name} - {classes[i].Description}");
        }
        Console.Write($"Select (1-{classes.Count}): ");
        string? classChoice = Console.ReadLine();
        
        int classIndex = int.TryParse(classChoice, out int idx) && idx >= 1 && idx <= classes.Count 
            ? idx - 1 
            : 0;
        CharacterClass characterClass = classes[classIndex].ClassType;

        Console.WriteLine("\nChoose your race:");
        var races = CharacterRaceFactory.GetAllRaces().ToList();
        for (int i = 0; i < races.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {races[i].Name} - {races[i].Description}");
        }
        Console.Write($"Select (1-{races.Count}): ");
        string? raceChoice = Console.ReadLine();
        
        int raceIndex = int.TryParse(raceChoice, out int ridx) && ridx >= 1 && ridx <= races.Count 
            ? ridx - 1 
            : 0;
        CharacterRace race = races[raceIndex].RaceType;

        Console.WriteLine("\nHow many AI companions do you want? (0-3): ");
        string? aiCountStr = Console.ReadLine();
        int aiCount = int.TryParse(aiCountStr, out int count) ? Math.Min(Math.Max(count, 0), 3) : 0;

        // Create game state
        var gameState = GameWorldInitializer.CreateNewGame();
        
        // Create player character
        var player = GameWorldInitializer.CreatePlayerCharacter(name, characterClass, race);
        gameState.Players.Add(player);

        // Create AI companions
        string[] aiNames = { "Thorin", "Elara", "Grimm" };
        for (int i = 0; i < aiCount; i++)
        {
            var aiPlayer = GameWorldInitializer.CreateAIPlayer(aiNames[i]);
            gameState.Players.Add(aiPlayer);
            Console.WriteLine($"AI companion {aiPlayer.Name} ({aiPlayer.Race} {aiPlayer.Class}) has joined your party!");
        }

        Console.WriteLine($"\nWelcome, {player.Name} the {player.Race} {player.Class}!");
        Console.WriteLine("Your adventure begins...\n");

        await RunGame(gameState);
    }

    static async Task LoadGame()
    {
        var saves = _persistence!.ListSaves();
        
        if (saves.Count == 0)
        {
            Console.WriteLine("\nNo saved games found.");
            return;
        }

        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine("Saved Games:");
        Console.WriteLine(new string('═', 80));
        
        for (int i = 0; i < saves.Count; i++)
        {
            var (sessionId, lastSaved) = saves[i];
            Console.WriteLine($"  {i + 1}. Session: {sessionId.Substring(0, 8)}... (Last saved: {lastSaved:g})");
        }

        Console.Write("\nSelect save to load (1-" + saves.Count + "): ");
        string? choice = Console.ReadLine();
        
        if (int.TryParse(choice, out int index) && index > 0 && index <= saves.Count)
        {
            var (sessionId, _) = saves[index - 1];
            var gameState = await _persistence.LoadGameAsync(sessionId);
            
            if (gameState != null)
            {
                Console.WriteLine($"\nGame loaded! Continuing from turn {gameState.TurnNumber}...");
                await RunGame(gameState);
            }
            else
            {
                Console.WriteLine("\nError loading game.");
            }
        }
        else
        {
            Console.WriteLine("\nInvalid selection.");
        }
    }

    static void ListSavedGames()
    {
        var saves = _persistence!.ListSaves();
        
        if (saves.Count == 0)
        {
            Console.WriteLine("\nNo saved games found.");
            return;
        }

        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine("Saved Games:");
        Console.WriteLine(new string('═', 80));
        
        foreach (var (sessionId, lastSaved) in saves)
        {
            Console.WriteLine($"  Session: {sessionId}");
            Console.WriteLine($"  Last Saved: {lastSaved:g}");
            Console.WriteLine();
        }
    }

    static async Task RunGame(GameState gameState)
    {
        try
        {
            // Create game configuration
            var config = new DndGameConfiguration(_client!, gameState);
            
            // Create runtime
            var runtime = new ChatRuntimeClass(config);

            // Start the game with initial DM narration
            var currentLocation = gameState.Locations[gameState.CurrentLocationName];
            string initialMessage = $"The party finds themselves at: {currentLocation.Name}. What happens next?";
            
            Console.WriteLine("\nGame started! Type 'quit' at any time to exit and save.\n");

            // Game loop - run until player quits
            bool continueGame = true;
            while (continueGame)
            {
                try
                {
                    var result = await runtime.InvokeAsync(new ChatMessage(ChatMessageRoles.User, initialMessage));
                    
                    // Check if game should end
                    if (result.Content?.Contains("Thank you for playing") == true)
                    {
                        continueGame = false;
                    }
                    
                    // Auto-save after each turn
                    if (gameState.TurnNumber % 5 == 0) // Save every 5 turns
                    {
                        await _persistence!.SaveGameAsync(gameState);
                        Console.WriteLine("\n[Game auto-saved]");
                    }

                    initialMessage = "Continue the adventure...";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nError during game: {ex.Message}");
                    continueGame = false;
                }
            }

            // Final save
            await _persistence!.SaveGameAsync(gameState);
            Console.WriteLine("\nGame saved successfully!");
            Console.WriteLine($"Session ID: {gameState.SessionId}");
            Console.WriteLine($"Total turns played: {gameState.TurnNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError running game: {ex.Message}");
            Console.WriteLine("Stack trace: " + ex.StackTrace);
        }
    }
}
