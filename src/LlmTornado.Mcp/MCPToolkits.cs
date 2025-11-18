using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Mcp;

/// <summary>
/// Built-in MCP toolkits.
/// </summary>
public static class MCPToolkits
{
    public static MCPServer Puppeteer(string[]? allowedTools = null)
    {
        MCPServer server = new MCPServer("puppeteer", command: "docker", arguments:
            [
                "run",
                "-i",
                "--rm",
                "--init",
                "-e",
                "DOCKER_CONTAINER=true",
                "mcp/puppeteer"
            ],
            allowedTools: allowedTools);
        return server;
    }

    public static MCPServer Github(string githubApiKey, string[]? allowedTools = null)
    {
        return new MCPServer("github", "https://api.githubcopilot.com/mcp", additionalConnectionHeaders: new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {githubApiKey}" }
        },
        allowedTools:allowedTools);
    }
    
    /// <summary>
    /// Meme generator using MCP Server Docker image. A <see cref="McpMemeCls"/> is available for this MCP Server with helper methods.
    /// </summary>
    public static MCPServer Meme(string[]? allowedTools = null)
    {
        MCPServer server = new MCPServer("memegen", command: "docker", arguments:
            [
                "run",
                "-i",
                "--rm",
                "-p",
                "5000:5000",
                "lofcz1/memegen-mcp"
            ],
            allowedTools: allowedTools);
        return server;
    }

    /// <summary>
    /// File System Toolkit using MCP Server Docker image
    /// </summary>
    public static MCPServer FileSystem(string workspaceFolder, string[]? allowedTools = null)
    {
        MCPServer server = new MCPServer("filesystem", command: "docker", arguments:
            [
                "run",
                "-i",
                "--rm",
                "--mount", $"type=bind,src={workspaceFolder},dst=/projects/workspace",
                "mcp/filesystem",
                "/projects/workspace"
            ],
            allowedTools: allowedTools);
        return server;
    }
    
    /// <summary>
    /// Smart File System Toolkit with intelligent pagination and ripgrep integration
    /// </summary>
    /// <param name="workspaceFolder">Local folder to mount into the container</param>
    /// <param name="options">Configuration options (pagination, search limits, etc.)</param>
    /// <param name="allowedTools">Optional: restrict to specific tools (e.g., ["search_code", "read_file"])</param>
    public static MCPServer FileSystemAgentic(
        string workspaceFolder, 
        FileSystemAgenticOptions? options = null,
        string[]? allowedTools = null)
    {
        options ??= new FileSystemAgenticOptions();
        
        List<string> args =
        [
            "run",
            "-i",
            "--rm",
            "-e", $"MCP_LINES_PER_PAGE={options.LinesPerPage}",
            "-e", $"MCP_MAX_SEARCH_RESULTS={options.MaxSearchResults}",
            "--mount", $"type=bind,src={workspaceFolder},dst=/workspace{(options.ReadOnly ? ",readonly" : "")}",
            "lofcz1/mcp-filesystem-smart",
            "/workspace"
        ];

        MCPServer server = new MCPServer(
            "filesystem-smart", 
            command: "docker", 
            arguments: args.ToArray(),
            allowedTools: allowedTools
        );
    
        return server;
    }

    /// <summary>
    /// Gmail toolkit based on @gongrzhe/server-gmail-autoauth-mcp.
    /// </summary>
    public static MCPServer Gmail(string[]? allowedTools = null)
    {
        MCPServer server = new MCPServer("gmail", command: "npx", arguments:
            [
                "@gongrzhe/server-gmail-autoauth-mcp"
            ],
            allowedTools: allowedTools);
        return server;
    }

    /// <summary>
    /// Playwright for web interactions.
    /// </summary>
    public static MCPServer Playwright(string[]? allowedTools = null)
    {
        MCPServer server = new MCPServer("playwright", command: "npx", arguments:
            [
                "@playwright/mcp@latest"
            ],
            allowedTools: allowedTools);
        return server;
    }

    /// <summary>
    /// Fetch MCP server.
    /// </summary>
    public static MCPServer Fetch(string[]? allowedTools = null)
    {
        MCPServer server = new MCPServer("fetch", command: "uvx", arguments:
            [
                "mcp-server-fetch"
            ],
            allowedTools: allowedTools);
        return server;
    }


    /// <summary>
    /// Fetch MCP server.
    /// </summary>
    public static MCPServer BasicMemory(string[]? allowedTools = null)
    {
        MCPServer server = new MCPServer("basic-memory", command: "uvx", arguments:
            [
                "basic-memory", "mcp"
            ],
            allowedTools: allowedTools);
        return server;
    }

}
