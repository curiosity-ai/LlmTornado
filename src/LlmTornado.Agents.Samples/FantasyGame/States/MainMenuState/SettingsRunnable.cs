using System;
using System.IO;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.MainMenuState;

public class SettingsRunnable : OrchestrationRunnable<MainMenuSelection, bool>
{
    public SettingsRunnable(Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
    }

    public override ValueTask<bool> Invoke(RunnableProcess<MainMenuSelection, bool> input)
    {
        while (true)
        {
            Console.WriteLine("\n" + new string('-', 80));
            Console.WriteLine("Settings");
            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"  1. Toggle narration TTS [{(FantasyEngineConfiguration.WorldState.EnableTts ? "ON" : "OFF")}]");
            Console.WriteLine("  0. Back to Main Menu");
            Console.Write("Select option: ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    FantasyEngineConfiguration.WorldState.EnableTts = !FantasyEngineConfiguration.WorldState.EnableTts;
                    FantasyEngineConfiguration.Settings.EnableTts = FantasyEngineConfiguration.WorldState.EnableTts;
                    SavePreference();
                    PersistPreferenceIntoActiveWorld();
                    Console.WriteLine($"Narration TTS {(FantasyEngineConfiguration.WorldState.EnableTts ? "enabled" : "disabled")}.");
                    break;
                case "0":
                case "":
                    return ValueTask.FromResult(false);
                default:
                    Console.WriteLine("Invalid selection. Please try again.");
                    break;
            }
        }
    }

    private static void SavePreference()
    {
        try
        {
            FantasyEngineConfiguration.Settings.Save(FantasyEngineConfiguration.SettingsFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Unable to save settings: {ex.Message}");
        }
    }

    private static void PersistPreferenceIntoActiveWorld()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(FantasyEngineConfiguration.WorldState.SaveDataDirectory) &&
                Directory.Exists(FantasyEngineConfiguration.WorldState.SaveDataDirectory))
            {
                FantasyEngineConfiguration.WorldState.SerializeToFile(FantasyEngineConfiguration.WorldState.WorldStateFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Unable to update save file with new TTS preference: {ex.Message}");
        }
    }
}

