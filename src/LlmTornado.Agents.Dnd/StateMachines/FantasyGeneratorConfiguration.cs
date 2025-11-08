using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;
using LlmTornado.Code;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyGenerator;

public class FantasyGeneratorConfiguration : Orchestration<string, bool>
{
    TornadoApi api;
    AdventureMdGeneratorRunnable adventureMdGeneratorRunnable;
    public FantasyGeneratorConfiguration()
    {
        api = new TornadoApi([
            new ProviderAuthentication(Code.LLmProviders.OpenAi, Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
            new ProviderAuthentication(Code.LLmProviders.Anthropic, Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
            new ProviderAuthentication(Code.LLmProviders.Google, Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
        ]);

        adventureMdGeneratorRunnable = new AdventureMdGeneratorRunnable(api,this);

        SetEntryRunnable(adventureMdGeneratorRunnable);

    }


}
