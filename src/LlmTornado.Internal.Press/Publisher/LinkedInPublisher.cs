using Flurl.Http;
using LlmTornado.Internal.Press.Database;
using LlmTornado.Internal.Press.Database.Models;
using Microsoft.EntityFrameworkCore;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using System.Text.Json;
using System.IO;
using LlmTornado.Internal.Press.Configuration;
using LlmTornado.Internal.Press.DataModels;
using System.Linq;
using System.Net.Http;

namespace LlmTornado.Internal.Press.Publisher;

public static class LinkedInPublisher
{
    private const string API_BASE_URL = "https://api.linkedin.com/v2/ugcPosts";
    private const string ASSETS_API_URL = "https://api.linkedin.com/v2/assets?action=registerUpload";
    private const int MAX_RETRIES = 2;
    private static readonly Random _random = Random.Shared;
    
    /// <summary>
    /// Diverse style variations to inject randomness into LinkedIn posts
    /// </summary>
    private static readonly string[] PostStyleVariations =
    [
        // Hook variations
        "**Hook**: Use a shocking statistic to grab attention",
        "**Hook**: Open with a bold contrarian statement",
        "**Hook**: Start with a relatable personal story",
        "**Hook**: Begin with an industry secret most people don't know",
        "**Hook**: Use a 'before vs after' scenario",
        
        // Structure variations
        "**Structure**: Keep it short and punchy (under 200 characters)",
        "**Structure**: Go detailed with multiple paragraphs",
        "**Structure**: Single powerful insight, expanded deeply",
        "**Structure**: Build tension and release with a surprise ending",
        
        // Emoji usage
        "**Emoji**: Use minimal emojis (1-2 only, very professional)",
        "**Emoji**: Moderate emoji usage (3-4 strategically placed)",
        "**Emoji**: More expressive with 5-6 emojis for energy",
        
        // Tone variations
        "**Tone**: Casual and conversational, like talking to a friend",
        "**Tone**: Professional and authoritative, industry expert voice",
        "**Tone**: Energetic and enthusiastic, show genuine excitement",
        "**Tone**: Thoughtful and reflective, contemplative approach",
        "**Tone**: Direct and no-nonsense, get straight to the point",
        
        // CTA variations
        "**CTA**: Ask for their experience ('What's been your approach?')",
        "**CTA**: Ask for agreement/disagreement ('Do you agree?')",
        "**CTA**: Ask what they'd add ('What would you add to this list?')",
        "**CTA**: Ask for their biggest challenge ('What's your main struggle with X?')",
        "**CTA**: Ask for predictions ('Where do you see this going?')",
        
        // Content approach
        "**Approach**: Focus on the 'why' more than the 'how'",
        "**Approach**: Focus on practical implementation details",
        "**Approach**: Emphasize lessons learned from mistakes",
        "**Approach**: Highlight the transformation or impact",
        "**Approach**: Compare multiple options objectively"
    ];
    
    /// <summary>
    /// Get random style hints for post generation
    /// </summary>
    private static string[] GetRandomStyleHints(int count = 3)
    {
        if (count >= PostStyleVariations.Length)
            return PostStyleVariations;
            
        // Shuffle and take
        string[] shuffled = PostStyleVariations.OrderBy(x => _random.Next()).Take(count).ToArray();
        return shuffled;
    }
    
