using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine;

public class SaveGameRunnable : OrchestrationRunnable<bool, bool>
{
    public SaveGameRunnable(Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
    }

    public override ValueTask<bool> Invoke(RunnableProcess<bool, bool> input)
    {
        //Save Game Data
        return ValueTask.FromResult(true);
    }
}
