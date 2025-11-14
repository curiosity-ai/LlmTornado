using System.Linq;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Tokenize.Vendors;

internal class VendorCohereTokenizeRequest
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; }

    public VendorCohereTokenizeRequest(TokenizeRequest request, IEndpointProvider provider)
    {
        Model = request.Model?.Name ?? string.Empty;

        // Cohere only supports text tokenization
        if (!string.IsNullOrWhiteSpace(request.Text))
        {
            Text = request.Text;
        }
        else if (request.Messages is not null && request.Messages.Count > 0)
        {
            // Extract text from first message if no Text provided
            ChatMessage? firstMsg = request.Messages.FirstOrDefault();
            Text = firstMsg?.Content ?? string.Empty;
        }
        else
        {
            Text = string.Empty;
        }
    }
}

