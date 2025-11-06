using LlmTornado;
using LlmTornado.Agents.Dnd.Agents;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.States.ActionStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Agents.Dnd.Persistence;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;
using System;
using System.Text;
using ChatRuntimeClass = LlmTornado.Agents.ChatRuntime.ChatRuntime;

namespace LlmTornado.Agents.Dnd;

class Program
{
    private static GameStatePersistence? _persistence;
    private static TornadoApi? _client;

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        

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
        //_persistence = new GameStatePersistence();

        await Test();

        return;

        await ShowMainMenu();
    }

    static async Task Test()
    {

        string adinstructions = @$" You are an expert DnD adventure generator. Your job is to generate a complete DnD adventure in markdown format based on the provided theme.
In the adventure, you should include the following sections:
# Adventure Title
# Introduction
# Quests
# Locations
# Items
# Non-Player Characters (NPCs)
Each section should be well-detailed and formatted in markdown. Use headings, subheadings, bullet points, and other markdown features to enhance readability.
The adventure should be engaging, imaginative, and suitable for a DnD campaign.
";
        TornadoAgent advGen = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5, "Adventure Md Generator", adinstructions, outputSchema: typeof(MarkdownFile));


        var theme = "Sci-fi Space Ship";
        var adConvo = await advGen.Run(theme);
        MarkdownFile? mdFile = await adConvo.Messages.Last().Content.SmartParseJsonAsync<MarkdownFile>(advGen);
        if (mdFile == null || !mdFile.HasValue)
        {
            return;
        }
        string fileName = $"{mdFile.Value.AdventureTitle.Replace(" ", "_")}.md";
        await File.WriteAllTextAsync(fileName, mdFile.Value.Content);
        Console.WriteLine($"Adventure markdown file generated: {fileName}");
        Console.WriteLine(mdFile.Value.Content);




        FantasyWorldState worldState = new FantasyWorldState()
        {
            Player = new FantasyPlayer("John", "Normal dude"),
        };
        TornadoAgent DMAgent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Mini, outputSchema: typeof(FantasyDMResult));
        string _adventureTitle = "Test Adventure";
        string _adventureDescription = "This is a test so follow along please";
        string _adventureSetting = "You will get the settings from the message history";
        string instruct = $"""
            You are an experienced Dungeon Master running the adventure: "{_adventureTitle}"
            
            Adventure Description: {_adventureDescription}
            Setting: {_adventureSetting}

            Your role is to:
            - Follow the adventure structure loosely - use it as a guide
            - Extract Actions from the User input such as
                //World Actions
                - Move, //Move

                // Item actions
                - UseItem, //When user says to use inventory item
                - GetItem, //When you need to give user item
                - DropItem, //When user says to drop inventory item

                //Party Actions
                - ActorJoinsParty, //When you decide the NPC in the game need to follow along
                - ActorLeavesParty, //When you decide the NPC leaves the party

            - Describe scenes vividly and engagingly based on the generated world
            - Respond to player actions with narrative flair
            - Control NPCs and the environment according to the adventure
            - Progress the main quest line naturally when appropriate
            - Create interesting scenarios aligned with the adventure theme
            - Decide when combat should be initiated based on encounters in the adventure
            - Make the game fun and immersive
            - STAY NEUTRAL and UNBIASED - do not favor any player or NPC
            
            Reference the generated quests, scenes, and NPCs but don't feel constrained by them.
            Use your creativity to enhance the experience.
            
            
            """;

        DMAgent.Instructions = instruct;

        string userActions = "Yeah Can I get a taco and a drink? Oh hey Mark! What took you so long to meet me here?";

        List<ChatMessage> messages = new List<ChatMessage>();

        messages.Add(new ChatMessage(ChatMessageRoles.Assistant, "You are at taco bell trying to order food. What would you like to do?"));

        Conversation conv = await DMAgent.Run(userActions, appendMessages: messages);

        FantasyDMResult? result = await conv.Messages.Last().Content.SmartParseJsonAsync<FantasyDMResult>(DMAgent);

        if (result.HasValue)
        {
            var val = result.Value;
            Console.WriteLine(val);
        }
        else
        {
            throw new Exception("Failed to parse DM result from agent response.");
        }
        List<FantasyActionContent> actions = result.Value.Actions.Where(a => a.ActionType == FantasyActionType.LoseItem || a.ActionType == FantasyActionType.GetItem).ToList();
        string instructions = @$" You are an expert item extractor. You job is to extract the item from the content and provide a Name and description for the Item. 

