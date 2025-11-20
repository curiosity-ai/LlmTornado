using System.Collections.Generic;
using System.Linq;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Google;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Tokenize.Vendors;

internal class VendorGoogleTokenizeRequest
{
    [JsonProperty("contents")]
    public List<VendorGoogleChatRequest.VendorGoogleChatRequestMessage> Contents { get; set; } = [];
    
    public VendorGoogleTokenizeRequest(TokenizeRequest request, IEndpointProvider provider)
    {
        // Google needs model interpolation in URL: /v1beta/models/{model}:countTokens
        if (request.Model is not null)
        {
            request.OverrideUrl($"{provider.ApiUrl(CapabilityEndpoints.Tokenize, null)}/{request.Model.Name}:countTokens");
        }

        if (request.Messages is not null)
        {
            foreach (ChatMessage msg in request.Messages)
            {
                Contents.Add(new VendorGoogleChatRequest.VendorGoogleChatRequestMessage(msg, new VendorGoogleChatRequest { Model = request.Model }));
            }
        }
        else if (request.Text is not null)
        {
            Contents.Add(new VendorGoogleChatRequest.VendorGoogleChatRequestMessage(new ChatMessage
            {
                Role = ChatMessageRoles.User,
                Content = request.Text
            }, new VendorGoogleChatRequest { Model = request.Model }));
        }
    }
}

