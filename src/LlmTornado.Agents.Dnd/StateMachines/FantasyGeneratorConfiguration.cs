using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;
using LlmTornado.Agents.Dnd.States.GeneratorStates;
using LlmTornado.Agents.Dnd.Utility;
using LlmTornado.Code;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyGenerator;

public class FantasyGeneratorConfiguration : Orchestration<string, bool>
{
    private static string _currentAdventureRootPath = Path.Combine(Directory.GetCurrentDirectory(), "Game_Data", "GeneratedAdventures");

    public static string GeneratedAdventuresFilePath => Path.Combine(Directory.GetCurrentDirectory(), "Game_Data", "GeneratedAdventures");

    public static string latestGeneratedTitle = "";
    public static string CurrentRevisionId { get; set; } = "";

    public static string CurrentAdventureRootPath
    {
        get => _currentAdventureRootPath;
        set => _currentAdventureRootPath = value;
    }

    public static string CurrentAdventurePath => Path.Combine(CurrentAdventureRootPath, AdventureRevisionManager.RevisionsFolderName, CurrentRevisionId);

    TornadoApi api;

    AdventureOverviewGeneratorRunnable overviewGeneratorRunnable;
    AdventureGeneratorRunnable adventureGeneratorRunnable;
    AdventureEditorRunnable adventureEditorRunnable;
    AdventureExtractionRunnable adventureExtractionRunnable;
    EndGeneratingState endGeneratingState;

    public FantasyGeneratorConfiguration()
    {
        api = CreateGeneratorClient();

        overviewGeneratorRunnable = new AdventureOverviewGeneratorRunnable(api, this);
        adventureGeneratorRunnable = new AdventureGeneratorRunnable(api, this);
        adventureEditorRunnable = new AdventureEditorRunnable(api, this);
        adventureExtractionRunnable = new AdventureExtractionRunnable(api, this) { AllowDeadEnd = true };
        endGeneratingState = new EndGeneratingState(this) { AllowDeadEnd = true };

        overviewGeneratorRunnable.AddAdvancer((condition)=> condition, adventureGeneratorRunnable);
        overviewGeneratorRunnable.AddAdvancer((condition) => !condition, endGeneratingState);
        adventureGeneratorRunnable.AddAdvancer((condition) => condition, adventureEditorRunnable);
        adventureGeneratorRunnable.AddAdvancer((condition) => !condition, endGeneratingState);
        adventureEditorRunnable.AddAdvancer((condition) => condition, adventureExtractionRunnable);
        adventureEditorRunnable.AddAdvancer((condition) => !condition, endGeneratingState);
        adventureExtractionRunnable.AddAdvancer((condition) => condition, endGeneratingState);

        SetEntryRunnable(overviewGeneratorRunnable);
        SetRunnableWithResult(adventureExtractionRunnable);
    }

    public static void SetAdventureContext(string adventureTitle, string adventureRootPath, string revisionId)
    {
        latestGeneratedTitle = adventureTitle;
        CurrentAdventureRootPath = adventureRootPath;
        CurrentRevisionId = revisionId;
    }

    public static TornadoApi CreateGeneratorClient()
    {
        return new TornadoApi([
            new ProviderAuthentication(Code.LLmProviders.OpenAi, Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
            new ProviderAuthentication(Code.LLmProviders.Anthropic, Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
            new ProviderAuthentication(Code.LLmProviders.Google, Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
        ]);
    }
}
