using LlmTornado.Agents;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Internal.Press.Configuration;
using LlmTornado.Internal.Press.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LlmTornado.Internal.Press.Agents;

public class IdeationRunnable : OrchestrationRunnable<TrendAnalysisOutput, ArticleIdeaOutput>
{
    private readonly TornadoAgent _agent;
    private readonly AppConfiguration _config;
    private static readonly Random _random = new Random();

    // Article angle categories for balanced selection
    public enum AngleCategory
    {
        Educational,
        Entertaining,
        Comparison,
        Tutorial,
        Story
    }

    // Article angle with category
    private class ArticleAngle
    {
        public string Hint { get; set; } = string.Empty;
        public AngleCategory Category { get; set; }
    }

    // Diverse article angle hints categorized by type
    private static readonly ArticleAngle[] ArticleAngles =
    [
        // Educational angles
        new ArticleAngle { Hint = "🔍 **Tool Comparison**: Compare 3-5 popular tools/libraries in this space, analyzing pros/cons objectively", Category = AngleCategory.Comparison },
        new ArticleAngle { Hint = "📊 **State of the Industry**: \"State of X in {year}\" - survey current landscape, major players, and future direction", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "⚡ **Performance Benchmarks**: Compare performance/speed/efficiency of different approaches with real metrics", Category = AngleCategory.Comparison },
        new ArticleAngle { Hint = "🎯 **Decision Guide**: \"When to Use X vs Y\" - help readers make informed technology choices", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "🔨 **Hands-On Tutorial**: Step-by-step guide building something practical from scratch", Category = AngleCategory.Tutorial },
        new ArticleAngle { Hint = "⚠️ **Common Pitfalls**: \"X Mistakes Developers Make with Y\" - learn from others' errors", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "🏗️ **Architecture Patterns**: Explore design patterns, best practices, and anti-patterns", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "💡 **Problem-Solution**: Start with a painful problem, explore multiple solutions", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "📈 **Migration Guide**: \"Moving from X to Y\" - help developers transition between technologies", Category = AngleCategory.Tutorial },
        new ArticleAngle { Hint = "🔬 **Deep Technical Dive**: Explain how something works under the hood", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "🎓 **Learning Path**: \"From Zero to Hero\" - comprehensive learning journey", Category = AngleCategory.Tutorial },
        new ArticleAngle { Hint = "🆚 **Technology Debate**: Present both sides of a controversial technical decision", Category = AngleCategory.Comparison },
        new ArticleAngle { Hint = "🚀 **Productivity Tips**: \"10 Ways to Speed Up Your X Workflow\"", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "🔐 **Security Focus**: Security considerations, best practices, and common vulnerabilities", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "💰 **Cost Analysis**: Compare costs, pricing models, and ROI of different solutions", Category = AngleCategory.Comparison },
        new ArticleAngle { Hint = "🌍 **Real-World Case Study**: How Company X solved Problem Y (can be anonymized)", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "🔄 **Evolution Story**: \"How X Has Changed\" - historical perspective on technology evolution", Category = AngleCategory.Story },
        new ArticleAngle { Hint = "🎪 **Behind the Scenes**: How popular frameworks/tools actually work internally", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "📱 **Platform-Specific**: Focus on specific platform (mobile, web, desktop, cloud)", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "🧪 **Experimental**: \"I Tested X for 30 Days\" - personal experiment with insights", Category = AngleCategory.Story },
        
        // Entertaining/story-driven angles inspired by WritingAgent personas
        new ArticleAngle { Hint = "📖 **Personal Narrative**: \"I Built X and Here's What Happened\" - share your journey building something real", Category = AngleCategory.Story },
        new ArticleAngle { Hint = "💥 **Failure Story**: \"How I Broke Production and Learned X\" - war stories with lessons learned", Category = AngleCategory.Entertaining },
        new ArticleAngle { Hint = "🗺️ **Journey Story**: \"From Zero to Production: My 6-Month Journey Building X\" - complete development journey", Category = AngleCategory.Story },
        new ArticleAngle { Hint = "🎭 **Behind-the-Scenes Narrative**: \"What Really Happens When You Deploy X\" - candid look at real-world deployment", Category = AngleCategory.Entertaining },
        new ArticleAngle { Hint = "🎖️ **Battle-Tested Lessons**: \"After 15 Years, Here's What I Learned About X\" - veteran developer wisdom", Category = AngleCategory.Story },
        new ArticleAngle { Hint = "💡 **Casual Discovery**: \"TIL: How X Actually Works\" - personal discovery shared informally", Category = AngleCategory.Entertaining },
        new ArticleAngle { Hint = "🐛 **The Bug That Taught Me Everything**: Deep dive into a production bug and what you learned", Category = AngleCategory.Entertaining },
        new ArticleAngle { Hint = "🌙 **2 AM Debugging Story**: \"The Night I Spent Debugging X and What I Discovered\" - relatable debugging tales", Category = AngleCategory.Entertaining },
        new ArticleAngle { Hint = "🎬 **The Making Of**: \"How I Built X: A Behind-the-Scenes Look\" - narrative of building process", Category = AngleCategory.Story },
        new ArticleAngle { Hint = "🤯 **Mind-Blowing Discovery**: \"I Thought X Worked Like Y, But I Was Completely Wrong\" - surprising revelations", Category = AngleCategory.Entertaining },
        
        // Creative formats inspired by WritingAgent WritingStyleHints
        new ArticleAngle { Hint = "📊 **Comparison Table Format**: Create detailed comparison tables comparing multiple options/features side-by-side", Category = AngleCategory.Comparison },
        new ArticleAngle { Hint = "🌳 **Decision Tree Guide**: \"When to Use X vs Y\" with decision trees based on specific criteria and use cases", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "📈 **Performance Benchmark Article**: Include concrete numbers, benchmarks, timing comparisons with real metrics and data", Category = AngleCategory.Comparison },
        new ArticleAngle { Hint = "🔄 **Before/After Transformation**: Show dramatic improvement with side-by-side code comparisons and transformations", Category = AngleCategory.Tutorial },
        new ArticleAngle { Hint = "🧪 **Real-World Case Study**: Detailed case study showing practical application with real scenarios (can be anonymized)", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "🔧 **Troubleshooting Narrative**: \"How I Solved X Problem\" - walk through debugging process as a story", Category = AngleCategory.Entertaining },
        new ArticleAngle { Hint = "🎨 **Analogy-Driven Explanation**: Use relatable analogies and metaphors to explain complex technical concepts", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "📋 **Checklist Format**: \"Complete Checklist for X\" - actionable checklists for common tasks", Category = AngleCategory.Tutorial },
        new ArticleAngle { Hint = "🎯 **Decision Matrix**: Show decision matrix or flowchart when multiple options exist for different scenarios", Category = AngleCategory.Educational },
        new ArticleAngle { Hint = "📊 **Progressive Enhancement**: Show basic → intermediate → advanced variations of the same concept", Category = AngleCategory.Tutorial }
    ];

