using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using LlmTornado.ManagedAgents.Anthropic;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicManagedAgentsOctoberTests
{
    [Test]
    public void DynamicMultiagent_SeparatesRostersAndPreservesOmittedUpdateSettings()
    {
        var config = AnthropicManagedAgentMultiagentConfig.Dynamic();
        var request = new AnthropicManagedAgentUpdateRequest { Multiagent = config };
        JObject minimal = JObject.Parse(request.Serialize());
        Assert.That(minimal["multiagent"]!.ToString(Formatting.None), Is.EqualTo("{\"type\":\"multiagent_20261001\"}"));

        config.Subagents = new() { InlineAgents = AnthropicManagedAgentToggle.Disabled(), PredefinedAgents = [AnthropicManagedAgentRosterEntry.FromAgent("agent_worker", 3)] };
        config.Workflows = new() { PredefinedAgents = [] };
        config.Advisor = new() { Model = AnthropicManagedAgentModels.ClaudeOpus55 };
        JObject body = JObject.Parse(request.Serialize());
        Assert.That(body["multiagent"]?["subagents"]?["predefined_agents"]?[0]?["version"]?.Value<int>(), Is.EqualTo(3));
        Assert.That(body["multiagent"]?["subagents"]?["inline_agents"]?["type"]?.ToString(), Is.EqualTo("disabled"));
        Assert.That(body["multiagent"]?["workflows"]?["predefined_agents"], Is.Empty);
        Assert.That(body["multiagent"]?["advisor"]?.Children().Count(), Is.EqualTo(2));
        Assert.That(body["multiagent"]?["agents"], Is.Null);

        var restored = JsonConvert.DeserializeObject<AnthropicManagedAgentUpdateRequest>(body.ToString())!;
        Assert.That(restored.Multiagent?.Subagents?.PredefinedAgents?[0].Id, Is.EqualTo("agent_worker"));
        Assert.That(restored.Multiagent?.Workflows?.PredefinedAgents, Is.Empty);
    }

    [TestCase("\"agent_plain\"")]
    [TestCase("{\"type\":\"agent\",\"id\":\"agent_versioned\",\"version\":7}")]
    [TestCase("{\"type\":\"self\"}")]
    [TestCase("null")]
    public void RosterReferences_RoundTripWithoutRecursiveConverters(string json)
    {
        var value = JsonConvert.DeserializeObject<AnthropicManagedAgentRosterEntry>(json);
        Assert.That(JToken.DeepEquals(JToken.Parse(JsonConvert.SerializeObject(value)), JToken.Parse(json)), Is.True);
    }

    [TestCase("\"agent_plain\"")]
    [TestCase("{\"type\":\"agent\",\"id\":\"agent_versioned\",\"version\":2147483648}")]
    [TestCase("null")]
    public void SessionReferences_RoundTripStringVersionAndNull(string json)
    {
        var value = JsonConvert.DeserializeObject<AnthropicManagedAgentSessionAgent>(json);
        Assert.That(JToken.DeepEquals(JToken.Parse(JsonConvert.SerializeObject(value)), JToken.Parse(json)), Is.True);
    }

    [Test]
    public void ToolPoliciesAndRestrictions_PreserveDefaultsAndEmptyLists()
    {
        var request = new AnthropicManagedAgentCreateRequest
        {
            Tools = [new AnthropicManagedAgentToolset
            {
                DefaultConfig = new() { PermissionPolicy = AnthropicManagedAgentPermissionPolicy.Auto() },
                Configs = [new() { Name = "bash", PermissionPolicy = AnthropicManagedAgentPermissionPolicy.AlwaysAsk() },
                    new() { Type = "web_search", Name = "web_search", AllowedDomains = [], UserLocation = new() { Country = "CZ", Timezone = "Europe/Prague" } },
                    new() { Type = "web_fetch", Name = "web_fetch", BlockedDomains = ["private.example"], MaxContentTokens = 4000 }]
            }, new AnthropicManagedAgentMcpToolset { McpServerName = "calendar" }]
        };
        var tools = JObject.Parse(request.Serialize())["tools"]!;
        Assert.That(tools[0]?["default_config"]?["permission_policy"]?["type"]?.ToString(), Is.EqualTo("auto"));
        Assert.That(tools[0]?["configs"]?[0]?["permission_policy"]?["type"]?.ToString(), Is.EqualTo("always_ask"));
        Assert.That(tools[0]?["configs"]?[1]?["allowed_domains"], Is.Empty);
        Assert.That(tools[0]?["configs"]?[1]?["user_location"]?["type"]?.ToString(), Is.EqualTo("approximate"));
        Assert.That(tools[0]?["configs"]?[2]?["max_content_tokens"]?.Value<int>(), Is.EqualTo(4000));
        Assert.That(tools[1]?["default_config"], Is.Null, "Omission must retain MCP's server default, rather than silently enabling auto.");
    }

    [Test]
    public void Environment_MapsLimitedNetworkAndPackageManagers()
    {
        var request = new AnthropicManagedAgentEnvironmentCreateRequest
        {
            Config = AnthropicManagedAgentEnvironmentConfig.Cloud(new()
            {
                AllowedHosts = ["api.example.com", "*.example.org"], AllowMcpServers = false, AllowPackageManagers = true
            }, new() { ["pip"] = ["pandas"], ["npm"] = ["typescript"] })
        };
        var config = JObject.Parse(request.Serialize())["config"]!;
        Assert.That(config["networking"]?["type"]?.ToString(), Is.EqualTo("limited"));
        Assert.That(config["networking"]?["allowed_hosts"]?.Values<string>(), Is.EqualTo(new[] { "api.example.com", "*.example.org" }));
        Assert.That(config["networking"]?["allow_mcp_servers"]?.Value<bool>(), Is.False);
        Assert.That(config["networking"]?["allow_package_managers"]?.Value<bool>(), Is.True);
        Assert.That(config["packages"]?["pip"]?[0]?.ToString(), Is.EqualTo("pandas"));
    }

    [Test]
    public void Budget_UsesExactCentsAndDistinguishesRemovalFromOmission()
    {
        var create = new AnthropicManagedAgentSessionCreateRequest { Budget = AnthropicManagedAgentBudget.UsdCents(long.MaxValue) };
        var amount = JObject.Parse(create.Serialize())["budget"]!["max_list_cost"]!["amount"]!;
        Assert.That(amount.Type, Is.EqualTo(JTokenType.String));
        Assert.That(amount.ToString(), Is.EqualTo("9223372036854775807"));
        Assert.That(JObject.Parse(new AnthropicManagedAgentSessionUpdateRequest().Serialize())["budget"], Is.Null);
        Assert.That(JObject.Parse(new AnthropicManagedAgentSessionUpdateRequest { ClearBudget = true }.Serialize())["budget"]!.Type, Is.EqualTo(JTokenType.Null));
        Assert.Throws<ArgumentException>(() => new AnthropicManagedAgentSessionUpdateRequest { Budget = create.Budget, ClearBudget = true }.Serialize());
        Assert.Throws<ArgumentOutOfRangeException>(() => AnthropicManagedAgentBudget.UsdCents(0));
    }

    [TestCase("0")]
    [TestCase("01")]
    [TestCase("-1")]
    [TestCase("1.00")]
    [TestCase("1\n")]
    [TestCase("")]
    public void Budget_RejectsNonCanonicalAmounts(string amount)
    {
        var request = new AnthropicManagedAgentSessionCreateRequest { Budget = new() { MaxListCost = new() { Amount = amount } } };
        Assert.Throws<ArgumentException>(() => request.Serialize());
    }

    [Test]
    public void WorkflowEvents_KeepForwardCompatibleResultsAndPhaseCorrelation()
    {
        var created = JsonConvert.DeserializeObject<AnthropicManagedAgentEvent>("""{"type":"workflow_run.created","workflow_run_id":"wr_1","phases":[{"id":"phase_1","name":"Read"}],"description":null}""")!;
        var ended = JsonConvert.DeserializeObject<AnthropicManagedAgentEvent>("""{"type":"workflow_run.status_ended","workflow_run_id":"wr_1","result":{"type":"error","error":{"type":"future_error","message":"Details","new_detail":3}}}""")!;
        var phase = JsonConvert.DeserializeObject<AnthropicManagedAgentEvent>("""{"type":"workflow_run.phase_ended","workflow_run_phase_id":"phase_1","phase_started_id":"sevt_start"}""")!;
        var refused = JsonConvert.DeserializeObject<AnthropicManagedAgentEvent>("""{"type":"workflow_run.error","workflow_run_id":null,"error":{"type":"max_workflow_runs_error","message":"Too many"}}""")!;
        Assert.That(created.Phases?[0].Id, Is.EqualTo("phase_1"));
        Assert.That(ended.WorkflowResult?.Error?.Type, Is.EqualTo("future_error"));
        Assert.That(ended.WorkflowResult?.Error?.AdditionalData?["new_detail"]?.Value<int>(), Is.EqualTo(3));
        Assert.That(phase.PhaseStartedId, Is.EqualTo("sevt_start"));
        Assert.That(refused.WorkflowRunId, Is.Null);
        Assert.That(refused.Error?.Type, Is.EqualTo("max_workflow_runs_error"));
        var confirmation = JsonConvert.DeserializeObject<AnthropicManagedAgentEvent>("""{"type":"user.tool_confirmation","result":"allow"}""")!;
        Assert.That(confirmation.Result?.ToString(), Is.EqualTo("allow"));
        Assert.That(confirmation.WorkflowResult, Is.Null);
    }

    [Test]
    public void PermissionEvaluationsAndRedactedAdvisorContent_ArePreserved()
    {
        const string json = """{"type":"agent.tool_use","id":"sevt_ask","session_thread_id":"thr_1","evaluated_permission":"ask","evaluation":{"type":"auto","evaluated_permission":{"type":"ask","reason_code":"future_reason"}},"content":[{"type":"redacted","data":"opaque"}]}""";
        var evt = JsonConvert.DeserializeObject<AnthropicManagedAgentEvent>(json)!;
        Assert.That(evt.Evaluation?.EvaluatedPermission?.ReasonCode, Is.EqualTo("future_reason"));
        Assert.That(evt.EvaluatedPermission, Is.EqualTo("ask"));
        Assert.That(evt.Content?[0]?["data"]?.ToString(), Is.EqualTo("opaque"));
        var body = JObject.Parse(new AnthropicManagedAgentSendEventsRequest
        {
            Events = [new AnthropicManagedAgentToolConfirmationEvent { ToolUseId = evt.Id!, Result = "deny", DenyMessage = "Skip" },
                new AnthropicManagedAgentCustomToolResultEvent { CustomToolUseId = "sevt_custom", Content = [new() { Text = "Result" }], IsError = false },
                new AnthropicManagedAgentInterruptEvent { SessionThreadId = evt.SessionThreadId }]
        }.Serialize());
        Assert.That(body["events"]?[0]?["tool_use_id"]?.ToString(), Is.EqualTo("sevt_ask"));
        Assert.That(body["events"]?[1]?["custom_tool_use_id"]?.ToString(), Is.EqualTo("sevt_custom"));
        Assert.That(body["events"]?[2]?["session_thread_id"]?.ToString(), Is.EqualTo("thr_1"));
    }

    [TestCase("agent-get", "GET", "/v1/agents/agent_1")]
    [TestCase("agent-update", "POST", "/v1/agents/agent_1")]
    [TestCase("agent-archive", "POST", "/v1/agents/agent_1/archive")]
    [TestCase("environment-get", "GET", "/v1/environments/env_1")]
    [TestCase("session-get", "GET", "/v1/sessions/sesn_1")]
    [TestCase("session-update", "POST", "/v1/sessions/sesn_1")]
    [TestCase("session-delete", "DELETE", "/v1/sessions/sesn_1")]
    [TestCase("session-archive", "POST", "/v1/sessions/sesn_1/archive")]
    [TestCase("session-send", "POST", "/v1/sessions/sesn_1/events")]
    [TestCase("events-legacy", "GET", "/v1/sessions/sesn_1/events")]
    [TestCase("thread-get", "GET", "/v1/sessions/sesn_1/threads/thr_1")]
    [TestCase("thread-archive", "POST", "/v1/sessions/sesn_1/threads/thr_1/archive")]
    [TestCase("thread-events", "GET", "/v1/sessions/sesn_1/threads/thr_1/events")]
    public async Task ResourceMethods_ResolveAbsoluteProviderUrls(string operation, string verb, string path)
    {
        var exchange = await Exchange(async (api, ct) =>
        {
            IHttpCallResult result = operation switch
            {
                "agent-get" => await api.AnthropicManagedAgents.Get("agent_1", ct),
                "agent-update" => await api.AnthropicManagedAgents.Update("agent_1", new() { Name = "Updated" }, ct),
                "agent-archive" => await api.AnthropicManagedAgents.Archive("agent_1", ct),
                "environment-get" => await api.AnthropicManagedAgentEnvironments.Get("env_1", ct),
                "session-get" => await api.AnthropicManagedAgentSessions.Get("sesn_1", ct),
                "session-update" => await api.AnthropicManagedAgentSessions.Update("sesn_1", new() { ClearBudget = true }, ct),
                "session-delete" => await api.AnthropicManagedAgentSessions.Delete("sesn_1", ct),
                "session-archive" => await api.AnthropicManagedAgentSessions.Archive("sesn_1", ct),
                "session-send" => await api.AnthropicManagedAgentSessions.SendEvents("sesn_1", AnthropicManagedAgentSendEventsRequest.UserMessage("Hello"), ct),
                "events-legacy" => await api.AnthropicManagedAgentSessions.ListEvents("sesn_1", cancellationToken: ct),
                "thread-get" => await api.AnthropicManagedAgentSessions.GetThread("sesn_1", "thr_1", ct),
                "thread-archive" => await api.AnthropicManagedAgentSessions.ArchiveThread("sesn_1", "thr_1", ct),
                _ => await api.AnthropicManagedAgentSessions.ListThreadEvents("sesn_1", "thr_1", cancellationToken: ct)
            };
            Assert.That(result.Code, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result.Exception, Is.Null);
            return true;
        }, operation == "session-delete" ? "{\"type\":\"session_deleted\",\"id\":\"sesn_1\"}" : "{\"id\":\"resource\",\"data\":[]}");
        Assert.That(exchange.Path, Is.EqualTo(path));
        Assert.That(exchange.Method, Is.EqualTo(verb));
        if (operation == "session-update") Assert.That(JObject.Parse(exchange.Body)["budget"]!.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public async Task NativeEventPagination_EncodesRepeatedFiltersAndOpaqueCursor()
    {
        var exchange = await Exchange((api, ct) => api.AnthropicManagedAgentSessions.ListEventsPage("sesn_1", new()
        {
            Page = "page+/=?&", Limit = 2, Types = ["workflow_run.created", "workflow_run.status_ended"], Order = "asc", CreatedAtOrAfter = "2026-10-01T00:00:00Z"
        }, ct), """{"data":[{"type":"workflow_run.status_ended","workflow_run_id":"wr_1","result":{"type":"completed"}}],"next_page":"next+/="}""");
        Assert.That(exchange.Result.Data?.NextPage, Is.EqualTo("next+/="));
        Assert.That(exchange.Result.Data?.Data[0].WorkflowResult?.Type, Is.EqualTo("completed"));
        Assert.That(exchange.Query.GetValues("types[]"), Is.EqualTo(new[] { "workflow_run.created", "workflow_run.status_ended" }));
        Assert.That(exchange.Query["page"], Is.EqualTo("page+/=?&"));
        Assert.That(exchange.Query["after_id"], Is.Null);
        Assert.That(exchange.Query["created_at[gte]"], Is.EqualTo("2026-10-01T00:00:00Z"));
    }

    [TestCase("agents", "/v1/agents")]
    [TestCase("environments", "/v1/environments")]
    [TestCase("sessions", "/v1/sessions")]
    public async Task NativeResourcePagination_RetainsNextPage(string resource, string path)
    {
        var exchange = await Exchange(async (api, ct) =>
        {
            AnthropicManagedAgentPageQuery query = new() { Page = "cursor+/=", Limit = 5 };
            string? next = resource switch
            {
                "agents" => (await api.AnthropicManagedAgents.ListPage(query, ct)).Data?.NextPage,
                "environments" => (await api.AnthropicManagedAgentEnvironments.ListPage(query, ct)).Data?.NextPage,
                _ => (await api.AnthropicManagedAgentSessions.ListPage(query, ct)).Data?.NextPage
            };
            return next;
        }, """{"data":[],"next_page":"page_2"}""");
        Assert.That(exchange.Path, Is.EqualTo(path));
        Assert.That(exchange.Result, Is.EqualTo("page_2"));
        Assert.That(exchange.Query["page"], Is.EqualTo("cursor+/="));
    }

    [Test]
    public async Task LegacyList_UsesNativeCursorAndExposesNextPageToken()
    {
        var exchange = await Exchange((api, ct) => api.AnthropicManagedAgentSessions.ListEvents("sesn_1",
            new ListQuery(limit: 2, order: SortOrder.Ascending) { PageToken = "cursor+/=" }, ct),
            """{"data":[{"type":"session.status_idle"}],"next_page":"cursor_next"}""");
        Assert.That(exchange.Query["page"], Is.EqualTo("cursor+/="));
        Assert.That(exchange.Query["after_id"], Is.Null);
        Assert.That(exchange.Query["order"], Is.EqualTo("asc"));
        Assert.That(exchange.Result.Data?.NextPageToken, Is.EqualTo("cursor_next"));
        Assert.That(exchange.Result.Data?.HasMore, Is.True);
        Assert.That(exchange.Result.Data?.Items.Single().Type, Is.EqualTo("session.status_idle"));
    }

    [Test]
    public async Task SessionFilters_UseRepeatedStatusesAndPreserveExplicitFalse()
    {
        var exchange = await Exchange((api, ct) => api.AnthropicManagedAgentSessions.ListPage(new AnthropicManagedAgentSessionQuery
        {
            AgentId = "agent_1", AgentVersion = 2, IncludeArchived = false, Statuses = ["rescheduling", "idle"],
            DeploymentId = "deploy_1", MemoryStoreId = "memory_1", Order = "asc", CreatedAfter = "2026-10-01T00:00:00Z"
        }, ct), """{"data":[],"next_page":null}""");
        Assert.That(exchange.Query["include_archived"], Is.EqualTo("false"));
        Assert.That(exchange.Query["agent_id"], Is.EqualTo("agent_1"));
        Assert.That(exchange.Query["agent_version"], Is.EqualTo("2"));
        Assert.That(exchange.Query["deployment_id"], Is.EqualTo("deploy_1"));
        Assert.That(exchange.Query["memory_store_id"], Is.EqualTo("memory_1"));
        Assert.That(exchange.Query.GetValues("statuses[]"), Is.EqualTo(new[] { "rescheduling", "idle" }));
        Assert.That(exchange.Query["created_at[gt]"], Is.EqualTo("2026-10-01T00:00:00Z"));
    }

    [Test]
    public void SystemMessages_PreserveOrderedToolSettlementAndContext()
    {
        var body = JObject.Parse(new AnthropicManagedAgentSendEventsRequest
        {
            Events = [new AnthropicManagedAgentCustomToolResultEvent { CustomToolUseId = "call_1", Content = [new() { Text = "Done" }] },
                new AnthropicManagedAgentSystemMessageEvent { Content = [new() { Text = "Use Europe/Prague." }] }]
        }.Serialize());
        Assert.That(body["events"]?[0]?["type"]?.ToString(), Is.EqualTo("user.custom_tool_result"));
        Assert.That(body["events"]?[1]?["type"]?.ToString(), Is.EqualTo("system.message"));
        Assert.That(body["events"]?[1]?["content"]?[0]?["text"]?.ToString(), Is.EqualTo("Use Europe/Prague."));
    }

    [Test]
    public async Task ThreadList_KeepsAdvisorWorkflowUsageAndRepeatedStatuses()
    {
        var exchange = await Exchange((api, ct) => api.AnthropicManagedAgentSessions.ListThreads("sesn_1", new() { Statuses = ["idle", "terminated"], Page = "page_1" }, ct),
            """{"data":[{"type":"session_thread","id":"thr_1","parent_thread_id":"thr_primary","workflow_run_id":"wr_1","agent":{"type":"advisor","model":"claude-opus-5-5"},"usage":{"list_cost":{"amount":"125","currency":"USD"},"active_seconds":2.5}}],"next_page":null}""");
        Assert.That(exchange.Path, Is.EqualTo("/v1/sessions/sesn_1/threads"));
        Assert.That(exchange.Query.GetValues("statuses[]"), Is.EqualTo(new[] { "idle", "terminated" }));
        var thread = exchange.Result.Data!.Data.Single();
        Assert.That(thread.WorkflowRunId, Is.EqualTo("wr_1"));
        Assert.That(thread.Agent?["type"]?.ToString(), Is.EqualTo("advisor"));
        Assert.That(thread.UsageDetails?.ListCost?.Amount, Is.EqualTo("125"));
        Assert.That(thread.UsageDetails?.ActiveSeconds, Is.EqualTo(2.5));
    }

    [Test]
    public async Task WorkflowOpenErrors_PreserveServerDetails()
    {
        const string response = """{"type":"error","error":{"type":"invalid_request_error","message":"Run still open","details":{"error_code":"workflow_run_open"}}}""";
        var exchange = await Exchange((api, ct) => api.AnthropicManagedAgentSessions.Archive("sesn_1", ct), response, 400);
        Assert.That(exchange.Result.Ok, Is.False);
        Assert.That(exchange.Result.Code, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(JObject.Parse(exchange.Result.Response!)["error"]?["details"]?["error_code"]?.ToString(), Is.EqualTo("workflow_run_open"));
    }

    [Test]
    public async Task Delete_MapsNativeConfirmationToExistingBooleanResult()
    {
        var exchange = await Exchange((api, ct) => api.AnthropicManagedAgentSessions.Delete("sesn_1", ct), """{"id":"sesn_1","type":"session_deleted"}""");
        Assert.That(exchange.Result.Ok, Is.True);
        Assert.That(exchange.Result.Data, Is.True);
        Assert.That(exchange.Result.Exception, Is.Null);
        Assert.That(JObject.Parse(exchange.Result.Response!)["id"]?.ToString(), Is.EqualTo("sesn_1"));
    }

    [Test]
    public async Task TypedSendResult_ExposesAcceptedEventIds()
    {
        var exchange = await Exchange((api, ct) => api.AnthropicManagedAgentSessions.SendEventsWithResult("sesn_1", AnthropicManagedAgentSendEventsRequest.UserMessage("Hello"), ct),
            """{"data":[{"type":"user.message","id":"sevt_accepted","processed_at":null,"content":[{"type":"text","text":"Hello"}]}]}""");
        Assert.That(exchange.Result.Ok, Is.True);
        Assert.That(exchange.Result.Data?.Data.Single().Id, Is.EqualTo("sevt_accepted"));
        Assert.That(exchange.Path, Is.EqualTo("/v1/sessions/sesn_1/events"));
        Assert.That(JObject.Parse(exchange.Body)["events"]?[0]?["type"]?.ToString(), Is.EqualTo("user.message"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Stream_PreservesPreviewsCallbacksAndEventsAfterIdle(bool thread)
    {
        List<AnthropicManagedAgentEvent> workflows = [], tools = [], previews = [];
        List<string> text = [];
        int idle = 0;
        var handler = new AnthropicManagedAgentStreamEventHandler
        {
            OnWorkflowRun = e => { workflows.Add(e); return Task.CompletedTask; },
            OnToolUse = e => { tools.Add(e); return Task.CompletedTask; },
            OnPreview = e => { previews.Add(e); return Task.CompletedTask; },
            OnTextDelta = value => { text.Add(value); return Task.CompletedTask; },
            OnSessionIdle = _ => { idle++; return Task.CompletedTask; }
        };
        string[] events = [
            """{"type":"workflow_run.created","workflow_run_id":"wr_1","phases":[]}""",
            """{"type":"event_start","event":{"type":"agent.message","id":"msg_1"}}""",
            """{"type":"event_delta","event_id":"msg_1","delta":{"type":"content_delta","index":2,"content":{"type":"text","text":"Part"}}}""",
            """{"type":"agent.tool_use","id":"tool_1","evaluated_permission":"ask","evaluation":{"type":"auto","evaluated_permission":{"type":"ask","reason_code":"high_risk"}}}""",
            """{"type":"agent.message","id":"msg_1","content":[{"type":"redacted","data":"opaque"},{"type":"text","text":"Partial, then complete"}]}""",
            """{"type":"session.status_idle","stop_reason":{"type":"budget_reached"}}""",
            """{"type":"workflow_run.status_ended","workflow_run_id":"wr_1","result":{"type":"completed"}}"""];
        string sse = string.Concat(events.Select(e => "data: " + e + "\n\n"));
        var exchange = await Exchange(async (api, ct) =>
        {
            List<AnthropicManagedAgentStreamEvent> received = [];
            var options = new AnthropicManagedAgentStreamOptions { EventDeltas = ["agent.message", "agent.thinking"] };
            var stream = thread ? api.AnthropicManagedAgentSessions.StreamThreadEventsWithOptions("sesn_1", "thr_1", options, handler, ct)
                : api.AnthropicManagedAgentSessions.StreamEventsWithOptions("sesn_1", options, handler, ct);
            await foreach (var evt in stream) received.Add(evt);
            return received;
        }, sse, contentType: "text/event-stream");
        Assert.That(exchange.Path, Is.EqualTo(thread ? "/v1/sessions/sesn_1/threads/thr_1/stream" : "/v1/sessions/sesn_1/events/stream"));
        Assert.That(exchange.Query.GetValues("event_deltas[]"), Is.EqualTo(new[] { "agent.message", "agent.thinking" }));
        Assert.That(exchange.Result, Has.Count.EqualTo(7));
        Assert.That(exchange.Result.Last().EventType, Is.EqualTo("workflow_run.status_ended"));
        Assert.That(workflows, Has.Count.EqualTo(2));
        Assert.That(tools.Single().Evaluation?.EvaluatedPermission?.ReasonCode, Is.EqualTo("high_risk"));
        Assert.That(previews[0].PreviewEvent?.Id, Is.EqualTo("msg_1"));
        Assert.That(previews[1].Delta?.Index, Is.EqualTo(2));
        Assert.That(previews[1].Delta?.Content?["text"]?.ToString(), Is.EqualTo("Part"));
        Assert.That(text, Is.EqualTo(new[] { "Partial, then complete" }), "Preview text must not duplicate the final-message callback.");
        Assert.That(idle, Is.EqualTo(1));
    }

    [Test]
    public async Task Stream_CancellationInterruptsPendingRead()
    {
        using var server = new LocalServer();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var operation = Task.Run(async () =>
        {
            await foreach (var _ in server.Api.AnthropicManagedAgentSessions.StreamEvents("sesn_1", cancellationToken: cancel.Token)) { }
        }, timeout.Token);
        var context = await server.Listener.GetContextAsync().WaitAsync(timeout.Token);
        context.Response.ContentType = "text/event-stream";
        context.Response.SendChunked = true;
        await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("data: {\"type\":\"session.status_running\"}\n\n"), timeout.Token);
        await context.Response.OutputStream.FlushAsync(timeout.Token);
        cancel.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(3)));
        context.Response.Close();
    }

    private static async Task<ExchangeResult<T>> Exchange<T>(Func<TornadoApi, CancellationToken, Task<T>> action, string response, int status = 200, string contentType = "application/json")
    {
        using var server = new LocalServer();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        Task<T> operation = action(server.Api, timeout.Token);
        var context = await server.Listener.GetContextAsync().WaitAsync(timeout.Token);
        Assert.That(context.Request.Headers["x-api-key"], Is.EqualTo("local-test-key"));
        Assert.That(context.Request.Headers["anthropic-beta"], Does.Contain(VendorAnthropicManagedAgentsConstants.BetaHeader));
        string body = await new StreamReader(context.Request.InputStream).ReadToEndAsync(timeout.Token);
        var result = new ExchangeResult<T> { Path = context.Request.Url!.AbsolutePath, Method = context.Request.HttpMethod, Query = context.Request.QueryString, Body = body };
        context.Response.StatusCode = status;
        context.Response.ContentType = contentType;
        await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response), timeout.Token);
        context.Response.Close();
        result.Result = await operation.WaitAsync(timeout.Token);
        return result;
    }

    private sealed class ExchangeResult<T>
    {
        internal T Result { get; set; } = default!;
        internal string Path { get; set; } = "";
        internal string Method { get; set; } = "";
        internal string Body { get; set; } = "";
        internal System.Collections.Specialized.NameValueCollection Query { get; set; } = new();
    }

    private sealed class LocalServer : IDisposable
    {
        internal HttpListener Listener { get; } = new();
        internal TornadoApi Api { get; }
        internal LocalServer()
        {
            using TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            string prefix = $"http://127.0.0.1:{port}/";
            Listener.Prefixes.Add(prefix);
            Listener.Start();
            Api = new(new AnthropicEndpointProvider { Auth = new(LLmProviders.Anthropic, "local-test-key"), UrlResolver = (_, _, _) => prefix + "v1/{0}{1}" });
        }
        public void Dispose() => Listener.Close();
    }
}
