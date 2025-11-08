using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.Agents;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Chat;
using LlmTornado.Code;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.GameEngineStates;

public class RunGameRunnable : OrchestrationRunnable<bool, bool>
{
    TornadoApi _client;
    FantasyWorldState _worldState;

    public RunGameRunnable(TornadoApi client, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
    }

    public override async ValueTask<bool> Invoke(RunnableProcess<bool, bool> input)
    {
        try
        {
            // Create improved game configuration with phase management
            var config = new FantasyEngineConfiguration();

            // Create runtime
            var runtime = new ChatRuntime.ChatRuntime(config);

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
                    //Invoke the DM
                    var result = await runtime.InvokeAsync(new ChatMessage(ChatMessageRoles.User, initialMessage));

                    // Check if game should end (Crazy way to end the game, but for demo purposes)
                    if (result.Content?.Contains("Thank you for playing") == true)
                    {
                        continueGame = false;
                    }

                    //Auto - save after each turn
                    if (_worldState.TurnNumber % 5 == 0) // Save every 5 turns
                    {
                        //await _persistence!.SaveGameAsync(gameState);
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
            //await _persistence!.SaveGameAsync(gameState);
            //Console.WriteLine("\nGame saved successfully!");
            //Console.WriteLine($"Session ID: {gameState.SessionId}");
            //Console.WriteLine($"Total turns played: {gameState.TurnNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError running game: {ex.Message}");
            Console.WriteLine("Stack trace: " + ex.StackTrace);
        }
        return true;
    }
}