using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado;

/// <summary>
///     Lifetime of a <see cref="TornadoApi"/> registered in a <see cref="TornadoApiRouter"/> with a factory.
/// </summary>
public enum TornadoApiLifetime
{
    /// <summary>
    ///     The factory runs once, the instance is shared by every caller.
    /// </summary>
    Singleton,

    /// <summary>
    ///     The factory runs once per <see cref="TornadoApiRouterScope"/>. Resolving from the router itself, outside of a scope, throws.
    /// </summary>
    Scoped,

    /// <summary>
    ///     The factory runs on every resolution.
    /// </summary>
    Transient
}

/// <summary>
///     Routes models to one of several named <see cref="TornadoApi"/> instances, for example to use OpenAI and a self-hosted OpenAI-compatible server side by side.
///     A single <see cref="TornadoApi"/> holds one endpoint per <see cref="LLmProviders"/>, the router lifts that limitation without changing it.
/// </summary>
public class TornadoApiRouter
{
    private sealed class Entry
    {
        public TornadoApiLifetime Lifetime { get; }
        public Func<TornadoApi> Factory { get; }
        public Lazy<TornadoApi>? Singleton { get; }

        public Entry(TornadoApiLifetime lifetime, Func<TornadoApi> factory)
        {
            Lifetime = lifetime;
            Factory = factory;

            if (lifetime is TornadoApiLifetime.Singleton)
            {
                Singleton = new Lazy<TornadoApi>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
            }
        }
    }

    private readonly ConcurrentDictionary<string, Entry> entries = new ConcurrentDictionary<string, Entry>(StringComparer.Ordinal);
    private Func<IModel, string>? route;

    /// <summary>
    ///     Creates a router without a routing function, set it with <see cref="Route"/> before resolving models.
    /// </summary>
    public TornadoApiRouter()
    {
    }

    /// <summary>
    ///     Creates a router.
    /// </summary>
    /// <param name="route">Returns the name of the registered <see cref="TornadoApi"/> that serves the given model.</param>
    public TornadoApiRouter(Func<IModel, string> route)
    {
        this.route = route ?? throw new ArgumentNullException(nameof(route));
    }

    /// <summary>
    ///     Sets the function that returns the name of the registered <see cref="TornadoApi"/> that serves the given model.
    /// </summary>
    public TornadoApiRouter Route(Func<IModel, string> routeFn)
    {
        route = routeFn ?? throw new ArgumentNullException(nameof(routeFn));
        return this;
    }

    /// <summary>
    ///     Registers an existing instance under a name. It behaves as a singleton.
    /// </summary>
    public TornadoApiRouter Add(string name, TornadoApi api)
    {
        if (api is null)
        {
            throw new ArgumentNullException(nameof(api));
        }

        return Add(name, () => api, TornadoApiLifetime.Singleton);
    }

    /// <summary>
    ///     Registers a factory under a name.
    /// </summary>
    public TornadoApiRouter Add(string name, Func<TornadoApi> factory, TornadoApiLifetime lifetime = TornadoApiLifetime.Singleton)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name must not be empty.", nameof(name));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        if (!entries.TryAdd(name, new Entry(lifetime, factory)))
        {
            throw new ArgumentException($"A TornadoApi named '{name}' is already registered.", nameof(name));
        }

        return this;
    }

    /// <summary>
    ///     Names of the registered instances.
    /// </summary>
    public IReadOnlyCollection<string> Names => entries.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();

    /// <summary>
    ///     Returns the instance that serves the given model.
    /// </summary>
    /// <exception cref="InvalidOperationException">No routing function is set, it returned an unregistered name, or the target is scoped.</exception>
    public TornadoApi Resolve(IModel model)
    {
        return Get(ResolveName(model), null);
    }

    /// <summary>
    ///     Returns the instance registered under the given name.
    /// </summary>
    /// <exception cref="InvalidOperationException">The name is not registered, or the target is scoped.</exception>
    public TornadoApi Get(string name)
    {
        return Get(name, null);
    }

    /// <summary>
    ///     Creates a scope, scoped instances are created once per scope.
    /// </summary>
    public TornadoApiRouterScope CreateScope()
    {
        return new TornadoApiRouterScope(this);
    }

    internal string ResolveName(IModel model)
    {
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        if (route is null)
        {
            throw new InvalidOperationException("No routing function is set. Pass one to the constructor or call Route().");
        }

        string? name = route(model);

        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException($"The routing function returned no name for model '{model.Name}'. Registered names: {FormatNames()}.");
        }

        return name;
    }

    internal TornadoApi Get(string name, TornadoApiRouterScope? scope)
    {
        if (!entries.TryGetValue(name, out Entry? entry))
        {
            throw new InvalidOperationException($"No TornadoApi named '{name}' is registered. Registered names: {FormatNames()}.");
        }

        switch (entry.Lifetime)
        {
            case TornadoApiLifetime.Singleton:
                return entry.Singleton!.Value;
            case TornadoApiLifetime.Transient:
                return entry.Factory();
            default:
                if (scope is null)
                {
                    throw new InvalidOperationException($"The TornadoApi named '{name}' is scoped, resolve it from a scope created by CreateScope().");
                }

                return scope.GetOrCreate(name, entry.Factory);
        }
    }

    private string FormatNames()
    {
        return entries.IsEmpty ? "none" : string.Join(", ", Names.Select(x => $"'{x}'"));
    }
}

/// <summary>
///     Scope of a <see cref="TornadoApiRouter"/>, holds the instances registered with <see cref="TornadoApiLifetime.Scoped"/>.
/// </summary>
public class TornadoApiRouterScope
{
    private readonly TornadoApiRouter router;
    private readonly ConcurrentDictionary<string, Lazy<TornadoApi>> scoped = new ConcurrentDictionary<string, Lazy<TornadoApi>>(StringComparer.Ordinal);

    internal TornadoApiRouterScope(TornadoApiRouter router)
    {
        this.router = router;
    }

    /// <summary>
    ///     Returns the instance that serves the given model.
    /// </summary>
    public TornadoApi Resolve(IModel model)
    {
        return router.Get(router.ResolveName(model), this);
    }

    /// <summary>
    ///     Returns the instance registered under the given name.
    /// </summary>
    public TornadoApi Get(string name)
    {
        return router.Get(name, this);
    }

    internal TornadoApi GetOrCreate(string name, Func<TornadoApi> factory)
    {
        return scoped.GetOrAdd(name, _ => new Lazy<TornadoApi>(factory, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }
}
