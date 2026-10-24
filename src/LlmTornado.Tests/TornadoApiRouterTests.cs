using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Tests;

/// <summary>
/// Unit tests for <see cref="TornadoApiRouter"/>, no network involved.
/// </summary>
[TestFixture]
public class TornadoApiRouterTests
{
    private static readonly ChatModel Remote = new ChatModel("gpt-remote", LLmProviders.OpenAi);
    private static readonly ChatModel Local = new ChatModel("llama-local", LLmProviders.Custom);

    private static string ByProvider(IModel model) => model.Provider is LLmProviders.OpenAi ? "openai" : "local";

    [Test]
    public void Resolve_RoutesModelToTheNamedApi()
    {
        TornadoApi openAi = new TornadoApi(LLmProviders.OpenAi, "key-a");
        TornadoApi local = new TornadoApi(new Uri("http://localhost:11434"), "key-b");
        TornadoApiRouter router = new TornadoApiRouter(ByProvider).Add("openai", openAi).Add("local", local);

        Assert.That(router.Resolve(Remote), Is.SameAs(openAi));
        Assert.That(router.Resolve(Local), Is.SameAs(local));
        Assert.That(router.Get("local"), Is.SameAs(local));
    }

    [Test]
    public void Route_SetsTheRoutingFunction()
    {
        TornadoApi api = new TornadoApi();
        TornadoApiRouter router = new TornadoApiRouter().Route(_ => "only").Add("only", api);

        Assert.That(router.Resolve(Remote), Is.SameAs(api));
    }

    [Test]
    public void Singleton_RunsTheFactoryOnce()
    {
        int created = 0;
        TornadoApiRouter router = new TornadoApiRouter(_ => "a").Add("a", () =>
        {
            created++;
            return new TornadoApi();
        });

        Assert.That(router.Resolve(Remote), Is.SameAs(router.Resolve(Local)));
        Assert.That(created, Is.EqualTo(1));
    }

    [Test]
    public void Transient_RunsTheFactoryOnEveryResolution()
    {
        TornadoApiRouter router = new TornadoApiRouter(_ => "a").Add("a", () => new TornadoApi(), TornadoApiLifetime.Transient);

        TornadoApi first = router.Resolve(Remote);
        TornadoApi second = router.Resolve(Remote);

        Assert.That(first, Is.Not.SameAs(second));
    }

    [Test]
    public void Scoped_IsSharedInsideAScopeOnly()
    {
        TornadoApiRouter router = new TornadoApiRouter(_ => "a").Add("a", () => new TornadoApi(), TornadoApiLifetime.Scoped);

        TornadoApiRouterScope first = router.CreateScope();
        TornadoApiRouterScope second = router.CreateScope();

        Assert.That(first.Resolve(Remote), Is.SameAs(first.Get("a")));
        Assert.That(first.Resolve(Remote), Is.Not.SameAs(second.Resolve(Remote)));
    }

    [Test]
    public void Scoped_ThrowsWhenResolvedOutsideAScope()
    {
        TornadoApiRouter router = new TornadoApiRouter(_ => "a").Add("a", () => new TornadoApi(), TornadoApiLifetime.Scoped);

        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => router.Resolve(Remote));
        Assert.That(ex!.Message, Does.Contain("scoped").And.Contain("'a'"));
    }

    [Test]
    public void Scoped_ResolvesSingletonsFromTheRouter()
    {
        TornadoApi api = new TornadoApi();
        TornadoApiRouter router = new TornadoApiRouter(_ => "a").Add("a", api);

        Assert.That(router.CreateScope().Resolve(Remote), Is.SameAs(api));
    }

    [Test]
    public void Resolve_ThrowsForAnUnregisteredNameAndListsTheRegisteredOnes()
    {
        TornadoApiRouter router = new TornadoApiRouter(_ => "missing").Add("openai", new TornadoApi()).Add("local", new TornadoApi());

        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => router.Resolve(Remote));
        Assert.That(ex!.Message, Does.Contain("'missing'").And.Contain("'local', 'openai'"));
    }

    [Test]
    public void Resolve_ThrowsWhenTheRoutingFunctionReturnsNoName()
    {
        TornadoApiRouter router = new TornadoApiRouter(_ => "").Add("a", new TornadoApi());

        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => router.Resolve(Remote));
        Assert.That(ex!.Message, Does.Contain("gpt-remote"));
    }

    [Test]
    public void Resolve_ThrowsWithoutARoutingFunction()
    {
        TornadoApiRouter router = new TornadoApiRouter().Add("a", new TornadoApi());

        Assert.Throws<InvalidOperationException>(() => router.Resolve(Remote));
    }

    [Test]
    public void Add_RejectsDuplicateEmptyAndNullArguments()
    {
        TornadoApiRouter router = new TornadoApiRouter().Add("a", new TornadoApi());

        Assert.Throws<ArgumentException>(() => router.Add("a", new TornadoApi()));
        Assert.Throws<ArgumentException>(() => router.Add("", new TornadoApi()));
        Assert.Throws<ArgumentNullException>(() => router.Add("b", (TornadoApi)null!));
        Assert.Throws<ArgumentNullException>(() => router.Add("b", (Func<TornadoApi>)null!));
        Assert.Throws<ArgumentNullException>(() => router.Route(null!));
    }
}
