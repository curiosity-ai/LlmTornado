using System.Collections.Generic;
using System.Linq;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Tokenize.Vendors;

internal class VendorMoonshotAiTokenizeRequest
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("messages")]
    public List<VendorMoonshotAiTokenizeMessage> Messages { get; set; } = [];

    public VendorMoonshotAiTokenizeRequest(TokenizeRequest request, IEndpointProvider provider)
    {
        Model = request.Model?.Name ?? string.Empty;

        if (request.Messages is not null && request.Messages.Count > 0)
        {
            foreach (ChatMessage msg in request.Messages)
            {
                // MoonshotAI accepts system messages in the messages array
                Messages.Add(new VendorMoonshotAiTokenizeMessage(msg));
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Text))
        {
            // If only text is provided, create a user message from it
            Messages.Add(new VendorMoonshotAiTokenizeMessage(new ChatMessage(ChatMessageRoles.User, request.Text!)));
        }
    }
}

internal class VendorMoonshotAiTokenizeMessage
{
    [JsonProperty("role")]
    public string Role { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }

    public VendorMoonshotAiTokenizeMessage(ChatMessage msg)
    {
        Role = msg.Role switch
        {
            ChatMessageRoles.System => "system",
            ChatMessageRoles.User => "user",
            ChatMessageRoles.Assistant => "assistant",
            _ => "user"
        };

        Content = msg.Content ?? string.Empty;
    }
}

