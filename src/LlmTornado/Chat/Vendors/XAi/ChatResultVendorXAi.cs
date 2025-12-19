using System.Collections.Generic;
using System.Linq;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Chat.Vendors.XAi;

internal class ChatResultVendorXAi : ChatResult
{
    [JsonProperty("citations")]
    public List<string>? Citations { get; set; }

    public static ChatResult? Deserialize(string json)
    {
        ChatResultVendorXAi? resultEx = JsonConvert.DeserializeObject<ChatResultVendorXAi>(json);

        if (resultEx is not null)
        {
            resultEx.VendorExtensions = new ChatResponseVendorExtensions
            {
                XAi = new ChatResponseVendorXAiExtensions
                {
                    Citations = resultEx.Citations
                }
            };
            
            // Convert ReasoningContent to Parts with reasoning part to harmonize with other providers
            if (resultEx.Choices is not null)
            {
                foreach (ChatChoice choice in resultEx.Choices)
                {
                    if (choice.Message is not null && choice.Message.ReasoningTokens is not null)
                    {
                        choice.Message.Parts ??= [];
                        
                        // Add text part if content exists
                        if (!string.IsNullOrEmpty(choice.Message.Content))
                        {
                            if (!choice.Message.Parts.Any(p => p.Type == ChatMessageTypes.Text))
                            {
                                choice.Message.Parts.Add(new ChatMessagePart(ChatMessageTypes.Text)
                                {
                                    Text = choice.Message.Content
                                });
                            }
                        }
                        
                        // Add reasoning part
                        if (!choice.Message.Parts.Any(p => p.Type == ChatMessageTypes.Reasoning))
                        {
                            choice.Message.Parts.Add(new ChatMessagePart(new ChatMessageReasoningData
                            {
                                Content  = choice.Message.ReasoningTokens,
                                Provider = LLmProviders.XAi
                            }));
                        }
                    }
                }
            }
        }
        
        return resultEx;
    }
}