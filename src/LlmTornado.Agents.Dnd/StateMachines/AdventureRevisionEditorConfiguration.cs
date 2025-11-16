using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;

namespace LlmTornado.Agents.Dnd.FantasyGenerator;

internal sealed class AdventureRevisionEditorConfiguration : Orchestration<bool, bool>
{
    private readonly AdventureEditorRunnable _editor;
    private readonly AdventureExtractionRunnable _extractor;

    public AdventureRevisionEditorConfiguration(TornadoApi api)
    {
        _editor = new AdventureEditorRunnable(api, this);
        _extractor = new AdventureExtractionRunnable(api, this) { AllowDeadEnd = true };

        _editor.AddAdvancer(condition => condition, _extractor);
        _editor.AddAdvancer(condition => !condition, _extractor);

        SetEntryRunnable(_editor);
        SetRunnableWithResult(_extractor);
    }
}

