using System;

namespace LlmTornado.Internal.Press.Publisher;

/// <summary>
/// Central registry of all available LinkedIn post strategies with random selection
/// </summary>
public static class LinkedInStrategyLibrary
{
    private static readonly Random _random = Random.Shared;
    
    /// <summary>
    /// All available LinkedIn post strategies
    /// </summary>
    public static readonly LinkedInPostStrategy[] AllStrategies = 
    [
        new StorytellingStrategy(),
        new DataDrivenStrategy(),
        new QuestionBasedStrategy(),
        new ListBasedStrategy(),
        new ContrarianStrategy(),
        new TutorialStrategy(),
        new InsightStrategy()
    ];
    
    /// <summary>
    /// Randomly select a strategy from the library
    /// </summary>
    public static LinkedInPostStrategy GetRandomStrategy()
    {
        int index = _random.Next(AllStrategies.Length);
        LinkedInPostStrategy selected = AllStrategies[index];
        Console.WriteLine($"[LinkedIn] 🎭 Strategy: {selected.Name} - {selected.Description}");
        return selected;
    }
    
    /// <summary>
    /// Get a specific strategy by name
    /// </summary>
    public static LinkedInPostStrategy? GetStrategyByName(string name)
    {
        foreach (LinkedInPostStrategy strategy in AllStrategies)
        {
            if (strategy.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return strategy;
            }
        }
        return null;
    }
}

