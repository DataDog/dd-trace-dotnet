// <copyright file="SpanExtensionsSetUserIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Extensions;

/// <summary>
/// System.Void Datadog.Trace.SpanExtensions::SetUser(Datadog.Trace.ISpan,System.String,System.String,System.String,System.Boolean,System.String,System.String,System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.SpanExtensions",
    MethodName = "SetUser",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Datadog.Trace.ISpan", ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.Bool, ClrNames.String, ClrNames.String, ClrNames.String },
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SpanExtensionsSetUserIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TSpan>(ref TSpan span, string? email, string? name, string id, bool propagateId, string? sessionId, string? role, string? scope)
    {
        // Annoyingly, this takes an ISpan, so we have to do some duckTyping to make it work
        ISpan? realSpan = null;

        // it's most likely to be a duck-typed Span, so try that first
        if (span is IDuckType { Instance: Span s })
        {
            realSpan = s;
        }
        else if (span is Span autoSpan)
        {
            // Not likely, but technically possible for this to happen
            realSpan = autoSpan;
        }
        else
        {
            // This is a worst case, should basically never be necessary
            // Only required if customers create a custom ISpan
            // Should we handle it? I chose to just ignore it here because it's a pain
            // but we could throw, or log?
        }

        realSpan?.SetUserInternal(
            new UserDetails(id)
            {
                Email = email,
                Name = name,
                PropagateId = propagateId,
                SessionId = sessionId,
                Role = role,
                Scope = scope
            });

        return CallTargetState.GetDefault();
    }
}
