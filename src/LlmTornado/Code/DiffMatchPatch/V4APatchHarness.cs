using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Responses;

namespace LlmTornado.Code.DiffMatchPatch;

/// <summary>
/// Harness for applying OpenAI V4A (headerless) patch format operations.
/// V4A diffs use `@@` markers without line numbers and rely on context matching.
/// </summary>
/// <remarks>
/// V4A Format Structure:
/// <code>
/// @@
/// -old line
/// +new line
///  context line
/// @@
/// -another old line
/// +another new line
/// </code>
/// 
/// Or with context anchors:
/// <code>
/// @@ def fibonacci(n):
/// -    return fib(n-1) + fib(n-2)
/// +    return fibonacci(n-1) + fibonacci(n-2)
/// </code>
/// 
/// The V4A format is headerless - it uses `@@` without line numbers, making it
/// more resilient to LLM hallucinations but relying on fuzzy context matching.
/// </remarks>
public static class V4APatchHarness
{
    /// <summary>
    /// Apply a V4A patch operation to an in-memory workspace.
    /// </summary>
    /// <param name="operation">The patch operation from GPT-5.1</param>
    /// <param name="workspace">In-memory file workspace (path -> content)</param>
    /// <param name="result">Success/failure message for the model</param>
    /// <returns>True if the operation succeeded, false otherwise</returns>
    public static bool TryApplyOperation(
        ResponseApplyPatchOperation operation,
        IDictionary<string, string> workspace,
        out string result)
    {
        if (operation is null || string.IsNullOrWhiteSpace(operation.Path))
        {
            result = "Error: No operation or path provided.";
            return false;
        }

        return operation.Type switch
        {
            ResponseApplyPatchOperationType.CreateFile => TryCreateFile(operation, workspace, out result),
            ResponseApplyPatchOperationType.UpdateFile => TryUpdateFile(operation, workspace, out result),
            ResponseApplyPatchOperationType.DeleteFile => TryDeleteFile(operation, workspace, out result),
            _ => HandleUnknownOperationType(operation.Type, out result)
        };
    }

    /// <summary>
    /// Apply a V4A patch operation to the file system.
    /// </summary>
    /// <param name="operation">The patch operation from GPT-5.1</param>
    /// <param name="basePath">Base directory for file operations</param>
    /// <param name="result">Success/failure message for the model</param>
    /// <param name="allowedPaths">Optional list of allowed path prefixes for security</param>
    /// <returns>True if the operation succeeded, false otherwise</returns>
    public static bool TryApplyOperationToFileSystem(
        ResponseApplyPatchOperation operation,
        string basePath,
        out string result,
        IEnumerable<string>? allowedPaths = null)
    {
        if (operation is null || string.IsNullOrWhiteSpace(operation.Path))
        {
            result = "Error: No operation or path provided.";
            return false;
        }

        // Security: Validate path
        if (!IsPathSafe(operation.Path, basePath, allowedPaths))
        {
            result = $"Error: Path '{operation.Path}' is not allowed or contains directory traversal.";
            return false;
        }

        string fullPath = Path.Combine(basePath, operation.Path);

        return operation.Type switch
        {
            ResponseApplyPatchOperationType.CreateFile => TryCreateFileOnDisk(operation, fullPath, out result),
            ResponseApplyPatchOperationType.UpdateFile => TryUpdateFileOnDisk(operation, fullPath, out result),
            ResponseApplyPatchOperationType.DeleteFile => TryDeleteFileOnDisk(fullPath, out result),
            _ => HandleUnknownOperationType(operation.Type, out result)
        };
    }

    private static bool TryCreateFile(
        ResponseApplyPatchOperation operation,
        IDictionary<string, string> workspace,
        out string result)
    {
        if (string.IsNullOrEmpty(operation.Diff))
        {
            result = $"Error: No diff provided for create_file operation on '{operation.Path}'.";
            return false;
        }

        if (workspace.ContainsKey(operation.Path))
        {
            result = $"Error: File '{operation.Path}' already exists.";
            return false;
        }

        bool success = DiffPatchEngine.TryApply(
            string.Empty,
            operation.Diff,
            out string createdContent,
            out string? error,
            format: PatchFormat.V4a);

        if (!success)
        {
            result = $"Error: Failed to create '{operation.Path}': {error}";
            return false;
        }

        workspace[operation.Path] = createdContent;
        result = $"Created {operation.Path} ({createdContent.Length} bytes)";
        return true;
    }

