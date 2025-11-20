using LlmTornado.Agents.ChatRuntime.Orchestration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.States.GeneratorStates;

internal class EndGeneratingState : OrchestrationRunnable<bool, bool>
{
    public EndGeneratingState(Orchestration orchestration, string name = "") : base(orchestration, name)
    {

    }

    public override ValueTask<bool> Invoke(RunnableProcess<bool, bool> input)
    {
        Orchestrator?.HasCompletedSuccessfully();

        return ValueTask.FromResult(input.Input);
    }
}