    public static async Task<bool> PublishArticleAsync(
        Article article, 
        string accessToken,
        string authorUrn,
        PressDbContext dbContext,
        TornadoApi? aiClient = null,
        AppConfiguration? config = null,
        string? summaryJson = null)
    {
        Console.WriteLine($"[LinkedIn] 📤 Publishing: {article.Title}");
        
        // Check if already published
        ArticlePublishStatus? publishStatus = await dbContext.ArticlePublishStatus
            .FirstOrDefaultAsync(p => p.ArticleId == article.Id && p.Platform == "linkedin");
            
        if (publishStatus?.Status == "Published")
        {
            Console.WriteLine($"[LinkedIn] ℹ️ Already published: Post ID {publishStatus.PlatformArticleId}");
            return true;
        }
        
        // Get dev.to URL - will be included at the end of the post
        ArticlePublishStatus? devToStatus = await dbContext.ArticlePublishStatus
            .FirstOrDefaultAsync(p => p.ArticleId == article.Id && p.Platform == "devto" && p.Status == "Published");
            
        if (devToStatus == null || string.IsNullOrEmpty(devToStatus.PublishedUrl))
        {
            Console.WriteLine($"[LinkedIn] ⚠️ Article not published to dev.to yet - skipping LinkedIn share");
            return false;
        }
        
        // Create or update status record
        publishStatus ??= new ArticlePublishStatus
        {
            ArticleId = article.Id,
            Platform = "linkedin",
            Status = "Pending"
        };
        
        // Generate AI clickbait description (with summary if available)
        Console.WriteLine($"[LinkedIn] 🎯 Generating clickbait post description...");
        string clickbaitPost = await GenerateClickbaitPostAsync(article, devToStatus.PublishedUrl, aiClient, config, dbContext, summaryJson);
        
        // Save generated post text for future reference and diversity checking
        publishStatus.GeneratedPostText = clickbaitPost;
        Console.WriteLine($"[LinkedIn] 💾 Saved generated post text for future diversity checks");
        
        // Check if we have an image to share (local file or HTTP URL)
        bool hasImage = !string.IsNullOrEmpty(article.ImageUrl);
        string? localImagePath = null;
        
        if (hasImage)
        {
            // Check if it's a local file or HTTP URL
            if (article.ImageUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                article.ImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Download HTTP image to temp file
                Console.WriteLine($"[LinkedIn] 🌐 Downloading image from URL: {article.ImageUrl}");
                try
                {
                    using HttpClient httpClient = new HttpClient();
                    byte[] imageBytes = await httpClient.GetByteArrayAsync(article.ImageUrl);
                    
                    // Create temp file
                    localImagePath = Path.Combine(Path.GetTempPath(), $"linkedin_image_{Guid.NewGuid()}.png");
                    await File.WriteAllBytesAsync(localImagePath, imageBytes);
                    Console.WriteLine($"[LinkedIn] ✓ Downloaded to temp file: {localImagePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LinkedIn] ⚠️ Failed to download image: {ex.Message}");
                    hasImage = false;
                }
            }
            else if (File.Exists(article.ImageUrl))
            {
                // Local file path
                localImagePath = article.ImageUrl;
                Console.WriteLine($"[LinkedIn] 📁 Using local image: {localImagePath}");
            }
            else
            {
                Console.WriteLine($"[LinkedIn] ⚠️ Image not found: {article.ImageUrl}");
                hasImage = false;
            }
        }
        
        Dictionary<string, object> body;
        
        if (hasImage && localImagePath != null)
        {
            Console.WriteLine($"[LinkedIn] 🖼️ Image ready, uploading and sharing as IMAGE post...");
            
            // Upload image and get asset URN
            string? assetUrn = await UploadImageAsync(localImagePath, accessToken, authorUrn);
            
            // Clean up temp file if we downloaded it
            if (article.ImageUrl!.StartsWith("http", StringComparison.OrdinalIgnoreCase) && 
                File.Exists(localImagePath))
            {
                try
                {
                    File.Delete(localImagePath);
                    Console.WriteLine($"[LinkedIn] 🗑️ Cleaned up temp file");
                }
                catch { /* Ignore cleanup errors */ }
            }
            
            if (assetUrn != null)
            {
                // Share with image
                body = BuildImageShareBody(authorUrn, clickbaitPost, assetUrn, article.Title, devToStatus.PublishedUrl);
                Console.WriteLine($"[LinkedIn] ✨ Sharing with IMAGE");
            }
            else
            {
                // Fallback to article share if image upload failed
                Console.WriteLine($"[LinkedIn] ⚠️ Image upload failed, falling back to ARTICLE share");
                body = BuildArticleShareBody(authorUrn, clickbaitPost, devToStatus.PublishedUrl, article.Title, article.Description);
            }
        }
        else
        {
            Console.WriteLine($"[LinkedIn] 📄 No image available, sharing as ARTICLE");
            body = BuildArticleShareBody(authorUrn, clickbaitPost, devToStatus.PublishedUrl, article.Title, article.Description);
        }
        
        // Attempt publishing with retries
        for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
        {
            try
            {
                publishStatus.AttemptCount = attempt;
                publishStatus.LastAttemptDate = DateTime.UtcNow;
                
                Console.WriteLine($"[LinkedIn] 🔄 Attempt {attempt}/{MAX_RETRIES}...");
                
                string? response = await API_BASE_URL
                    .WithHeaders(new
                    {
                        Authorization = $"Bearer {accessToken}",
                        LinkedIn_Version = "202210",
                        X_Restli_Protocol_Version = "2.0.0"
                    })
                    .PostJsonAsync(body)
                    .ReceiveString();
                
                // Get the post ID from X-RestLi-Id header (would need to access response headers)
                // For now, we'll mark as published
                publishStatus.Status = "Published";
                publishStatus.PublishedUrl = devToStatus.PublishedUrl; // LinkedIn shares the dev.to URL
                publishStatus.PlatformArticleId = "published"; // LinkedIn doesn't return post ID in body
                publishStatus.PublishedDate = DateTime.UtcNow;
                publishStatus.LastError = null;
                
                if (publishStatus.Id == 0)
                    dbContext.ArticlePublishStatus.Add(publishStatus);
                
                await dbContext.SaveChangesAsync();
                
                Console.WriteLine($"[LinkedIn] ✅ Published: Shared {devToStatus.PublishedUrl}");
                return true;
            }
            catch (FlurlHttpException ex)
            {
                // Check if it's an auth error (don't retry)
                if (ex.StatusCode == 401)
                {
                    string error = "Unauthorized - check access token";
                    Console.WriteLine($"[LinkedIn] 🔒 {error}");
                    
                    publishStatus.Status = "Failed";
                    publishStatus.LastError = error;
                    
                    if (publishStatus.Id == 0)
                        dbContext.ArticlePublishStatus.Add(publishStatus);
                    
                    await dbContext.SaveChangesAsync();
                    return false;
                }
                
                // Other HTTP error - will retry
                string errorMsg = await ex.GetResponseStringAsync() ?? ex.Message;
                publishStatus.LastError = $"HTTP {ex.StatusCode}: {errorMsg}";
                
                Console.WriteLine($"[LinkedIn] ⚠️ Attempt {attempt} failed: {publishStatus.LastError}");
                
                if (attempt < MAX_RETRIES)
                {
                    await Task.Delay(2000); // Wait 2s before retry
                }
            }
            catch (Exception ex)
            {
                publishStatus.LastError = ex.Message;
                Console.WriteLine($"[LinkedIn] ❌ Attempt {attempt} error: {ex.Message}{(ex.InnerException is null ? string.Empty : $", inner exception: {ex.InnerException.Message}")}");
                
                if (attempt < MAX_RETRIES)
                {
                    await Task.Delay(2000);
                }
            }
        }
        
        // All retries failed
        publishStatus.Status = "Failed";
        
        if (publishStatus.Id == 0)
            dbContext.ArticlePublishStatus.Add(publishStatus);
        
        await dbContext.SaveChangesAsync();
        
        Console.WriteLine($"[LinkedIn] 💥 Failed after {MAX_RETRIES} attempts");
        return false;
    }
    
