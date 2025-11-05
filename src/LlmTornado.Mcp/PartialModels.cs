namespace LlmTornado.Mcp;

/// <summary>
/// Configuration options for the Smart Filesystem MCP server
/// </summary>
public class FileSystemAgenticOptions
{
    /// <summary>
    /// Lines per page when reading files (default: 500)
    /// </summary>
    public int LinesPerPage { get; set; } = 500;
    
    /// <summary>
    /// Search results per page (default: 100)
    /// </summary>
    public int MaxSearchResults { get; set; } = 100;
    
    /// <summary>
    /// Make the filesystem mount read-only (default: true, recommended for security)
    /// </summary>
    public bool ReadOnly { get; set; } = true;
}