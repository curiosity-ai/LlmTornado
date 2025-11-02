using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using LlmTornado.Infra;

namespace LlmTornado.Agents;

/// <summary>
/// Class to Invoke the tools during run
/// </summary>
public static class ToolRunner
{
    /// <summary>
    /// Invoke function from FunctionCallItem and return FunctionOutputItem
    /// </summary>
    /// <param name="agent"></param>
    /// <param name="call"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<FunctionResult> CallFuncToolAsync(TornadoAgent agent, FunctionCall call)
    {
        if (!agent.ToolList.TryGetValue(call.Name, out Tool? tool))
        {
            throw new Exception($"I don't have a tool called {call.Name}");
        }
        
        if (tool?.Delegate is not null)
        {
            MethodInvocationResult invocationResult = await call.Invoke(call.Arguments ?? "{}").ConfigureAwait(false);
            return call.Result ?? (invocationResult.InvocationSuccessful
                ? new FunctionResult(call, new
                {
                    result = "ok"
                })
                : new FunctionResult(call, new
                {
                    error = invocationResult.InvocationException?.Message,
                }, false));
        }

        return new FunctionResult(call, "Error No Delegate found");
    }

    /// <summary>
    /// Calls the MCP tool and returns the result
    /// </summary>
    /// <param name="agent">The agent invoking the tool</param>
    /// <param name="call">The function call containing the arguments</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    /// <exception cref="System.Text.Json.JsonException"></exception>
    public static async Task<FunctionResult> CallMcpToolAsync(TornadoAgent agent, FunctionCall call)
    {

        if (!agent.McpTools.TryGetValue(call.Name, out Tool? tool))
            throw new Exception($"I don't have a tool called {call.Name}");

        Dictionary<string, object?>? dict = null;

        //Need to check if function has required parameters and if so, parse them from the call.FunctionArguments
        if (call.Arguments != null)
        {
            if (!JsonUtility.IsValidJson(call.Arguments))
                throw new System.Text.Json.JsonException($"Function arguments for {call.Name} are not valid JSON");
            dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(call.Arguments);
        }

        // call the tool on MCP server, pass args
        await call.ResolveRemote(dict);

        // extract tool result and pass it back to the model
        if (call.Result?.RemoteContent is McpContent mcpContent)
        {
            foreach (IMcpContentBlock block in mcpContent.McpContentBlocks)
            {
                if (block is McpContentBlockText textBlock)
                {
                    call.Result.Content = textBlock.Text;
                }
            }
        }

        return call.Result;
    }

   
}