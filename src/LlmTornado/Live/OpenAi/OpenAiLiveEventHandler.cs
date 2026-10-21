using System;
using System.Threading.Tasks;

namespace LlmTornado.Live.OpenAi;

/// <summary>
/// Typed callbacks for GPT-Live server events. Unset handlers are skipped.
/// </summary>
public class OpenAiLiveEventHandler
{
    public Func<OpenAiLiveServerEvent, ValueTask>? OnEvent { get; set; }
    public Func<OpenAiLiveDecisionEvent, ValueTask>? OnDecisionEvent { get; set; }
    public Func<OpenAiLiveDecisionItemCreatedEvent, ValueTask>? OnDecisionItemCreated { get; set; }
    public Func<OpenAiLiveSessionStartedEvent, ValueTask>? OnSessionStarted { get; set; }
    public Func<OpenAiLiveSessionUpdatedEvent, ValueTask>? OnSessionUpdated { get; set; }
    public Func<OpenAiLiveSessionClosedEvent, ValueTask>? OnSessionClosed { get; set; }
    public Func<OpenAiLiveOutputAudioDeltaEvent, ValueTask>? OnOutputAudioDelta { get; set; }
    public Func<OpenAiLiveInputAudioReflectedEvent, ValueTask>? OnInputAudioReflected { get; set; }
    public Func<OpenAiLiveInputTranscriptDeltaEvent, ValueTask>? OnInputTranscriptDelta { get; set; }
    public Func<OpenAiLiveOutputTranscriptDeltaEvent, ValueTask>? OnOutputTranscriptDelta { get; set; }
    public Func<OpenAiLiveInstructionsAppendedEvent, ValueTask>? OnInstructionsAppended { get; set; }
    public Func<OpenAiLiveThinkingAppendedEvent, ValueTask>? OnThinkingAppended { get; set; }
    public Func<OpenAiLiveCommentaryAppendedEvent, ValueTask>? OnCommentaryAppended { get; set; }
    public Func<OpenAiLiveInputAudioMutedEvent, ValueTask>? OnInputAudioMuted { get; set; }
    public Func<OpenAiLiveInputAudioUnmutedEvent, ValueTask>? OnInputAudioUnmuted { get; set; }
    public Func<OpenAiLiveDelegationCreatedEvent, ValueTask>? OnDelegationCreated { get; set; }
    public Func<OpenAiLiveResponseEvent, ValueTask>? OnResponseEvent { get; set; }
    public Func<OpenAiLiveUsageUpdatedEvent, ValueTask>? OnUsageUpdated { get; set; }
    public Func<OpenAiLiveErrorEvent, ValueTask>? OnError { get; set; }
    public Func<OpenAiLiveDtmfReceivedEvent, ValueTask>? OnDtmfReceived { get; set; }
    public Func<OpenAiLiveDtmfSendEvent, ValueTask>? OnDtmfSend { get; set; }

    public async ValueTask DispatchAsync(OpenAiLiveServerEvent evt)
    {
        if (OnEvent is not null)
        {
            await OnEvent(evt).ConfigureAwait(false);
        }

        switch (evt)
        {
            case OpenAiLiveDecisionEvent decision when OnDecisionEvent is not null:
                await OnDecisionEvent(decision).ConfigureAwait(false);
                break;
            case OpenAiLiveDecisionItemCreatedEvent item when OnDecisionItemCreated is not null:
                await OnDecisionItemCreated(item).ConfigureAwait(false);
                break;
            case OpenAiLiveSessionStartedEvent started when OnSessionStarted is not null:
                await OnSessionStarted(started).ConfigureAwait(false);
                break;
            case OpenAiLiveSessionUpdatedEvent updated when OnSessionUpdated is not null:
                await OnSessionUpdated(updated).ConfigureAwait(false);
                break;
            case OpenAiLiveSessionClosedEvent closed when OnSessionClosed is not null:
                await OnSessionClosed(closed).ConfigureAwait(false);
                break;
            case OpenAiLiveOutputAudioDeltaEvent audio when OnOutputAudioDelta is not null:
                await OnOutputAudioDelta(audio).ConfigureAwait(false);
                break;
            case OpenAiLiveInputAudioReflectedEvent reflected when OnInputAudioReflected is not null:
                await OnInputAudioReflected(reflected).ConfigureAwait(false);
                break;
            case OpenAiLiveInputTranscriptDeltaEvent input when OnInputTranscriptDelta is not null:
                await OnInputTranscriptDelta(input).ConfigureAwait(false);
                break;
            case OpenAiLiveOutputTranscriptDeltaEvent output when OnOutputTranscriptDelta is not null:
                await OnOutputTranscriptDelta(output).ConfigureAwait(false);
                break;
            case OpenAiLiveInstructionsAppendedEvent instructions when OnInstructionsAppended is not null:
                await OnInstructionsAppended(instructions).ConfigureAwait(false);
                break;
            case OpenAiLiveThinkingAppendedEvent thinking when OnThinkingAppended is not null:
                await OnThinkingAppended(thinking).ConfigureAwait(false);
                break;
            case OpenAiLiveCommentaryAppendedEvent commentary when OnCommentaryAppended is not null:
                await OnCommentaryAppended(commentary).ConfigureAwait(false);
                break;
            case OpenAiLiveInputAudioMutedEvent muted when OnInputAudioMuted is not null:
                await OnInputAudioMuted(muted).ConfigureAwait(false);
                break;
            case OpenAiLiveInputAudioUnmutedEvent unmuted when OnInputAudioUnmuted is not null:
                await OnInputAudioUnmuted(unmuted).ConfigureAwait(false);
                break;
            case OpenAiLiveDelegationCreatedEvent delegation when OnDelegationCreated is not null:
                await OnDelegationCreated(delegation).ConfigureAwait(false);
                break;
            case OpenAiLiveResponseEvent response when OnResponseEvent is not null:
                await OnResponseEvent(response).ConfigureAwait(false);
                break;
            case OpenAiLiveUsageUpdatedEvent usage when OnUsageUpdated is not null:
                await OnUsageUpdated(usage).ConfigureAwait(false);
                break;
            case OpenAiLiveErrorEvent error when OnError is not null:
                await OnError(error).ConfigureAwait(false);
                break;
            case OpenAiLiveDtmfReceivedEvent dtmf when OnDtmfReceived is not null:
                await OnDtmfReceived(dtmf).ConfigureAwait(false);
                break;
            case OpenAiLiveDtmfSendEvent dtmfSend when OnDtmfSend is not null:
                await OnDtmfSend(dtmfSend).ConfigureAwait(false);
                break;
        }
    }
}
