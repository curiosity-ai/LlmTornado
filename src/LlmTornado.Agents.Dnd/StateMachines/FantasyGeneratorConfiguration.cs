using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;
using LlmTornado.Agents.Dnd.States.GeneratorStates;
using LlmTornado.Code;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyGenerator;

public class FantasyGeneratorConfiguration : Orchestration<string, bool>
{
    public static string GeneratedAdventuresFilePath => Path.Combine(Directory.GetCurrentDirectory(), "Game_Data", "GeneratedAdventures");

    public static string latestGeneratedTitle = "";
    public static string CurrentAdventurePath => Path.Combine(GeneratedAdventuresFilePath, latestGeneratedTitle.Replace(" ", "_").Replace(":","_"));

    TornadoApi api;

    AdventureOverviewGeneratorRunnable overviewGeneratorRunnable;
    AdventureGeneratorRunnable adventureGeneratorRunnable;
    AdventureEditorRunnable adventureEditorRunnable;
    AdventureExtractionRunnable adventureExtractionRunnable;
    EndGeneratingState endGeneratingState;

    public FantasyGeneratorConfiguration()
    {
        api = new TornadoApi([
            new ProviderAuthentication(Code.LLmProviders.OpenAi, Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
            new ProviderAuthentication(Code.LLmProviders.Anthropic, Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
            new ProviderAuthentication(Code.LLmProviders.Google, Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
        ]);

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


}
