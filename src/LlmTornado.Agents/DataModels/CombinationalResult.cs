using System;
using System.Collections.Generic;
using System.Linq;

namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Represents a result that combines multiple values from different runnables.
/// Used for combinational advancement patterns in orchestration.
/// </summary>
/// <typeparam name="T">The type of values being combined.</typeparam>
public class CombinationalResult<T>
{
    /// <summary>
    /// Gets or sets the combined values from multiple runnables.
    /// </summary>
    public T[] Values { get; set; } = Array.Empty<T>();

    /// <summary>
    /// Gets the values as a list.
    /// </summary>
    public List<T> ValuesList => Values?.ToList() ?? new List<T>();
}

