using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Demo;

public class OpenRouterDemo : DemoBase
{
    [TornadoTest]
    public static async Task OpenRouterGenerateImage()
    {
        TornadoApi api = Program.Connect();

        Conversation chat = api.Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.OpenRouter.All.Gemini25FlashImage,
            Modalities = [ChatModelModalities.Text, ChatModelModalities.Image]
        });

        chat.AppendUserInput("Generate a simple blue square on white background");

        ChatRichResponse? response = await chat.GetResponseRich();

        Console.WriteLine($"Content: {response?.Text}");

        ChatMessage? lastMessage = response?.Result?.Choices?.FirstOrDefault()?.Message;
        if (lastMessage?.Images?.Count > 0)
        {
            Console.WriteLine($"Generated {lastMessage.Images.Count} image(s):");
            foreach (ChatOutputImage img in lastMessage.Images)
            {
                Console.WriteLine($"  Type: {img.Type}");
                string? url = img.Image?.Url;
                
                if (url != null)
                {
                    int prefixLen = Math.Min(80, url.Length);
                    Console.WriteLine($"  URL: {url[..prefixLen]}...");
                    Console.WriteLine($"  URL length: {url.Length} chars");
                    
                    await DisplayImage(img.Image.Url.Replace("data:image/png;base64,", string.Empty));
                }
            }
        }
        else
        {
            Console.WriteLine("No images in response");
        }
    }

}