    private static bool TryUpdateFile(
        ResponseApplyPatchOperation operation,
        IDictionary<string, string> workspace,
        out string result)
    {
        if (!workspace.TryGetValue(operation.Path, out string? existingContent))
        {
            result = $"Error: File not found at path '{operation.Path}'";
            return false;
        }

        if (string.IsNullOrEmpty(operation.Diff))
        {
            result = $"Error: No diff provided for update_file operation on '{operation.Path}'.";
            return false;
        }

        bool success = DiffPatchEngine.TryApply(
            existingContent,
            operation.Diff,
            out string newContent,
            out string? error,
            format: PatchFormat.V4a);

        if (!success)
        {
            result = $"Error: Failed to apply diff to '{operation.Path}': {error}";
            return false;
        }

        int sizeDelta = newContent.Length - existingContent.Length;
        workspace[operation.Path] = newContent;
        result = $"Updated {operation.Path} ({existingContent.Length} → {newContent.Length} bytes, {sizeDelta:+#;-#;0})";
        return true;
    }

    private static bool TryDeleteFile(
        ResponseApplyPatchOperation operation,
        IDictionary<string, string> workspace,
        out string result)
    {
        bool removed = workspace.Remove(operation.Path);
        result = removed
            ? $"Deleted {operation.Path}"
            : $"Error: File '{operation.Path}' did not exist.";
        return removed;
    }

