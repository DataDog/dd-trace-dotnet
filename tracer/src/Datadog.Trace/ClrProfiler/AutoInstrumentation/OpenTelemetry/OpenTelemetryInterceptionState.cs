// <copyright file="OpenTelemetryInterceptionState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.OpenTelemetry
{
    /// <summary>
    /// Thread-local stash used by the OTel <c>StartActiveSpan(parent)</c> / <c>StartSpan(parent)</c>
    /// integrations to pass the in-process parent <see cref="Scope"/> through to
    /// <c>ActivityStartIntegration</c>. The OTel SDK lowers the <c>TelemetrySpan</c> parent into an
    /// <c>ActivityContext</c> before calling <c>ActivitySource.StartActivity</c>, which loses the
    /// in-process parent reference; capturing the parent at the API boundary preserves it.
    /// <para>
    /// Uses <c>[ThreadStatic]</c> rather than <c>AsyncLocal</c> because the entire chain
    /// (<c>StartActiveSpan</c> → <c>ActivitySource.StartActivity</c> → <c>Activity.Start</c> →
    /// <c>ActivityStartIntegration.OnMethodEnd</c>) runs synchronously on the calling thread.
    /// </para>
    /// </summary>
    internal static class OpenTelemetryInterceptionState
    {
        [System.ThreadStatic]
        private static Stack<Scope?>? _explicitParentScopes;

        /// <summary>Pushes the given parent scope onto the thread-local stack.</summary>
        internal static void PushExplicitParent(Scope? scope)
            => (_explicitParentScopes ??= new Stack<Scope?>()).Push(scope);

        /// <summary>Pops the most recently pushed parent scope; returns null if the stack is empty.</summary>
        internal static Scope? PopExplicitParent()
            => _explicitParentScopes is { Count: > 0 } stack ? stack.Pop() : null;

        /// <summary>
        /// Peeks the most recently pushed parent scope without removing it; returns null if the stack
        /// is empty. Used by <c>ActivityStartIntegration</c> so that the OTel-API integration's
        /// <c>OnMethodEnd</c> remains responsible for popping (keeps the lifecycle co-located and
        /// keeps the stack consistent if <c>Activity.Start</c> throws).
        /// </summary>
        internal static Scope? PeekExplicitParent()
            => _explicitParentScopes is { Count: > 0 } stack ? stack.Peek() : null;
    }
}