    public IdeationRunnable(
        TornadoApi client,
        AppConfiguration config,
        Orchestration orchestrator) : base(orchestrator)
    {
        _config = config;

        DateTime now = DateTime.Now;
        int currentYear = now.Year;
        string currentQuarter = $"Q{(now.Month - 1) / 3 + 1}";
        string currentMonth = now.ToString("MMMM");

        string instructions = $"""
                               You are an expert content strategist specializing in developer marketing through educational content.
                               Your goal is to create article ideas that provide GENUINE VALUE while subtly influencing decisions.

                               Current Date: {now:MMMM dd, yyyy} ({currentQuarter} {currentYear})
                               Background Context (Keep Private): {config.Objective}

                               STRATEGIC APPROACH - The Art of Subtle Influence:

                               1. **Problem-First Thinking**
                                  - Start with real problems developers face TODAY
                                  - Address pain points, frustrations, decision paralysis
                                  - Offer solutions (where our context naturally fits as ONE option)

                               2. **Trend Commentary & Analysis**
                                  - Analyze CURRENT industry shifts, new technologies, changing best practices
                                  - Compare approaches, frameworks, or patterns (tool comparisons are EXCELLENT)
                                  - Position context as a relevant example without forcing it
                                  - Use actual trending topics from research

                               3. **Educational Deep-Dives**
                                  - "How to...", "Understanding...", "Guide to..."
                                  - Technical tutorials, architecture patterns, best practices
                                  - Mention context where it genuinely adds value

                               4. **Temporal Relevance (USE CURRENT DATE)**
                                  - "Best X in {currentQuarter} {currentYear}", "X Trends in {currentYear}", "What's Changed in {currentYear}"
                                  - Month/quarter/year-specific: "{currentMonth} {currentYear}", "{currentQuarter} {currentYear}"
                                  - Current events, new releases, emerging patterns
                                  - "State of X in {currentYear}" surveys

                               TITLE GUIDELINES - DIVERSITY IS KEY:
                               - NEVER mention the promotional context directly in titles
                               - **CRITICAL: Mix title lengths for variety in each batch:**
                                 * Short/punchy (30-50 chars): "Why X Sucks", "The X Problem", "I Built X. Here's What Happened"
                                 * Medium (50-70 chars): "Building Production-Ready X: A Developer's Guide"
                                 * SEO-optimized longer (70-90 chars): "Complete Guide to X in C#: Best Practices, Patterns, and Real-World Examples"
                               - Use curiosity gaps: "Why...", "The Secret to...", "What Nobody Tells You..."
                               - Use specificity: numbers, timeframes, concrete outcomes
                               - Be contrarian when appropriate: "Why X is Overrated", "The Problem With..."
                               - Focus on reader benefit, not product features

                               EXAMPLES OF GOOD vs BAD TITLES:

                               ❌ BAD (Too Direct):
                               - "Why You Should Use [Product X]"
                               - "10 Reasons [Product X] is Great"
                               - "[Product X]: The Best Solution for Y"

                               ✅ GOOD (Subtle - SHORT, 30-50 chars):
                               - "Why X Sucks"
                               - "The X Problem"
                               - "I Built X. Here's What Happened"
                               - "TIL: How X Actually Works"
                               - "The Bug That Taught Me Everything"

                               ✅ GOOD (Subtle - MEDIUM, 50-70 chars):
                               - "Building Production-Ready AI Agents: A C# Developer's Journey"
                               - "The Hidden Complexity of LLM API Integration (And How to Tame It)"
                               - "What I Learned Managing 100B+ Tokens in a .NET Application"
                               - "5 Patterns for Reliable LLM Orchestration in Enterprise C#"
                               - "Why Most C# AI Tutorials Get Error Handling Wrong"

                               ✅ GOOD (Subtle - SEO-OPTIMIZED LONGER, 70-90 chars):
                               - "Complete Guide to X in C#: Best Practices, Patterns, and Real-World Examples"
                               - "Top 5 C# AI Libraries in Q1 2025: A Comprehensive Developer Comparison"
                               - "Understanding LLM Orchestration in .NET: Architecture Patterns, Best Practices, and Common Pitfalls"

                               CONTENT STRATEGY:
                               - Provide 90% genuine value, 10% subtle positioning
                               - Lead with problems, trends, or questions
                               - Compare multiple approaches (mention context as one)
                               - Use case studies, real-world scenarios, lessons learned
                               - Be honest about tradeoffs and limitations

                               STORYTELLING & ENTERTAINMENT GUIDELINES:
                               Inspired by successful developer content personas:

                               1. **Technical Storyteller Style** (Narratives, metaphors, journey structure):
                                  - "Picture this scenario...", "Imagine you're building..."
                                  - Use analogies to explain complex concepts
                                  - Structure like journeys with beginning, middle, end
                                  - Make abstract concepts concrete through examples
                                  - Example: "Think of AI agents like orchestra conductors..."

                               2. **Battle-Scarred Veteran Style** (War stories, lessons learned):
                                  - "After 15 years of debugging production issues..."
                                  - "I've made this mistake more times than I'd like to admit..."
                                  - Share war stories and lessons learned the hard way
                                  - Honest about failures and wrong turns
                                  - Example: "I've seen this pattern fail in production more times than I can count..."

                               3. **Casual Blogger Style** (Personal discoveries, informal tone):
                                  - "Here's something interesting I learned today..."
                                  - "I was working on X and discovered Y..."
                                  - Personal anecdotes and genuine curiosity
                                  - Humble about mistakes and learning process
                                  - Example: "TIL that you can actually do X in C#. Mind blown."

                               CREATIVE FORMATS TO CONSIDER:
                               - Comparison tables/articles with side-by-side analysis
                               - Decision trees/guides with criteria-based choices
                               - Performance benchmarks with real metrics and data
                               - Before/after transformations showing improvements
                               - Troubleshooting narratives walking through problem-solving
                               - Analogies and metaphors for complex concepts
                               - Case studies with real-world scenarios
                               - Progressive enhancement (basic → intermediate → advanced)

                               For each idea, provide:
                               - A subtle, value-driven title (NO direct product mentions)
                                 **IMPORTANT: Vary title lengths - include short, medium, and SEO-optimized longer titles**
                               - A summary focused on the problem/topic (not the solution)
                               - Relevance score based on trend fit and reader value
                               - Tags reflecting the actual topic, not promotional keywords
                               - Reasoning explaining the subtle influence strategy

                               Generate 3-5 article ideas that developers will genuinely want to read.
                               Ensure variety in title lengths and content types (educational, entertaining, comparison, tutorial, story).
                               """;

        ChatModel model = new ChatModel(config.Models.Ideation);

        _agent = new TornadoAgent(
            client: client,
            model: model,
            name: "Ideation Agent",
            instructions: instructions,
            outputSchema: typeof(ArticleIdeaOutput),
            options: new ChatRequest() { Temperature = 1 });
    }
    
    

