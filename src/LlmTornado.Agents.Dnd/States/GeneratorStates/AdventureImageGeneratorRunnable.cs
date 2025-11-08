using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using LlmTornado.Agents.Dnd.FantasyEngine.States.ActionStates;
using LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Images;
using LlmTornado.Images.Models;

namespace LlmTornado.Agents.Dnd.States.GeneratorStates;

internal class AdventureImageGeneratorRunnable : OrchestrationRunnable<AdventureBreakdown, AdventureBreakdown>
{
    TornadoApi _client;
    FantasyWorldState _worldState;
    string _imageTheme = string.Empty;
    public AdventureImageGeneratorRunnable(TornadoApi client, FantasyWorldState worldState, Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        _worldState = worldState;
    }

    public override async ValueTask<AdventureBreakdown> Invoke(RunnableProcess<AdventureBreakdown, AdventureBreakdown> input)
    {
        _imageTheme = await GenerateThemeForImages();
        List<Task> imageGenerationTasks = new List<Task>();
        foreach (var location in input.Input.Locations)
        {
            imageGenerationTasks.Add(Task.Run( async () => await GenerateImageForLocationAsync(location, _imageTheme)));
        }
        await Task.WhenAll(imageGenerationTasks);

        imageGenerationTasks.Clear();
        foreach (var npc in input.Input.NonPlayerCharacters)
        {
            imageGenerationTasks.Add(Task.Run( async () => await GenerateImageForNpc(npc,  _imageTheme)));
        }
        await Task.WhenAll(imageGenerationTasks);

        imageGenerationTasks.Clear();

        foreach (var item in input.Input.Items)
        {
            imageGenerationTasks.Add(Task.Run( async () => await GenerateImageForItems(item,  _imageTheme)));
        }
        await Task.WhenAll(imageGenerationTasks);


        return input.Input;
    }

    public async Task<string> GenerateThemeForImages()
    {
        string prompt = $@"Your goal is to generate a theme for a series of images that will be used later keep images consistent.
Each location, item, and NPC, will be generated separately, but should all follow the same theme.
Information about the images will be provided with descriptions so keep the theme focused on style and tone.
.Use the provided user prompt to get an idea of the potential style. Keep it concise, max length of the theme should be less than 1000 characters.";
        TornadoAgent agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5, "Adventure Image Theme Generator",prompt);
        string context = File.ReadAllText(_worldState.AdventureFile);
        var response = await agent.Run(context);
        var lastmessage = response.Messages.Last();

        if(lastmessage == null)
        {
            throw new Exception("No response from image theme generator.");
        }

        if(lastmessage.Parts != null)
        {
            string textPart = lastmessage.Parts.Last().Text ?? string.Empty;
            if(!string.IsNullOrEmpty(textPart))
            {
                return textPart;
            }
        }

        return lastmessage.Content ?? lastmessage.GetMessageContent();
    }

    public async Task GenerateImageForItems(FantasyItemResult item, string theme)
    {
        try
        {
            string prompt = $@"Your goal is to generate a Portrait image for the following Item to be used later as a reference for generating more images in a role playing game.

Do not include Text or UI elements overlayed on the image.

Image Series Theme:
    {theme}

Item Name:
{item.Name}

Item Description:
{item.Description}
";
            ImageGenerationResult? generatedImg = await _client.ImageGenerations.CreateImage(
                new ImageGenerationRequest(
                    prompt,
                    model: ImageModel.Google.Imagen.V4Generate001)
                );

            await SaveImages(item.Name, generatedImg);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating image for item {item.Name}: {ex.Message}");
        }
    }

    public async Task GenerateImageForNpc(FantasyNpcResult npc, string theme)
    {
        try
        {
            string prompt = $@"Your goal is to generate a Portrait image for the following NPC to be used later as a reference for generating more images in a role playing game.
Do not include Text or UI elements overlayed on the image.
Image Series Theme:
 {theme}

NPC Name:
{npc.Name}

NPC Description:
{npc.Description}
";
            ImageGenerationResult? generatedImg = await _client.ImageGenerations.CreateImage(
            new ImageGenerationRequest(
                prompt,
                responseFormat: TornadoImageResponseFormats.Base64,
                model: ImageModel.Google.Imagen.V4Generate001)
            );
            await SaveImages(npc.Name, generatedImg);
        }
      catch (Exception ex)
        {
            Console.WriteLine($"Error generating image for NPC {npc.Name}: {ex.Message}");
        }


    }

    public async Task GenerateImageForLocationAsync(FantasyLocationResult location, string theme)
    {
        try
        {
            string prompt = $@"Your goal is to generate a Cover image for the following location to be used later as a reference for generating more images in a role playing game.
Do not include Text or UI elements overlayed on the image.
Image Series Theme:
 {theme}

Location Name:
{location.Name}

Location Description:
{location.Description}
";

            ImageGenerationResult? generatedImg = await _client.ImageGenerations.CreateImage(
                new ImageGenerationRequest(
                    prompt,
                    responseFormat: TornadoImageResponseFormats.Base64,
                    model: ImageModel.Google.Imagen.V4Generate001)
                );

            await SaveImages(location.Name, generatedImg);
        }
       catch (Exception ex)
        {
            Console.WriteLine($"Error generating image for location {location.Name}: {ex.Message}");
        }
    }

    public static async Task SaveImages(string imageName, ImageGenerationResult generatedImg)
    {
        if (generatedImg?.Data == null || generatedImg.Data.Count == 0)
        {
            Console.WriteLine("No image data available.");
            return;
        }

        foreach (var imgData in generatedImg.Data)
        {
            byte[] imageBytes;
            if (!string.IsNullOrEmpty(imgData.Base64))
            {
                imageBytes = Convert.FromBase64String(imgData.Base64);
            }
            else if (!string.IsNullOrEmpty(imgData.Url))
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    imageBytes = await httpClient.GetByteArrayAsync(imgData.Url);
                }
            }
            else
            {
                Console.WriteLine("No base64 data or URL available in the image result.");
                continue;
            }
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), $"{imageName}.png");
            await File.WriteAllBytesAsync(filePath, imageBytes);
            Console.WriteLine($"Image saved to: {filePath}");
        }
    }
}
