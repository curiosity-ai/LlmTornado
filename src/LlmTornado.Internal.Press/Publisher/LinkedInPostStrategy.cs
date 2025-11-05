namespace LlmTornado.Internal.Press.Publisher;

/// <summary>
/// Base class for different LinkedIn post writing strategies
/// </summary>
public abstract class LinkedInPostStrategy
{
    /// <summary>
    /// Name of the strategy
    /// </summary>
    public abstract string Name { get; }
    
    /// <summary>
    /// Brief description of the strategy
    /// </summary>
    public abstract string Description { get; }
    
    /// <summary>
    /// Get the strategy-specific instructions for the AI prompt
    /// </summary>
    public abstract string GetStrategyInstructions();
}

/// <summary>
/// Narrative approach with personal journey and storytelling
/// </summary>
public class StorytellingStrategy : LinkedInPostStrategy
{
    public override string Name => "Storytelling";
    
    public override string Description => "Narrative approach with personal journey";
    
    public override string GetStrategyInstructions()
    {
        return """
               **STORYTELLING STRATEGY:**
               - Open with a personal anecdote or scenario
               - Take readers on a journey (problem → discovery → solution)
               - Use "I", "my", "we" to make it personal and relatable
               - Show vulnerability or learning moments
               - Build to a satisfying conclusion
               - Example opening: "Last week I faced a problem that completely changed how I think about..."
               """;
    }
}

/// <summary>
/// Stats, numbers, and metrics-focused approach
/// </summary>
public class DataDrivenStrategy : LinkedInPostStrategy
{
    public override string Name => "Data-Driven";
    
    public override string Description => "Stats, numbers, and metrics-focused";
    
    public override string GetStrategyInstructions()
    {
        return """
               **DATA-DRIVEN STRATEGY:**
               - Lead with a compelling statistic or benchmark
               - Use concrete numbers to support every claim
               - Include comparisons (X is 3x faster than Y)
               - Reference studies, tests, or measurements
               - Make it quantifiable and objective
               - Example opening: "After testing 10 different approaches, the results were surprising..."
               """;
    }
}

/// <summary>
/// Provocative questions to spark discussion
/// </summary>
public class QuestionBasedStrategy : LinkedInPostStrategy
{
    public override string Name => "Question-Based";
    
    public override string Description => "Provocative questions to spark discussion";
    
    public override string GetStrategyInstructions()
    {
        return """
               **QUESTION-BASED STRATEGY:**
               - Start with a thought-provoking question
               - Challenge assumptions with "What if...?" or "Why do we...?"
               - Use questions throughout to maintain engagement
               - Make readers think and reflect
               - End with a question to drive comments
               - Example opening: "Why do we still accept X when Y is clearly better?"
               """;
    }
}

/// <summary>
/// Listicle format with numbered insights
/// </summary>
public class ListBasedStrategy : LinkedInPostStrategy
{
    public override string Name => "List-Based";
    
    public override string Description => "Numbered list format (3-5 things I learned)";
    
    public override string GetStrategyInstructions()
    {
        return """
               **LIST-BASED STRATEGY:**
               - Use "3 things", "5 lessons", "7 insights" format
               - Number each point clearly (1., 2., 3.)
               - Keep each item concise and punchy
               - Build momentum from point to point
               - Make the list scannable and easy to digest
               - Example opening: "3 things I wish I knew before building..."
               """;
    }
}

/// <summary>
/// Challenge conventional wisdom with contrarian take
/// </summary>
public class ContrarianStrategy : LinkedInPostStrategy
{
    public override string Name => "Contrarian";
    
    public override string Description => "Challenge conventional wisdom";
    
    public override string GetStrategyInstructions()
    {
        return """
               **CONTRARIAN STRATEGY:**
               - Challenge the status quo or popular opinion
               - Use "Unpopular opinion:", "Hot take:", or "Controversial but..."
               - Present an alternative perspective
               - Back up the contrarian view with reasoning
               - Be bold but not abrasive
               - Example opening: "Everyone says X is the future. I disagree. Here's why..."
               """;
    }
}

/// <summary>
/// Quick actionable tips and how-to guidance
/// </summary>
public class TutorialStrategy : LinkedInPostStrategy
{
    public override string Name => "Tutorial";
    
    public override string Description => "Quick actionable tips and how-to";
    
    public override string GetStrategyInstructions()
    {
        return """
               **TUTORIAL STRATEGY:**
               - Promise immediate value ("How to...", "Quick way to...")
               - Provide step-by-step or clear action items
               - Focus on practical, applicable knowledge
               - Use imperative language ("Start by...", "Next, try...")
               - Make it feel like a quick win
               - Example opening: "Here's how to X in under 5 minutes..."
               """;
    }
}

/// <summary>
/// One powerful insight expanded and explored
/// </summary>
public class InsightStrategy : LinkedInPostStrategy
{
    public override string Name => "Insight";
    
    public override string Description => "One powerful insight expanded deeply";
    
    public override string GetStrategyInstructions()
    {
        return """
               **INSIGHT STRATEGY:**
               - Share one profound realization or discovery
               - Go deep rather than broad
               - Connect the insight to broader implications
               - Use "I realized...", "It hit me that...", "The key insight is..."
               - Make it feel like an "aha" moment
               - Example opening: "I finally understood why X matters when I discovered..."
               """;
    }
}

