using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Chat.Vendors.Google;
using LlmTornado.Chat.Vendors.Mistral;
using LlmTornado.Chat.Vendors.Zai;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using PuppeteerSharp;
using System.Text;

namespace LlmTornado.Demo;

public partial class ChatDemo : DemoBase
{
    [TornadoTest]
    public static async Task ZaiWebSearch()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Zai.Glm.Glm46,
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "Use web search to find the latest release of NodeJS")
            ],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorZaiExtensions
            {
                BuiltInTools = [
                    new VendorZaiWebSearchTool
                    {
                        WebSearch = new VendorZaiWebSearchObject
                        {
                            Enable = true,
                            SearchEngine = VendorZaiSearchEngine.SearchProJina
                        }
                    }
                ]
            })
        });

        ChatRichResponse response = await chat.GetResponseRich();

        Console.WriteLine("ZAi:");
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task ZaiGlm()
    {
        await BasicChat(ChatModel.Zai.Glm.Glm46);
    }
    
    [TornadoTest]
    public static async Task MistralMagistralReasoning()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Mistral.Free.MagistralSmall2509,
            Messages =
            [
                new ChatMessage(ChatMessageRoles.User, "Solve step-by-step: John is one of 4 children. The first sister is 4 years old. Next year, the second sister will be twice as old as the first sister. The third sister is two years older than the second sister. The third sister is half the age of her older brother. How old is John?")
            ],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorMistralExtensions
            {
                PromptMode = MistralPromptMode.Reasoning
            })
        });

        ChatRichResponse response = await chat.GetResponseRich();

        Console.WriteLine("Magistral Small (reasoning prompt mode):");
        Console.WriteLine(response);
    }

    [TornadoTest]
    [Flaky("manual interaction")]
    public static async Task GoogleComputerUse()
    {
        // Download browser if needed
        Console.WriteLine("Downloading browser for Puppeteer...");
        await new BrowserFetcher().DownloadAsync();

        // Set up browser
        IBrowser? browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = false, // Set to true for headless operation
            DefaultViewport = new ViewPortOptions { Width = 1440, Height = 900 },
            Args = ["--no-sandbox", "--disable-setuid-sandbox"]
        });

        IPage? page = await browser.NewPageAsync();
        await page.SetViewportAsync(new ViewPortOptions { Width = 1440, Height = 900 });

        try
        {
            // Create Computer Use API instance
            TornadoApi api = Program.Connect();

            // Initial screenshot
            byte[]? screenshot = await page.ScreenshotDataAsync(new ScreenshotOptions { Type = ScreenshotType.Png });
            string base64Screenshot = Convert.ToBase64String(screenshot);

            Console.WriteLine("🤖 Starting Computer Use automation...");
            Console.WriteLine("Task: Navigate to Google and search for 'AI technology'");
            Console.WriteLine();

            // Create initial request with screenshot
            Conversation conversation = api.Chat.CreateConversation(new ChatRequest
            {
                Model = ChatModel.Google.GeminiPreview.Gemini25ComputerUsePreview102025,
                Messages =
                [
                    new ChatMessage(ChatMessageRoles.User, "Navigate to google.com and search for 'AI technology'")
                    {
                        Parts = [
                            new ChatMessagePart("Navigate to google.com and search for 'AI technology'"),
                            new ChatMessagePart(new ChatImage(base64Screenshot, "image/png"))
                        ]
                    }
                ],
                VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorGoogleExtensions
                {
                    ComputerUse = ChatRequestVendorGoogleComputerUse.Browser
                }),
                MaxTokens = 1000
            });
            
            // Computer Use loop
            int maxTurns = 10;
            for (int turn = 1; turn <= maxTurns; turn++)
            {
                Console.WriteLine($"--- Turn {turn} ---");

                TornadoRequestContent cc = conversation.Serialize();
                
                try
                {
                    // Send request to Computer Use model
                    ChatRichResponse? response = await conversation.GetResponseRich();

                    Console.WriteLine($"Model response: {response.Text}");

                    // Check if response contains tool calls (UI actions)
                    List<FunctionCall>? toolCalls = response.Blocks.Where(x => x.Type is ChatRichResponseBlockTypes.Function).Select(x => x.FunctionCall!).ToList();
                    if (toolCalls == null || toolCalls.Count == 0)
                    {
                        Console.WriteLine("✅ Task completed - no more actions needed");
                        break;
                    }

                    Console.WriteLine($"📋 Executing {toolCalls.Count} UI actions:");

                    ChatMessage? msg = null;

                    // Execute each UI action
                    foreach (FunctionCall toolCall in toolCalls)
                    {
                        msg = await ExecuteComputerUseAction(page, toolCall);
                    }

                    // Wait a moment for page to update
                    await Task.Delay(2000);

                    // Take new screenshot
                    byte[]? newScreenshot = await page.ScreenshotDataAsync(new ScreenshotOptions { Type = ScreenshotType.Png });
                    string newBase64Screenshot = Convert.ToBase64String(newScreenshot);

                    msg ??= new ChatMessage(ChatMessageRoles.User, [
                        new ChatMessagePart("Continue with the automation"),
                        new ChatMessagePart(new ChatImage(newBase64Screenshot, "image/png"))
                    ]);
                    
                    // Create follow-up request with new screenshot
                    conversation.AddMessage(msg);

                    Console.WriteLine($"🔄 Completed turn {turn}, continuing automation...");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error in turn {turn}: {ex.Message}");
                    break;
                }
            }

            Console.WriteLine("🎉 Computer Use automation demo completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Demo error: {ex.Message}");
            Console.WriteLine("Note: Computer Use model requires special API access");
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    private static async Task<ChatMessage?> ExecuteComputerUseAction(IPage page, FunctionCall toolCall)
    {
        ChatMessage? toReturn = null;
        
        if (toolCall.Name == null) return toReturn;

        Console.WriteLine($"  🔧 Executing: {toolCall.Name}");

        try
        {
            switch (toolCall.Name.ToLowerInvariant())
            {
                case "open_web_browser":
                    toReturn = new ChatMessage(ChatMessageRoles.User, "Browser opened");
                    // Browser is already open
                    Console.WriteLine("    ✅ Browser already open");
                    break;

                case "navigate":
                    if (toolCall.Get("url", out string? url))
                    {
                        await page.GoToAsync(url);
                        Console.WriteLine($"    ✅ Navigated to: {url}");
                    }
                    break;

                case "click_at":
                    if (toolCall.Get("x", out int? x) &&
                        toolCall.Get("y", out int? y))
                    {
                        // Convert normalized coordinates (0-1000) to actual pixels
                        int actualX = (int)(x / 1000.0 * 1440);
                        int actualY = (int)(y / 1000.0 * 900);

                        await page.Mouse.ClickAsync(actualX, actualY);
                        Console.WriteLine($"    ✅ Clicked at: ({actualX}, {actualY})");
                    }
                    break;

                case "type_text_at":
                    if (toolCall.Get("x", out int? textX) &&
                        toolCall.Get("y", out int? textY) &&
                        toolCall.Get("text", out string? text))
                    {
                        int actualTextX = (int)(textX / 1000.0 * 1440);
                        int actualTextY = (int)(textY / 1000.0 * 900);

                        await page.Mouse.ClickAsync(actualTextX, actualTextY);
                        await page.Keyboard.TypeAsync(text);

                        // Check for press_enter parameter
                        if (toolCall.Get("press_enter", out bool? pressEnter))
                        {
                            await page.Keyboard.PressAsync("Enter");
                            Console.WriteLine($"    ✅ Typed '{text}' and pressed Enter");
                        }
                        else
                        {
                            Console.WriteLine($"    ✅ Typed: {text}");
                        }
                    }
                    break;

                case "scroll_document":
                    if (toolCall.Get("direction", out string? direction))
                    {
                        switch (direction.ToLowerInvariant())
                        {
                            case "down":
                                await page.EvaluateExpressionAsync("window.scrollBy(0, window.innerHeight)");
                                Console.WriteLine("    ✅ Scrolled down");
                                break;
                            case "up":
                                await page.EvaluateExpressionAsync("window.scrollBy(0, -window.innerHeight)");
                                Console.WriteLine("    ✅ Scrolled up");
                                break;
                        }
                    }
                    break;

                case "wait_5_seconds":
                    await Task.Delay(5000);
                    Console.WriteLine("    ✅ Waited 5 seconds");
                    break;

                default:
                    Console.WriteLine($"    ⚠️  Unhandled action: {toolCall.Name}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ❌ Error executing {toolCall.Name}: {ex.Message}");
        }

        return toReturn;
    }

    [TornadoTest]
    public static async Task GoogleComputerUseWithExclusions()
    {
        Console.WriteLine("=== Google Computer Use with Function Exclusions ===");
        Console.WriteLine();

        // Example showing how to exclude certain Computer Use functions
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.Google.GeminiPreview.Gemini25ComputerUsePreview102025,
            Messages =
            [
                new ChatMessage(ChatMessageRoles.User,
                    "Open a web browser and navigate to example.com")
            ],
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorGoogleExtensions
            {
                ComputerUse = new ChatRequestVendorGoogleComputerUse(
                    ChatRequestVendorGoogleComputerUsePredefinedFunctions.DragAndDrop,
                    ChatRequestVendorGoogleComputerUsePredefinedFunctions.KeyCombination
                )
            })
        };

        Console.WriteLine("Computer Use configuration with exclusions:");
        Console.WriteLine($"- Environment: {request.VendorExtensions.Google.ComputerUse?.Environment}");
        Console.WriteLine($"- Excluded Functions: {string.Join(", ", request.VendorExtensions.Google.ComputerUse?.ExcludedPredefinedFunctions ?? [])}");
        Console.WriteLine();

        Console.WriteLine("Available Computer Use Functions:");
        Console.WriteLine("- OpenWebBrowser, Wait5Seconds, GoBack, GoForward, Search, Navigate");
        Console.WriteLine("- ClickAt, HoverAt, TypeTextAt, KeyCombination, ScrollDocument");
        Console.WriteLine("- ScrollAt, DragAndDrop");
        Console.WriteLine();
        Console.WriteLine("The model will avoid using the excluded functions in its response.");
    }

    [TornadoTest("MCP Anthropic")]
    [Flaky("Requires GITHUB_API_KEY setup in environment variables")]
    public static async Task AnthropicMcpServerUse()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude45.Sonnet250929,
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "Make a new branch on the Agent-Skills Repo")
            ],
            VendorExtensions = new ChatRequestVendorExtensions()
            {
                Anthropic = new ChatRequestVendorAnthropicExtensions
                {
                    McpServers = [
                    new AnthropicMcpServer(){
                            Name = "github",
                            Url = "https://api.githubcopilot.com/mcp/",
                            AuthorizationToken = Environment.GetEnvironmentVariable("GITHUB_API_KEY") ?? "github-api-key"
                        }
                    ]
                }
            }
        });

        ChatRichResponse response = await chat.GetResponseRich();
        Console.WriteLine("Anthropic MCP Server Use:");
        Console.WriteLine(response);
    }
    
    [TornadoTest]
    public static async Task QwenMax()
    {
        await BasicChat(ChatModel.Alibaba.Flagship.Qwen3Max);
    }
    
    // ===== Claude Opus 4.5 Specific Features =====
    
    [TornadoTest]
    public static async Task ClaudeOpus45Basic()
    {
        await BasicChat(ChatModel.Anthropic.Claude45.Opus251101);
    }
    
    [TornadoTest]
    public static async Task ClaudeOpus45Effort()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude45.Opus251101,
            ReasoningEffort = ChatReasoningEfforts.Low, // Most efficient, significant token savings
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "What is 2+2?")
            ]
        });

        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine("Claude Opus 4.5 with Low Effort:");
        Console.WriteLine(response.Text);
        Console.WriteLine($"Usage: {response.Usage?.TotalTokens} tokens");
    }
    
    [TornadoTest]
    public static async Task ClaudeOpus45ProgrammaticToolCalling()
    {
        // Define a tool that can only be called from code execution
        Tool queryTool = new Tool(new ToolFunction("query_database", "Execute a SQL query against the database", new
        {
            type = "object",
            properties = new
            {
                sql = new { type = "string", description = "SQL query to execute" }
            },
            required = new[] { "sql" }
        }))
        {
            AllowedCallers = [ToolAllowedCallers.CodeExecution20250825] // Only callable from code execution
        };
        
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude45.Opus251101,
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "Query the database to find the total sales for each region.")
            ],
            Tools = [queryTool],
            VendorExtensions = new ChatRequestVendorExtensions
            {
                Anthropic = new ChatRequestVendorAnthropicExtensions
                {
                    BuiltInTools = [new VendorAnthropicChatRequestBuiltInToolCodeExecution20250825()]
                }
            }
        });

        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine("Claude Opus 4.5 Programmatic Tool Calling:");
        Console.WriteLine(response.Text);
        
        // Check if any tool calls have caller info
        foreach (ChatRichResponseBlock block in response.Blocks.Where(b => b.Type == ChatRichResponseBlockTypes.Function))
        {
            if (block.FunctionCall?.ToolCall?.Caller != null)
            {
                Console.WriteLine($"Tool '{block.FunctionCall.Name}' called by: {block.FunctionCall.ToolCall.Caller.Type}");
                if (block.FunctionCall.ToolCall.Caller.ToolId != null)
                {
                    Console.WriteLine($"  From code execution tool: {block.FunctionCall.ToolCall.Caller.ToolId}");
                }
            }
        }
    }
    
    [TornadoTest]
    public static async Task ClaudeOpus45ToolSearchRegex()
    {
        // Define tools with defer_loading for on-demand discovery
        Tool weatherTool = new Tool(new ToolFunction("get_weather", "Get current weather for a location", new
        {
            type = "object",
            properties = new
            {
                location = new { type = "string", description = "City name" }
            },
            required = new[] { "location" }
        }))
        {
            DeferLoading = true // Only loaded when discovered via tool search
        };
        
        Tool stockTool = new Tool(new ToolFunction("get_stock_price", "Get current stock price", new
        {
            type = "object",
            properties = new
            {
                symbol = new { type = "string", description = "Stock symbol" }
            },
            required = new[] { "symbol" }
        }))
        {
            DeferLoading = true
        };
        
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude45.Opus251101,
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "What's the weather in San Francisco?")
            ],
            Tools = [weatherTool, stockTool],
            VendorExtensions = new ChatRequestVendorExtensions
            {
                Anthropic = new ChatRequestVendorAnthropicExtensions
                {
                    // Tool search tool - Claude searches for tools using regex patterns
                    BuiltInTools = [new VendorAnthropicChatRequestBuiltInToolSearchRegex20251119()]
                }
            }
        });

        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine("Claude Opus 4.5 Tool Search (Regex):");
        Console.WriteLine(response.Text);
    }
    
    [TornadoTest]
    public static async Task ClaudeOpus45ToolSearchBm25()
    {
        // Multiple deferred tools - Claude will search using natural language
        Tool[] deferredTools = [
            new Tool(new ToolFunction("send_email", "Send an email to a recipient", new
            {
                type = "object",
                properties = new
                {
                    to = new { type = "string" },
                    subject = new { type = "string" },
                    body = new { type = "string" }
                },
                required = new[] { "to", "subject", "body" }
            })) { DeferLoading = true },
            
            new Tool(new ToolFunction("create_calendar_event", "Create a calendar event", new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string" },
                    date = new { type = "string" },
                    time = new { type = "string" }
                },
                required = new[] { "title", "date" }
            })) { DeferLoading = true },
            
            new Tool(new ToolFunction("search_contacts", "Search for contacts by name", new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string" }
                },
                required = new[] { "query" }
            })) { DeferLoading = true }
        ];
        
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Anthropic.Claude45.Opus251101,
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "Schedule a meeting with John for tomorrow at 3pm.")
            ],
            Tools = [..deferredTools],
            VendorExtensions = new ChatRequestVendorExtensions
            {
                Anthropic = new ChatRequestVendorAnthropicExtensions
                {
                    // Tool search using BM25 natural language queries
                    BuiltInTools = [new VendorAnthropicChatRequestBuiltInToolSearchBm2520251119()]
                }
            }
        });

        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine("Claude Opus 4.5 Tool Search (BM25):");
        Console.WriteLine(response.Text);
    }
    
    // ===== Gemini 3 Pro Image Preview Specific Features =====
    
    [TornadoTest]
    public static async Task Gemini3ProImagePreviewBasic()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.GeminiPreview.Gemini3ProImagePreview,
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "Create a minimalist logo for a coffee shop called 'The Daily Grind'. The text should be in a clean, bold, sans-serif font. The color scheme is black and white.")
            ],
            Modalities = [ChatModelModalities.Text, ChatModelModalities.Image],
            // Configure image output: square aspect ratio, 2K resolution
            ImageOutput = new ChatImageOutputConfig
            {
                AspectRatio = ChatImageAspectRatios.Square,
                Resolution = ChatImageResolutions.Resolution2K
            }
        });

        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine("Gemini 3 Pro Image Preview:");
        Console.WriteLine(response.Text);
        
        // Display generated images
        foreach (ChatRichResponseBlock block in response.Blocks.Where(b => b.ChatImage is not null))
        {
            Console.WriteLine($"Generated image: {block.ChatImage?.MimeType} ({block.ChatImage?.Url?.Length} chars)");
            await DisplayImage(block.ChatImage!.Url);
        }
    }
    
    [TornadoTest]
    public static async Task Gemini3ProImagePreviewWidescreen()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.GeminiPreview.Gemini3ProImagePreview,
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "Create a cinematic landscape photo of a sunset over mountains with dramatic clouds.")
            ],
            Modalities = [ChatModelModalities.Text, ChatModelModalities.Image],
            // Configure image output: widescreen aspect ratio, 4K resolution
            ImageOutput = new ChatImageOutputConfig
            {
                AspectRatio = ChatImageAspectRatios.Landscape16x9,
                Resolution = ChatImageResolutions.Resolution4K
            }
        });

        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine("Gemini 3 Pro Image Preview (Widescreen 16:9, 4K):");
        Console.WriteLine(response.Text);
        
        foreach (ChatRichResponseBlock block in response.Blocks.Where(b => b.ChatImage is not null))
        {
            Console.WriteLine($"Generated image: {block.ChatImage?.MimeType} ({block.ChatImage?.Url?.Length} chars)");
            await DisplayImage(block.ChatImage!.Url);
        }
    }
    
    [TornadoTest]
    public static async Task Gemini3ProImagePreviewWithGoogleSearch()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.Google.GeminiPreview.Gemini3ProImagePreview,
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "Create a vibrant infographic that explains photosynthesis as a recipe for plant food. Show the ingredients (sunlight, water, CO2) and the finished dish (sugar/energy).")
            ],
            Modalities = [ChatModelModalities.Text, ChatModelModalities.Image],
            ImageOutput = new ChatImageOutputConfig
            {
                AspectRatio = ChatImageAspectRatios.Portrait3x4,
                Resolution = ChatImageResolutions.Resolution2K
            },
            VendorExtensions = new ChatRequestVendorExtensions(new ChatRequestVendorGoogleExtensions
            {
                GoogleSearch = ChatRequestVendorGoogleSearch.Inst
            })
        });

        ChatRichResponse response = await chat.GetResponseRich();
        
        Console.WriteLine("Gemini 3 Pro Image Preview with Google Search:");
        Console.WriteLine(response.Text);
        
        foreach (ChatRichResponseBlock block in response.Blocks.Where(b => b.ChatImage is not null))
        {
            Console.WriteLine($"Generated image: {block.ChatImage?.MimeType} ({block.ChatImage?.Url?.Length} chars)");
            await DisplayImage(block.ChatImage!.Url);
        }
    }
    
    [TornadoTest]
    public static async Task Grok41FastReasoningStreaming()
    {
        Conversation chat = Program.Connect().Chat.CreateConversation(new ChatRequest
        {
            Model = ChatModel.XAi.Grok41.V41FastReasoning,
            Messages = [
                new ChatMessage(ChatMessageRoles.User, "Important: reason before answering. Solve step-by-step: If a train travels 120 miles in 2 hours, and another train travels 180 miles in 3 hours, which train is faster?")
            ]
        });

        Console.WriteLine("Grok 4.1 Fast Reasoning (streaming):");
        Console.WriteLine("Reasoning (gray):");
        
        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            ReasoningTokenHandler = (reasoning) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(reasoning.Content);
                Console.ResetColor();
                return ValueTask.CompletedTask;
            },
            MessageTokenExHandler = (token) =>
            {
                if (token.Index is 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Response:");
                }
                
                Console.Write(token);
                return ValueTask.CompletedTask;
            },
            BlockFinishedHandler = (block) =>
            {
                Console.WriteLine();
                return ValueTask.CompletedTask;
            }
        });
    }
}

