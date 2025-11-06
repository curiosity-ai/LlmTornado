using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Chat.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.States.GeneratorStates;

[Description("A markdown file containing a complete DnD adventure.")]
public struct MarkdownFile
{
    [Description("The title of the adventure.")]
    public string AdventureTitle { get; set; }

    [Description("The content of the markdown file.")]
    public string Content { get; set; }
}

internal class AdventureMdGeneratorRunnable : OrchestrationRunnable<string, bool>
{
    TornadoApi _client;
    TornadoAgent _agent;
    public AdventureMdGeneratorRunnable(TornadoApi client,Orchestration orchestrator, string runnableName = "") : base(orchestrator, runnableName)
    {
        _client = client;
        string instructions = @$" You are an expert DnD adventure generator. Your job is to generate a complete DnD adventure in markdown format based on the provided theme.
In the adventure, you should include the following sections:
# Adventure Title
# Introduction
# Quests
# Locations
# Items
# Non-Player Characters (NPCs)
Each section should be well-detailed and formatted in markdown. Use headings, subheadings, bullet points, and other markdown features to enhance readability.
The adventure should be engaging, imaginative, and suitable for a DnD campaign.
";
        _agent = new TornadoAgent(_client, ChatModel.OpenAi.Gpt5.V5Pro, "Adventure Md Generator", instructions, outputSchema: typeof(MarkdownFile));
    }

    public override async ValueTask<bool> Invoke(RunnableProcess<string, bool> input)
    {
        var theme = input.Input;
        var result = await _agent.Run(theme);
        MarkdownFile? mdFile = await result.Messages.Last().Content.SmartParseJsonAsync<MarkdownFile>(_agent);
        if(mdFile == null || !mdFile.HasValue)
        {
            return false;
        }
        string fileName = $"{mdFile.Value.AdventureTitle.Replace(" ", "_")}.md";
        await File.WriteAllTextAsync(fileName, mdFile.Value.Content);
        Console.WriteLine($"Adventure markdown file generated: {fileName}");
        Console.WriteLine(mdFile.Value.Content);
        return true;
    }
}