    public override async ValueTask<ArticleIdeaOutput> Invoke(RunnableProcess<TrendAnalysisOutput, ArticleIdeaOutput> process)
    {
        return await Invoke(process, batchNumber: 1, totalBatches: 1, previouslyUsedCategories: []);
    }

    /// <summary>
    /// Invoke with batch context for diversity tracking
    /// </summary>
    public async ValueTask<ArticleIdeaOutput> Invoke(
        RunnableProcess<TrendAnalysisOutput, ArticleIdeaOutput> process,
        int batchNumber,
        int totalBatches,
        HashSet<AngleCategory> previouslyUsedCategories)
    {
        var (result, _) = await InvokeWithCategories(process, batchNumber, totalBatches, previouslyUsedCategories);
        return result;
    }

    /// <summary>
    /// Invoke with batch context for diversity tracking, returns categories used
    /// </summary>
    public async ValueTask<(ArticleIdeaOutput, HashSet<AngleCategory>)> InvokeWithCategories(
        RunnableProcess<TrendAnalysisOutput, ArticleIdeaOutput> process,
        int batchNumber,
        int totalBatches,
        HashSet<AngleCategory> previouslyUsedCategories)
    {
        process.RegisterAgent(_agent);

        // Build context from trends
        string trendsContext = BuildTrendsContext(process.Input);
        
        // Get balanced article angle hints based on batch context
        var (selectedAngles, usedCategories) = GetBalancedArticleAnglesWithCategories(count: 3, previouslyUsedCategories);
        string anglesText = "";
        
        if (selectedAngles.Length > 0)
        {
            Console.WriteLine($"  [IdeationAgent] 🎯 Selected {selectedAngles.Length} article angle suggestions:");
            foreach (string angle in selectedAngles)
            {
                string angleTitle = angle.Split(':')[0].Trim();
                Console.WriteLine($"    • {angleTitle}");
            }
            
            anglesText = "**🎯 ARTICLE ANGLE SUGGESTIONS (Consider these approaches):**\n\n";
            foreach (string angle in selectedAngles)
            {
                anglesText += $"{angle}\n\n";
            }
            anglesText += "---\n\n";
        }
        
        DateTime now = DateTime.Now;
        int currentYear = now.Year;
        string currentQuarter = $"Q{(now.Month - 1) / 3 + 1}";
        
        // Build batch context text
        string batchContextText = "";
        if (totalBatches > 1)
        {
            string previousBatchesText = previouslyUsedCategories.Count > 0
                ? $"Previous batches focused on: {string.Join(", ", previouslyUsedCategories.Select(c => c.ToString()))}. "
                : "";
            
            batchContextText = $"""
                                
                                **BATCH CONTEXT:**
                                This is batch {batchNumber} of {totalBatches} total batches.
                                {previousBatchesText}
                                Ensure this batch uses DIFFERENT angles and content types than previous batches.
                                Aim for variety: include 1-2 story-driven/entertaining ideas, mix educational and comparison content.
                                
                                """;
        }
        
        string prompt = $"""
                         {anglesText}**CURRENT CONTEXT:**
                         Today's Date: {now:MMMM dd, yyyy}
                         Current Period: {currentQuarter} {currentYear}
                         {batchContextText}
                         **TRENDING TOPICS TO LEVERAGE:**
                         {trendsContext}

                         **YOUR MISSION:**
                         Generate 5 article ideas that developers will GENUINELY want to read. These ideas should:

                         1. **Latch onto REAL trends** from the research above (not generic AI agent content)
                         2. **Use diverse angles** - comparisons, tutorials, problem-solving, industry analysis, stories, etc.
                         3. **Be temporally relevant** - reference current period ({currentQuarter} {currentYear}) when appropriate, but NOT all ideas need dates
                         4. **Provide educational value first** - teach, compare, analyze, guide, OR entertain with stories
                         5. **Subtly position context** - mention as ONE option among several, not the hero
                         6. **Use engaging titles** - NO direct promotional mentions, use curiosity/specificity/controversy
                         7. **VARY TITLE LENGTHS** - Include mix of short (30-50 chars), medium (50-70 chars), and SEO-optimized longer (70-90 chars) titles

                         **EXCELLENT ANGLE IDEAS (Mix of lengths and styles):**
                         
                         SHORT (30-50 chars):
                         - "Why X Sucks"
                         - "I Built X. Here's What Happened"
                         - "The Bug That Taught Me Everything"
                         
                         MEDIUM (50-70 chars):
                         - "I Spent 30 Days Testing LLM Orchestration Frameworks in .NET - Here's What I Found"
                         - "Why Your C# AI Integration is Probably Too Complex (And How to Simplify)"
                         - "From LangChain to Native .NET: A Migration Story"
                         
                         SEO-OPTIMIZED LONGER (70-90 chars):
                         - "Top 5 C# AI Libraries in {currentQuarter} {currentYear}: A Comprehensive Developer Comparison"
                         - "Complete Guide to LLM Orchestration in .NET: Best Practices, Patterns, and Real-World Examples"

                         Think like a respected developer writing for other developers - what would YOU genuinely want to read?
                         Be creative with angles, be current with dates (but not all), be diverse in approach and title lengths!
                         """;

        Conversation conversation = await _agent.Run(prompt);
        ChatMessage lastMessage = conversation.Messages.Last();
        ArticleIdeaOutput? ideaOutput = await lastMessage.Content?.SmartParseJsonAsync<ArticleIdeaOutput>(_agent);

        if (ideaOutput == null)
        {
            return (new ArticleIdeaOutput
            {
                Ideas = []
            }, usedCategories);
        }

        return (ideaOutput, usedCategories);
    }

