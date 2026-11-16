// <copyright file="IastLifecycle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading;

namespace Datadog.Trace.Iast;

/// <summary>
/// Tracks the IAST request that is currently being processed, independently of the active span/scope.
/// The request entry points (ASP.NET / ASP.NET Core) call <see cref="BeginRequest"/> when a request starts,
/// and the IAST sinks and sources read the ambient state through <see cref="CurrentContext"/>.
/// </summary>
internal static class IastLifecycle
{
    private static readonly AsyncLocal<RequestState?> Current = new();

    /// <summary>
    /// Gets a value indicating whether we are inside a web request. This is true even when IAST
    /// decided not to analyze the request (see <see cref="CurrentContext"/>).
    /// </summary>
    internal static bool InRequest => GetState() is not null;

    /// <summary>
    /// Gets the IAST context of the current request, or null if we are not in a request
    /// or the overhead controller decided not to analyze it.
    /// </summary>
    internal static IastRequestContext? CurrentContext => GetState()?.Context;

    /// <summary>
    /// Gets the web span of the current request. Temporary: only used to read request metadata
    /// (route, url, method) until the span dependency is removed.
    /// </summary>
    internal static Span? CurrentRootSpan => GetState()?.RootSpan;

    /// <summary>
    /// Marks the start of a web request.
    /// </summary>
    /// <param name="rootSpan">The web span of the request</param>
    /// <param name="sampled">Whether IAST analyzes this request (the overhead controller acquired it)</param>
    /// <returns>The request context, or null when the request is not analyzed</returns>
    internal static IastRequestContext? BeginRequest(Span rootSpan, bool sampled)
    {
        var context = sampled ? new IastRequestContext() : null;
        Current.Value = new RequestState(rootSpan, context);

        // Mirrored on the trace context so code that holds the root span explicitly (and the span close hook)
        // can still reach it. Removed together with the spans.
        rootSpan.Context?.TraceContext?.SetIastRequestContext(context);
        return context;
    }

    /// <summary>
    /// Marks the end of a web request. Does nothing if another request is the current one.
    /// </summary>
    internal static void EndRequest(Span rootSpan)
    {
        if (ReferenceEquals(Current.Value?.RootSpan, rootSpan))
        {
            Current.Value = null;
        }
    }

    private static RequestState? GetState()
    {
        var state = Current.Value;
        if (state is not null)
        {
            return state;
        }

        // Transitional fallback. In some hosting flows (e.g. the IIS integrated pipeline, where the web span is
        // re-activated for the MVC action on another execution context) the ambient value set when the request
        // began is not visible, but the root web span is still reachable through the active scope.
        // Removed together with the spans.
        var rootSpan = Tracer.Instance.InternalActiveScope?.Root?.Span;
        if (rootSpan is { Type: SpanTypes.Web } && rootSpan.Context?.TraceContext is { } traceContext)
        {
            return new RequestState(rootSpan, traceContext.IastRequestContext);
        }

        return null;
    }

    private sealed class RequestState
    {
        public RequestState(Span rootSpan, IastRequestContext? context)
        {
            RootSpan = rootSpan;
            Context = context;
        }

        public Span RootSpan { get; }

        public IastRequestContext? Context { get; }
    }
}