    private static bool TryCreateFileOnDisk(
        ResponseApplyPatchOperation operation,
        string fullPath,
        out string result)
    {
        if (string.IsNullOrEmpty(operation.Diff))
        {
            result = $"Error: No diff provided for create_file operation on '{operation.Path}'.";
            return false;
        }

        if (File.Exists(fullPath))
        {
            result = $"Error: File '{operation.Path}' already exists.";
            return false;
        }

        bool success = DiffPatchEngine.TryApply(
            string.Empty,
            operation.Diff,
            out string createdContent,
            out string? error,
            format: PatchFormat.V4a);

        if (!success)
        {
            result = $"Error: Failed to create '{operation.Path}': {error}";
            return false;
        }

        try
        {
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, createdContent);
            result = $"Created {operation.Path} ({createdContent.Length} bytes)";
            return true;
        }
        catch (Exception ex)
        {
            result = $"Error: Failed to write file '{operation.Path}': {ex.Message}";
            return false;
        }
    }

    private static bool TryUpdateFileOnDisk(
        ResponseApplyPatchOperation operation,
        string fullPath,
        out string result)
    {
        if (!File.Exists(fullPath))
        {
            result = $"Error: File not found at path '{operation.Path}'";
            return false;
        }

        if (string.IsNullOrEmpty(operation.Diff))
        {
            result = $"Error: No diff provided for update_file operation on '{operation.Path}'.";
            return false;
        }

        try
        {
            string existingContent = File.ReadAllText(fullPath);

            bool success = DiffPatchEngine.TryApply(
                existingContent,
                operation.Diff,
                out string newContent,
                out string? error,
                format: PatchFormat.V4a);

            if (!success)
            {
                result = $"Error: Failed to apply diff to '{operation.Path}': {error}";
                return false;
            }

            File.WriteAllText(fullPath, newContent);
            int sizeDelta = newContent.Length - existingContent.Length;
            result = $"Updated {operation.Path} ({existingContent.Length} → {newContent.Length} bytes, {sizeDelta:+#;-#;0})";
            return true;
        }
        catch (Exception ex)
        {
            result = $"Error: Failed to update file '{operation.Path}': {ex.Message}";
            return false;
        }
    }

    private static bool TryDeleteFileOnDisk(string fullPath, out string result)
    {
        try
        {
            if (!File.Exists(fullPath))
            {
                result = $"Error: File did not exist.";
                return false;
            }

            File.Delete(fullPath);
            result = $"Deleted file";
            return true;
        }
        catch (Exception ex)
        {
            result = $"Error: Failed to delete file: {ex.Message}";
            return false;
        }
    }

    private static bool HandleUnknownOperationType(ResponseApplyPatchOperationType type, out string result)
    {
        result = $"Error: Unsupported operation type '{type}'.";
        return false;
    }

    private static bool IsPathSafe(string relativePath, string basePath, IEnumerable<string>? allowedPaths)
    {
        // Prevent directory traversal
        if (relativePath.Contains("..") || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        // Check allowed paths if specified
        if (allowedPaths is not null)
        {
            bool isAllowed = allowedPaths.Any(allowed =>
                relativePath.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Process a batch of apply_patch operations and create response items.
    /// </summary>
    /// <param name="patchCalls">List of apply_patch_call items from GPT-5.1</param>
    /// <param name="workspace">In-memory file workspace</param>
    /// <returns>List of apply_patch_call_output items to send back to the model</returns>
    public static List<ApplyPatchCallOutput> ProcessPatchBatch(
        IEnumerable<ResponseApplyPatchToolCallItem> patchCalls,
        IDictionary<string, string> workspace)
    {
        List<ApplyPatchCallOutput> outputs = [];

        foreach (ResponseApplyPatchToolCallItem call in patchCalls)
        {
            bool success = TryApplyOperation(call.Operation, workspace, out string result);

            outputs.Add(new ApplyPatchCallOutput
            {
                CallId = call.CallId ?? call.Id ?? Guid.NewGuid().ToString(),
                Status = success
                    ? ResponseApplyPatchCallOutputStatus.Completed
                    : ResponseApplyPatchCallOutputStatus.Failed,
                Output = result
            });
        }

        return outputs;
    }

    /// <summary>
    /// Process a batch of apply_patch operations on the file system.
    /// </summary>
    /// <param name="patchCalls">List of apply_patch_call items from GPT-5.1</param>
    /// <param name="basePath">Base directory for file operations</param>
    /// <param name="allowedPaths">Optional list of allowed path prefixes</param>
    /// <returns>List of apply_patch_call_output items to send back to the model</returns>
    public static List<ApplyPatchCallOutput> ProcessPatchBatchOnFileSystem(
        IEnumerable<ResponseApplyPatchToolCallItem> patchCalls,
        string basePath,
        IEnumerable<string>? allowedPaths = null)
    {
        List<ApplyPatchCallOutput> outputs = [];

        foreach (ResponseApplyPatchToolCallItem call in patchCalls)
        {
            bool success = TryApplyOperationToFileSystem(
                call.Operation,
                basePath,
                out string result,
                allowedPaths);

            outputs.Add(new ApplyPatchCallOutput
            {
                CallId = call.CallId ?? call.Id ?? Guid.NewGuid().ToString(),
                Status = success
                    ? ResponseApplyPatchCallOutputStatus.Completed
                    : ResponseApplyPatchCallOutputStatus.Failed,
                Output = result
            });
        }

        return outputs;
    }

    #region Async File System Operations

    /// <summary>
    /// Apply a V4A patch operation to the file system asynchronously.
    /// </summary>
    /// <param name="operation">The patch operation from GPT-5.1</param>
    /// <param name="basePath">Base directory for file operations</param>
    /// <param name="allowedPaths">Optional list of allowed path prefixes for security</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result tuple (success, message)</returns>
    public static async Task<(bool success, string result)> TryApplyOperationToFileSystemAsync(
        ResponseApplyPatchOperation operation,
        string basePath,
        IEnumerable<string>? allowedPaths = null,
        CancellationToken cancellationToken = default)
    {
        if (operation is null || string.IsNullOrWhiteSpace(operation.Path))
        {
            return (false, "Error: No operation or path provided.");
        }

        // Security: Validate path
        if (!IsPathSafe(operation.Path, basePath, allowedPaths))
        {
            return (false, $"Error: Path '{operation.Path}' is not allowed or contains directory traversal.");
        }

        string fullPath = Path.Combine(basePath, operation.Path);

        return operation.Type switch
        {
            ResponseApplyPatchOperationType.CreateFile => await TryCreateFileOnDiskAsync(operation, fullPath, cancellationToken),
            ResponseApplyPatchOperationType.UpdateFile => await TryUpdateFileOnDiskAsync(operation, fullPath, cancellationToken),
            ResponseApplyPatchOperationType.DeleteFile => await TryDeleteFileOnDiskAsync(fullPath, cancellationToken),
            _ => (false, $"Error: Unsupported operation type '{operation.Type}'.")
        };
    }

    private static async Task<(bool success, string result)> TryCreateFileOnDiskAsync(
        ResponseApplyPatchOperation operation,
        string fullPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(operation.Diff))
        {
            return (false, $"Error: No diff provided for create_file operation on '{operation.Path}'.");
        }

        if (File.Exists(fullPath))
        {
            return (false, $"Error: File '{operation.Path}' already exists.");
        }

        bool success = DiffPatchEngine.TryApply(
            string.Empty,
            operation.Diff,
            out string createdContent,
            out string? error,
            PatchFormat.V4a);

        if (!success)
        {
            return (false, $"Error: Failed to create '{operation.Path}': {error}");
        }

        try
        {
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, createdContent, cancellationToken);
            return (true, $"Created {operation.Path} ({createdContent.Length} bytes)");
        }
        catch (OperationCanceledException)
        {
            return (false, $"Error: Operation cancelled while writing '{operation.Path}'.");
        }
        catch (Exception ex)
        {
            return (false, $"Error: Failed to write file '{operation.Path}': {ex.Message}");
        }
    }

    private static async Task<(bool success, string result)> TryUpdateFileOnDiskAsync(
        ResponseApplyPatchOperation operation,
        string fullPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath))
        {
            return (false, $"Error: File not found at path '{operation.Path}'");
        }

        if (string.IsNullOrEmpty(operation.Diff))
        {
            return (false, $"Error: No diff provided for update_file operation on '{operation.Path}'.");
        }

        try
        {
            string existingContent = await File.ReadAllTextAsync(fullPath, cancellationToken);

            bool success = DiffPatchEngine.TryApply(
                existingContent,
                operation.Diff,
                out string newContent,
                out string? error,
                PatchFormat.V4a);

            if (!success)
            {
                return (false, $"Error: Failed to apply diff to '{operation.Path}': {error}");
            }

            await File.WriteAllTextAsync(fullPath, newContent, cancellationToken);
            int sizeDelta = newContent.Length - existingContent.Length;
            return (true, $"Updated {operation.Path} ({existingContent.Length} → {newContent.Length} bytes, {sizeDelta:+#;-#;0})");
        }
        catch (OperationCanceledException)
        {
            return (false, $"Error: Operation cancelled while updating '{operation.Path}'.");
        }
        catch (Exception ex)
        {
            return (false, $"Error: Failed to update file '{operation.Path}': {ex.Message}");
        }
    }

    private static async Task<(bool success, string result)> TryDeleteFileOnDiskAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(fullPath))
            {
                return (false, "Error: File did not exist.");
            }

            await Task.Run(() => File.Delete(fullPath), cancellationToken);
            return (true, "Deleted file");
        }
        catch (OperationCanceledException)
        {
            return (false, "Error: Operation cancelled during file deletion.");
        }
        catch (Exception ex)
        {
            return (false, $"Error: Failed to delete file: {ex.Message}");
        }
    }

    /// <summary>
    /// Process a batch of apply_patch operations on the file system asynchronously.
    /// </summary>
    /// <param name="patchCalls">List of apply_patch_call items from GPT-5.1</param>
    /// <param name="basePath">Base directory for file operations</param>
    /// <param name="allowedPaths">Optional list of allowed path prefixes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of apply_patch_call_output items to send back to the model</returns>
    public static async Task<List<ApplyPatchCallOutput>> ProcessPatchBatchOnFileSystemAsync(
        IEnumerable<ResponseApplyPatchToolCallItem> patchCalls,
        string basePath,
        IEnumerable<string>? allowedPaths = null,
        CancellationToken cancellationToken = default)
    {
        List<ApplyPatchCallOutput> outputs = [];

        foreach (ResponseApplyPatchToolCallItem call in patchCalls)
        {
            (bool success, string result) = await TryApplyOperationToFileSystemAsync(
                call.Operation,
                basePath,
                allowedPaths,
                cancellationToken);

            outputs.Add(new ApplyPatchCallOutput
            {
                CallId = call.CallId ?? call.Id ?? Guid.NewGuid().ToString(),
                Status = success
                    ? ResponseApplyPatchCallOutputStatus.Completed
                    : ResponseApplyPatchCallOutputStatus.Failed,
                Output = result
            });
        }

        return outputs;
    }

    #endregion
}

