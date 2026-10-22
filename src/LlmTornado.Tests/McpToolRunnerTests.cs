using LlmTornado.Agents;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Tests;

[TestFixture]
public class McpToolRunnerTests
{
    [Test]
    public async Task CallMcpToolAsync_KeepsEveryTextBlockOfTheResult()
    {
        FunctionResult result = await Call(
            new McpContentBlockText { Text = "first" },
            new McpContentBlockText { Text = "second" });

        Assert.That(result.Content, Is.EqualTo("first\nsecond"));
    }

    [Test]
    public async Task CallMcpToolAsync_WithOneTextBlock_ReturnsItsText()
    {
        FunctionResult result = await Call(new McpContentBlockText { Text = "only" });

        Assert.That(result.Content, Is.EqualTo("only"));
    }

    [Test]
    public async Task CallMcpToolAsync_WithoutTextBlocks_KeepsTheContentTheToolFilled()
    {
        FunctionResult result = await Call(new McpContentBlockImage { Data = "aW1n", MimeType = "image/png" });

        Assert.That(result.Content, Is.EqualTo("filled"));
    }

    private static async Task<FunctionResult> Call(params IMcpContentBlock[] blocks)
    {
        Tool tool = new Tool(new ToolFunction("lookup", "Look something up"))
        {
            RemoteTool = new McpTool
            {
                CallAsync = (_, _, _, _, _) => ValueTask.FromResult(new FunctionResult
                {
                    Name = "lookup",
                    Content = "filled",
                    InvocationSucceeded = true,
                    RemoteContent = new McpContent { McpContentBlocks = blocks.ToList() }
                })
            }
        };

        TornadoAgent agent = new TornadoAgent(new TornadoApi(LLmProviders.OpenAi, "key"), ChatModel.OpenAi.Gpt4.O);
        agent.McpTools.Add("lookup", tool);

        FunctionCall call = new FunctionCall
        {
            Name = "lookup",
            Arguments = "{}",
            Tool = tool
        };

        return await ToolRunner.CallMcpToolAsync(agent, call);
    }
}