The player can only lose an item if it has it.
The player already has the following items:
Inventory:
{string.Join(",\n", worldState.Player.Inventory) + "\n"}

Narration from Game Master: 
{result.Value.Narration}
";
        TornadoAgent agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Mini, instructions: instructions, outputSchema: typeof(DetectedFantasyItems));
        var convo = await agent.Run(actions.FirstOrDefault().ActionContent);
        DetectedFantasyItems? detected = await convo.Messages.Last().Content.SmartParseJsonAsync<DetectedFantasyItems>(agent);
        if (detected.HasValue)
        {
            foreach (var item in detected.Value.ItemsGained)
            {
                Console.WriteLine($"Getting item: {item.Name} - {item.Description}");
                worldState.Player.Inventory.Add(new FantasyItem(item.Name, item.Description));
            }

            foreach (var losing in detected.Value.ItemsLost)
            {
                if (worldState.Player.Inventory.Any(item => item.Name.Contains(losing.Name)))
                {
                    Console.WriteLine($"Losing Item: {losing.Name} - {losing.Description}");
                    worldState.Player.Inventory.RemoveAll(item => item.Name == losing.Name);
                }
            }
        }
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
            Console.WriteLine("  4. Generate New Adventure");
            Console.WriteLine("  5. List Generated Adventures");
            Console.WriteLine("  6. Exit");
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
                    await GenerateAdventure();
                    break;
                case "5":
                    ListGeneratedAdventures();
                    break;
                case "6":
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

        // Optionally select an Adventure
        string? selectedAdventureId = null;
        var adventurePersistence = new AdventurePersistence();
        var availableAdventures = adventurePersistence.ListAdventures();
        
        if (availableAdventures.Count > 0)
        {
            Console.WriteLine("\n" + new string('─', 80));
            Console.WriteLine("Select an Adventure (or press Enter to use default world):");
            Console.WriteLine(new string('─', 80));
            Console.WriteLine("  0. Use default world (no Adventure)");
            for (int i = 0; i < availableAdventures.Count; i++)
            {
                var adventure = availableAdventures[i];
                Console.WriteLine($"  {i + 1}. {adventure.Name} ({adventure.Difficulty}) - {adventure.Description.Substring(0, Math.Min(50, adventure.Description.Length))}...");
            }
            Console.Write($"Select (0-{availableAdventures.Count}): ");
            string? adventureChoice = Console.ReadLine();
            
            if (!string.IsNullOrWhiteSpace(adventureChoice) && int.TryParse(adventureChoice, out int adventureIndex) && adventureIndex >= 1 && adventureIndex <= availableAdventures.Count)
            {
                selectedAdventureId = availableAdventures[adventureIndex - 1].Id;
                Console.WriteLine($"\n✓ Selected Adventure: {availableAdventures[adventureIndex - 1].Name}");
            }
            else
            {
                Console.WriteLine("\n✓ Using default world");
            }
        }

        // Create game state
        var gameState = GameWorldInitializer.CreateNewGame(selectedAdventureId);
        
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

    static async Task GenerateAdventure()
    {
        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine("🎲 Adventure Generator");
        Console.WriteLine(new string('═', 80));
        Console.WriteLine("\nThis will use AI to generate a complete adventure with:");
        Console.WriteLine("  ✨ Adventure description and difficulty");
        Console.WriteLine("  📜 Main quest line (20+ quests)");
        Console.WriteLine("  🗺️  Interconnected scenes and world map");
        Console.WriteLine("  👹 Boss encounters with scaled stats");
        Console.WriteLine("  🎯 Side quests for optional content");
        Console.WriteLine("  ⚔️  Trash mob encounters");
        Console.WriteLine("  💎 Rare events and special loot");
        Console.WriteLine("\n⚠️  Note: Generation may take several minutes and will use API credits.\n");

        Console.Write("Do you want to continue? (y/n): ");
        string? confirm = Console.ReadLine();
        
        if (confirm?.ToLower() != "y")
        {
            Console.WriteLine("Adventure generation cancelled.");
            return;
        }

        Console.Write("\nOptional: Enter adventure theme/seed (or press Enter for AI to decide): ");
        string? seed = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(seed))
        {
            seed = null;
        }

        try
        {
            var persistence = new AdventurePersistence();
            var config = new AdventureGeneratorConfiguration(_client!, persistence, seed);
            var runtime = new ChatRuntimeClass(config);

            Console.WriteLine("\n🎲 Starting adventure generation...\n");
            Console.WriteLine(new string('─', 80));

            var result = await runtime.InvokeAsync(new ChatMessage(ChatMessageRoles.User, "Generate adventure"));

            Console.WriteLine(new string('─', 80));
            
            if (config.Adventure != null && !string.IsNullOrEmpty(config.Adventure.Id))
            {
                Console.WriteLine("\n✅ Adventure generated successfully!");
                Console.WriteLine($"\n📖 Adventure: {config.Adventure.Name}");
                Console.WriteLine($"📝 Description: {config.Adventure.Description}");
                Console.WriteLine($"⚡ Difficulty: {config.Adventure.Difficulty}");
                Console.WriteLine($"🗺️  Scenes: {config.Adventure.Scenes.Count}");
                Console.WriteLine($"📜 Main Quests: {config.Adventure.MainQuestLine.Count}");
                Console.WriteLine($"🎯 Side Quests: {config.Adventure.SideQuests.Count}");
                Console.WriteLine($"👹 Bosses: {config.Adventure.Bosses.Count}");
                Console.WriteLine($"💎 Rare Events: {config.Adventure.RareEvents.Count}");
                Console.WriteLine($"\n💾 Adventure ID: {config.Adventure.Id}");
                Console.WriteLine("\nYou can now start a new game and reference this adventure!");
            }
            else
            {
                Console.WriteLine("\n❌ Adventure generation failed. Please try again.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error generating adventure: {ex.Message}");
            Console.WriteLine("Please check your API key and try again.");
        }
    }

    static void ListGeneratedAdventures()
    {
        var persistence = new AdventurePersistence();
        var adventures = persistence.ListAdventures();
        
        if (adventures.Count == 0)
        {
            Console.WriteLine("\nNo generated adventures found.");
            Console.WriteLine("Use option 4 to generate a new adventure!");
            return;
        }

        Console.WriteLine("\n" + new string('═', 80));
        Console.WriteLine("Generated Adventures:");
        Console.WriteLine(new string('═', 80));
        
        foreach (var adventure in adventures)
        {
            Console.WriteLine($"\n📖 {adventure.Name}");
            Console.WriteLine($"   Description: {adventure.Description}");
            Console.WriteLine($"   Difficulty: {adventure.Difficulty}");
            Console.WriteLine($"   Quests: {adventure.QuestCount}");
            Console.WriteLine($"   Progress: {adventure.Progress}");
            Console.WriteLine($"   Generated: {adventure.GeneratedAt:g}");
            Console.WriteLine($"   ID: {adventure.Id}");
        }
        
        Console.WriteLine("\n💡 Tip: You can reference these adventures when starting a new game.");
    }

    static async Task RunGame(GameState gameState)
    {
        try
        {
            // Create improved game configuration with phase management
            var config = new ImprovedDndGameConfiguration(_client!, gameState);
            
            // Create runtime
            var runtime = new ChatRuntimeClass(config);

            // Start the game
            string initialMessage = "The adventure begins...";
            
            Console.WriteLine("\n🎮 Game started! Type 'quit' at any time to exit and save.\n");
            Console.WriteLine("💡 The game has two main phases:");
            Console.WriteLine("   🗺️  Adventuring Phase - Explore, talk to NPCs, and interact with the world");
            Console.WriteLine("   ⚔️  Combat Phase - Tactical turn-based combat on a grid\n");

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
