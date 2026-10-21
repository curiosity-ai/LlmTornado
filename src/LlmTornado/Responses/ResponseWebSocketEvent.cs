using LlmTornado.Responses.Events;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Responses;

/// <summary>WebSocket envelope, including lane routing and raw data for future event types.</summary>
public class ResponseWebSocketEvent
{
    public string Type { get; internal set; } = string.Empty;
    public string? StreamId { get; internal set; }
    public string Data { get; internal set; } = string.Empty;
    public JObject Raw { get; internal set; } = new();
    public ResponseAgent? Agent => (Event as IResponseAgentEvent)?.Agent ?? Raw["agent"]?.ToObject<ResponseAgent>();
    public IResponseEvent? Event { get; internal set; }
    public ResponseResult? Response { get; internal set; }

    internal static ResponseWebSocketEvent Parse(string data)
    {
        JObject raw = JObject.Parse(data);
        string type = (string?)raw["type"] ?? string.Empty;
        IResponseEvent? parsed = ResponsesEndpoint.DeserializeResponseEvent(type, data);
        return new()
        {
            Type = type, StreamId = (string?)raw["stream_id"], Data = data, Raw = raw, Event = parsed,
            Response = parsed switch
            {
                ResponseEventCreated created => created.Response,
                ResponseEventCompleted completed => completed.Response,
                ResponseEventFailed failed => failed.Response,
                ResponseEventIncomplete incomplete => incomplete.Response,
                _ => null
            }
        };
    }
}