    /// <summary>
    /// Generate a diverse, engaging LinkedIn post using AI with strategy-driven approach
    /// </summary>
    private static async Task<string> GenerateClickbaitPostAsync(
        Article article, 
        string articleUrl, 
        TornadoApi? aiClient, 
        AppConfiguration? config,
        PressDbContext dbContext,
        string? summaryJson = null)
    {
        if (aiClient == null)
        {
            // Fallback to simple format if no AI client provided
            return $"🚀 {article.Title}\n\n{article.Description}\n\n📖 Read more: {articleUrl}";
        }
        
        try
        {
            // Retrieve previous 5 LinkedIn posts to avoid repetition
            List<string?> previousPosts = await dbContext.ArticlePublishStatus
                .Where(p => p.Platform == "linkedin" 
                    && p.Status == "Published" 
                    && p.GeneratedPostText != null)
                .OrderByDescending(p => p.PublishedDate)
                .Take(5)
                .Select(p => p.GeneratedPostText)
                .ToListAsync();
            
            Console.WriteLine($"[LinkedIn] 📚 Retrieved {previousPosts.Count} previous posts for diversity check");
            
            // Select random strategy
            LinkedInPostStrategy strategy = LinkedInStrategyLibrary.GetRandomStrategy();
            
            // Get random style hints
            string[] styleHints = GetRandomStyleHints(count: 3);
            Console.WriteLine($"[LinkedIn] 🎨 Selected {styleHints.Length} style variations:");
            foreach (string hint in styleHints)
            {
                Console.WriteLine($"  • {hint.Split(':')[0]}");
            }
            
            // Parse summary if available for enhanced context
            ArticleSummary? summary = null;
            if (!string.IsNullOrEmpty(summaryJson))
            {
                try
                {
                    summary = JsonSerializer.Deserialize<ArticleSummary>(summaryJson);
                    Console.WriteLine($"[LinkedIn] 📊 Using article summary for enhanced post generation");
                }
                catch
                {
                    Console.WriteLine($"[LinkedIn] ⚠️ Failed to parse summary, using article data only");
                }
            }
            
            // Build previous posts context
            string previousPostsContext = "";
            if (previousPosts.Count > 0)
            {
                previousPostsContext = $"""
                                       
                                       **PREVIOUS POSTS (Avoid Repetition):**
                                       
                                       Review these recent posts and create something DISTINCTLY DIFFERENT in approach, angle, and structure:
                                       
                                       {string.Join("\n\n---\n\n", previousPosts.Select((p, i) => $"Post {i + 1}:\n{p}"))}
                                       
                                       **CRITICAL**: Your post must feel fresh and unique compared to these. Use a different hook style, structure, and voice.
                                       
                                       ---
                                       
                                       """;
            }
            
            // Build summary context
            string summaryContext = "";
            if (summary != null)
            {
                summaryContext = $"""
                                 
                                 **Article Analysis:**
                                 
                                 Executive Summary: {summary.ExecutiveSummary}
                                 
                                 Key Technical Points:
                                 {string.Join("\n", summary.KeyPoints.Select(p => $"- {p}"))}
                                 
                                 Target Audience: {summary.TargetAudience}
                                 Emotional Tone: {summary.EmotionalTone}
                                 
                                 Pre-crafted Hook (you can use or improve): {summary.SocialMediaHook}
                                 
                                 ---
                                 
                                 """;
            }
            
            // Build style hints context
            string styleHintsContext = $"""
                                       **STYLE VARIATIONS FOR THIS POST:**
                                       
                                       {string.Join("\n", styleHints)}
                                       
                                       ---
                                       
                                       """;
            
            string prompt = $"""
                            Generate a highly engaging LinkedIn post for the following article.
                            
                            Article Title: {article.Title}
                            Article Description: {article.Description}
                            
                            {previousPostsContext}{summaryContext}{styleHintsContext}
                            {strategy.GetStrategyInstructions()}
                            
                            **CORE REQUIREMENTS:**
                            
                            1. **Emoji Guidelines**: Use emojis strategically (refer to style hints for guidance)
                               - Available: 🚀 💡 🔥 ⚡ 🎯 💪 🧠 ✨ 📈 👀 💻 🔧 🎨 ⚙️ 🛠️
                            
                            2. **Length**: 150-300 characters for optimal LinkedIn engagement
                            
                            3. **Link Placement**: Put the article URL at the VERY END with a call-to-action:
                               "📖 Read more: {articleUrl}" or "🔗 Full article: {articleUrl}"
                            
                            4. **Engagement**: End with a question or call-to-action to spark comments
                            
                            **ANTI-PATTERNS TO AVOID:**
                            - No hashtags (looks spammy)
                            - Don't be overly promotional
                            - Don't give away everything in the post
                            - Avoid generic phrases like "Check this out"
                            - Don't copy patterns from previous posts
                            
                            Generate ONLY the post text, nothing else. Include the article URL at the end.
                            """;

            // Get model from config or use default
            string modelName = config?.Models.LinkedInPost ?? "gpt-4o-mini";
            ChatModel model = new ChatModel(modelName);
            
            Conversation conversation = aiClient.Chat.CreateConversation(new ChatRequest
            {
                Model = model,
                Temperature = 1.0  // Increased temperature for more creative variation
            });
            conversation.AppendSystemMessage("You are a LinkedIn engagement expert who creates diverse, viral posts for developers. Each post should feel unique and fresh.");
            conversation.AppendUserInput(prompt);
            
            Console.WriteLine($"[LinkedIn] 🤖 Using model: {modelName} (temperature: 1.0)");
            
            string? response = await conversation.GetResponse();
            string generatedPost = response.Trim();
            
            // Ensure URL is at the end if not already there
            if (!generatedPost.Contains(articleUrl))
            {
                generatedPost += $"\n\n📖 Read more: {articleUrl}";
            }
            
            Console.WriteLine($"[LinkedIn] ✨ Generated post ({generatedPost.Length} chars)");
            return generatedPost;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LinkedIn] ⚠️ AI generation failed: {ex.Message}, using fallback");
            return $"🚀 {article.Title}\n\n{article.Description}\n\nWhat's your take on this? 🤔\n\n📖 Read more: {articleUrl}";
        }
    }
    
    /// <summary>
    /// Upload an image to LinkedIn and get the asset URN
    /// </summary>
    private static async Task<string?> UploadImageAsync(string imagePath, string accessToken, string authorUrn)
    {
        try
        {
            Console.WriteLine($"[LinkedIn] 📤 Registering image upload...");
            
            // Step 1: Register the upload
            Dictionary<string, object> registerBody = new Dictionary<string, object>
            {
                ["registerUploadRequest"] = new Dictionary<string, object>
                {
                    ["recipes"] = new[] { "urn:li:digitalmediaRecipe:feedshare-image" },
                    ["owner"] = authorUrn,
                    ["serviceRelationships"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["relationshipType"] = "OWNER",
                            ["identifier"] = "urn:li:userGeneratedContent"
                        }
                    }
                }
            };
            
            JsonElement registerResponse = await ASSETS_API_URL
                .WithHeaders(new
                {
                    Authorization = $"Bearer {accessToken}",
                    LinkedIn_Version = "202210",
                    X_Restli_Protocol_Version = "2.0.0"
                })
                .PostJsonAsync(registerBody)
                .ReceiveJson<JsonElement>();
            
            // Extract upload URL and asset URN
            string uploadUrl = registerResponse
                .GetProperty("value")
                .GetProperty("uploadMechanism")
                .GetProperty("com.linkedin.digitalmedia.uploading.MediaUploadHttpRequest")
                .GetProperty("uploadUrl")
                .GetString()!;
                
            string assetUrn = registerResponse
                .GetProperty("value")
                .GetProperty("asset")
                .GetString()!;
            
            Console.WriteLine($"[LinkedIn] 📤 Uploading image binary...");
            
            // Step 2: Upload the image binary
            byte[] imageData = await File.ReadAllBytesAsync(imagePath);
            
            IFlurlResponse? uploadResponse = await uploadUrl
                .WithHeader("Authorization", $"Bearer {accessToken}")
                .PutAsync(new ByteArrayContent(imageData));
            
            if (uploadResponse.StatusCode == 201 || uploadResponse.StatusCode == 200)
            {
                Console.WriteLine($"[LinkedIn] ✅ Image uploaded successfully: {assetUrn}");
                return assetUrn;
            }
            else
            {
                Console.WriteLine($"[LinkedIn] ⚠️ Image upload failed with status: {uploadResponse.StatusCode}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LinkedIn] ❌ Image upload error: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Build request body for sharing with an image
    /// </summary>
    private static Dictionary<string, object> BuildImageShareBody(
        string authorUrn, 
        string commentary, 
        string assetUrn, 
        string title,
        string articleUrl)
    {
        return new Dictionary<string, object>
        {
            ["author"] = authorUrn,
            ["lifecycleState"] = "PUBLISHED",
            ["specificContent"] = new Dictionary<string, object>
            {
                ["com.linkedin.ugc.ShareContent"] = new Dictionary<string, object>
                {
                    ["shareCommentary"] = new Dictionary<string, object>
                    {
                        ["text"] = commentary
                    },
                    ["shareMediaCategory"] = "IMAGE",
                    ["media"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["status"] = "READY",
                            ["description"] = new Dictionary<string, object>
                            {
                                ["text"] = $"From: {articleUrl}"
                            },
                            ["media"] = assetUrn,
                            ["title"] = new Dictionary<string, object>
                            {
                                ["text"] = title
                            }
                        }
                    }
                }
            },
            ["visibility"] = new Dictionary<string, object>
            {
                ["com.linkedin.ugc.MemberNetworkVisibility"] = "PUBLIC"
            }
        };
    }
    
    /// <summary>
    /// Build request body for sharing an article (no image)
    /// </summary>
    private static Dictionary<string, object> BuildArticleShareBody(
        string authorUrn,
        string commentary,
        string articleUrl,
        string title,
        string? description)
    {
        return new Dictionary<string, object>
        {
            ["author"] = authorUrn,
            ["lifecycleState"] = "PUBLISHED",
            ["specificContent"] = new Dictionary<string, object>
            {
                ["com.linkedin.ugc.ShareContent"] = new Dictionary<string, object>
                {
                    ["shareCommentary"] = new Dictionary<string, object>
                    {
                        ["text"] = commentary
                    },
                    ["shareMediaCategory"] = "ARTICLE",
                    ["media"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["status"] = "READY",
                            ["description"] = new Dictionary<string, object>
                            {
                                ["text"] = description ?? title
                            },
                            ["originalUrl"] = articleUrl,
                            ["title"] = new Dictionary<string, object>
                            {
                                ["text"] = title
                            }
                        }
                    }
                }
            },
            ["visibility"] = new Dictionary<string, object>
            {
                ["com.linkedin.ugc.MemberNetworkVisibility"] = "PUBLIC"
            }
        };
    }
}

