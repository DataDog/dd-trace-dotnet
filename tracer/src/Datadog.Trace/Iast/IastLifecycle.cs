// <copyright file="IastLifecycle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.Iast;

/// <summary>
/// Tracks the IAST request that is currently being processed.
/// The request entry points (ASP.NET / ASP.NET Core) call <see cref="BeginRequest"/> when a request starts and store the returned
/// state in the <c>HttpContext.Items</c> of the request (<see cref="HttpContextKey"/>), so that code that is handed the HttpContext can reach it
/// without relying on ambient state. The ambient (<see cref="AsyncLocal{T}"/>) value is what the IAST sinks and sources read through <see cref="CurrentContext"/>.
/// Where the host does not flow the execution context between pipeline steps (IIS), the ambient value is restored with <see cref="Resume"/>
/// when ASP.NET re-associates the HttpContext with a thread.
/// </summary>
internal static class IastLifecycle
{
    /// <summary>
    /// The key under which the request state is stored in the HttpContext items
    /// </summary>
    internal const string HttpContextKey = "__Datadog.Iast.Request";

    private static readonly AsyncLocal<IastRequestState?> Current = new();

    /// <summary>
    /// Gets a value indicating whether we are inside a web request. This is true even when IAST decided not to analyze
    /// the request (see <see cref="CurrentContext"/>).
    /// </summary>
    internal static bool InRequest => Current.Value is not null;

    /// <summary>
    /// Gets the IAST context of the current request, or null if we are not in a request
    /// or the overhead controller decided not to analyze it.
    /// </summary>
    internal static IastRequestContext? CurrentContext => Current.Value?.Context;

    /// <summary>
    /// Gets the web span of the current request. Temporary: only used to reach the trace until the spans are removed.
    /// </summary>
    internal static Span? CurrentRootSpan => Current.Value?.RootSpan;

    /// <summary>
    /// Marks the start of a web request and makes it the current one.
    /// </summary>
    /// <param name="rootSpan">The web span of the request</param>
    /// <param name="sampled">Whether IAST analyzes this request (the overhead controller acquired it)</param>
    /// <param name="httpMethod">The HTTP method of the request</param>
    /// <param name="httpUrl">The full URL of the request</param>
    /// <returns>The state of the request. The caller stores it in the HttpContext items under <see cref="HttpContextKey"/></returns>
    internal static IastRequestState BeginRequest(Span rootSpan, bool sampled, string? httpMethod, string? httpUrl)
    {
        var context = sampled ? new IastRequestContext() : null;
        context?.SetRequestInfo(httpMethod, httpUrl);

        var state = new IastRequestState(rootSpan, context);
        Current.Value = state;
        return state;
    }

    /// <summary>
    /// Makes the given request the current one. Used when the host re-associates the HttpContext with a thread.
    /// </summary>
    internal static void Resume(IastRequestState? state)
    {
        Current.Value = state;
    }

    /// <summary>
    /// Clears the current request. Used when the host dissociates the HttpContext from a thread.
    /// </summary>
    internal static void Suspend()
    {
        Current.Value = null;
    }

    /// <summary>
    /// Marks the end of a web request: updates the route stats and releases the overhead controller slot taken when the request began,
    /// then clears the ambient state if it still belongs to this request. Safe to call more than once for the same request.
    /// </summary>
    /// <param name="state">The state returned by <see cref="BeginRequest"/>, or null</param>
    internal static void EndRequest(IastRequestState? state)
    {
        if (state is null)
        {
            return;
        }

        if (state.TryEnd() && state.Context is { } context)
        {
            context.OnRequestEnded();
            Iast.Instance.OverheadController.ReleaseRequest();
        }

        if (ReferenceEquals(Current.Value, state))
        {
            Current.Value = null;
        }
    }

#if NETFRAMEWORK
    internal static IastRequestState? GetState(System.Collections.IDictionary? items) => items?[HttpContextKey] as IastRequestState;

    internal static IastRequestContext? GetContext(System.Collections.IDictionary? items) => GetState(items)?.Context;
#else
    internal static IastRequestState? GetState(IDictionary<object, object?>? items) => items is not null && items.TryGetValue(HttpContextKey, out var state) ? state as IastRequestState : null;

    internal static IastRequestContext? GetContext(IDictionary<object, object?>? items) => GetState(items)?.Context;
#endif
}