    private string BuildTrendsContext(TrendAnalysisOutput trends)
    {
        if (trends.Trends == null || trends.Trends.Length == 0)
        {
            return "No specific trends available. Use general industry knowledge.";
        }

        string context = "Trending Topics:\n";
        foreach (TrendItem trend in trends.Trends.Take(5))
        {
            context += $"\n- {trend.Topic} (Relevance: {trend.Relevance:F2})\n";
            context += $"  {trend.Description}\n";
            if (trend.Keywords != null && trend.Keywords.Length > 0)
            {
                context += $"  Keywords: {string.Join(", ", trend.Keywords)}\n";
            }
        }

        return context;
    }

    /// <summary>
    /// Selects balanced article angle hints across categories to diversify ideation
    /// </summary>
    private static string[] GetBalancedArticleAngles(int count, HashSet<AngleCategory> previouslyUsedCategories)
    {
        var (angles, _) = GetBalancedArticleAnglesWithCategories(count, previouslyUsedCategories);
        return angles;
    }

    /// <summary>
    /// Selects balanced article angle hints across categories and returns categories used
    /// </summary>
    private static (string[] angles, HashSet<AngleCategory> categoriesUsed) GetBalancedArticleAnglesWithCategories(int count, HashSet<AngleCategory> previouslyUsedCategories)
    {
        if (count <= 0)
            return ([], []);

        // Group angles by category
        var anglesByCategory = ArticleAngles
            .GroupBy(a => a.Category)
            .ToDictionary(g => g.Key, g => g.ToList());

        List<ArticleAngle> selected = [];
        HashSet<AngleCategory> usedInThisBatch = [];

        // First pass: Try to select from categories not used in previous batches
        var preferredCategories = anglesByCategory.Keys
            .Where(cat => !previouslyUsedCategories.Contains(cat))
            .ToList();

        if (preferredCategories.Count > 0)
        {
            // Shuffle preferred categories
            var shuffledPreferred = preferredCategories.OrderBy(_ => _random.Next()).ToList();
            
            foreach (var category in shuffledPreferred.Take(count))
            {
                if (anglesByCategory.TryGetValue(category, out var angles))
                {
                    var angle = angles[_random.Next(angles.Count)];
                    selected.Add(angle);
                    usedInThisBatch.Add(category);
                    
                    if (selected.Count >= count)
                        break;
                }
            }
        }

        // Second pass: Fill remaining slots with any available category, prioritizing unused ones
        while (selected.Count < count)
        {
            var availableCategories = anglesByCategory.Keys
                .Where(cat => !usedInThisBatch.Contains(cat))
                .ToList();

            if (availableCategories.Count == 0)
                availableCategories = anglesByCategory.Keys.ToList();

            if (availableCategories.Count == 0)
                break;

            var category = availableCategories[_random.Next(availableCategories.Count)];
            if (anglesByCategory.TryGetValue(category, out var angles))
            {
                var angle = angles[_random.Next(angles.Count)];
                selected.Add(angle);
                usedInThisBatch.Add(category);
            }
        }

        return (selected.Select(a => a.Hint).ToArray(), usedInThisBatch);
    }
}

