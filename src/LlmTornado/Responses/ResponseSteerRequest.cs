using System;
using System.Collections.Generic;
using System.Linq;
using LlmTornado.Chat;
using LlmTornado.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Responses;

/// <summary>User input queued for a response on the same WebSocket connection.
/// Acceptance is not application: the successor's response.created event commits the input.</summary>
public class ResponseSteerRequest
{
    public string PreviousResponseId { get; set; } = string.Empty;
    public string? InputString { get; set; }
    public List<ResponseInputMessage>? InputMessages { get; set; }

    public ResponseSteerRequest() { }
    public ResponseSteerRequest(string previousResponseId, string input) { PreviousResponseId = previousResponseId; InputString = input; }
    public ResponseSteerRequest(string previousResponseId, List<ResponseInputMessage> input) { PreviousResponseId = previousResponseId; InputMessages = input; }

    internal JObject ToPayload()
    {
        if (string.IsNullOrWhiteSpace(PreviousResponseId)) throw new ArgumentException("A steering target response ID is required.");
        if ((InputString is null) == (InputMessages is null)) throw new ArgumentException("Specify steering text or user messages, not both.");
        JToken input;
        if (InputString is not null) input = new JValue(InputString);
        else
        {
            if (InputMessages!.Count == 0) throw new ArgumentException("Steering requires a nonempty message array.");
            JArray messages = [];
            foreach (ResponseInputMessage message in InputMessages)
            {
                if (message is null || message.Role != ChatMessageRoles.User || message.Status is not null || message.Phase is not null)
                    throw new ArgumentException("Steering accepts user messages with type, role and content only.");
                if (message.Content is null || message.Content.Any(x => x is null || x.Type is not ("input_text" or "input_image" or "input_file")))
                    throw new ArgumentException("Steering content supports only input_text, input_image and input_file.");
                messages.Add(new JObject { ["type"] = "message", ["role"] = "user", ["content"] = JArray.FromObject(message.Content, JsonSerializer.Create(EndpointBase.NullSettings)) });
            }
            input = messages;
        }
        return new JObject { ["type"] = "response.steer", ["previous_response_id"] = PreviousResponseId, ["input"] = input };
    }
}

/// <summary>Client-side state of a steering submission. Connection loss leaves uncommitted input unknown.</summary>
public enum ResponseSteerSubmissionStatus { Sent, Accepted, WaitingForInput, Committed, Failed, Unknown }

/// <summary>Submission journal for reconciling steering. Never automatically replay an unknown submission.</summary>
public class ResponseSteerSubmission
{
    public string? Id { get; internal set; }
    public string PreviousResponseId { get; internal set; } = string.Empty;
    public string? StreamId { get; internal set; }
    public JToken? Input { get; internal set; }
    public ResponseSteerSubmissionStatus Status { get; internal set; }
    public string? SuccessorResponseId { get; internal set; }
    public Events.ResponseSteerError? Error { get; internal set; }
}
