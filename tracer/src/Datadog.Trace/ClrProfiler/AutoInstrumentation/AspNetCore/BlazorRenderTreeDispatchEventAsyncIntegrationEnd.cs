// <copyright file="BlazorRenderTreeDispatchEventAsyncIntegrationEnd.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

/// <summary>
/// The ASP.NET Core middleware integration.
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Components",
    TypeName = "Microsoft.AspNetCore.Components.RenderTree.Renderer",
    MethodName = "DispatchEventAsync",
    ParameterTypeNames = new[] { ClrNames.UInt64, "Microsoft.AspNetCore.Components.RenderTree.EventFieldInfo", "System.EventArgs" },
    ReturnTypeName = ClrNames.Task,
    MinimumVersion = "7",
    MaximumVersion = "7",
    IntegrationName = nameof(IntegrationId.AspNetCore))]
// [InstrumentMethod(
//     AssemblyName = "Microsoft.AspNetCore.Components",
//     TypeName = "Microsoft.AspNetCore.Components.RenderTree.Renderer ",
//     MethodName = "DispatchEventAsync",
//     ReturnTypeName = ClrNames.Task,
//     ParameterTypeNames = new[] { ClrNames.UInt64, "Microsoft.AspNetCore.Components.RenderTree.EventFieldInfo", "System.EventArgs" },
//     MinimumVersion = "7",
//     MaximumVersion = "7",
//     IntegrationName = nameof(IntegrationId.AspNetCore),
//     CallTargetIntegrationType = IntegrationType.Derived)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class BlazorRenderTreeDispatchEventAsyncIntegrationEnd
{
    private const string ChangeEventArgs = "ChangeEventArgs";
    private const string MouseEventArgs = "MouseEventArgs";

    internal static CallTargetState OnMethodBegin<TTarget, TEventFieldInfo>(TTarget instance, ulong eventHandlerId, in TEventFieldInfo fieldInfo, in System.EventArgs eventArgs)
        where TTarget : IRendererProxy, IDuckType
    {
        var eventType = eventArgs?.GetType().Name;

        // skip some events, as don't want to generate too much data
        string eventDescription;
        switch (eventType)
        {
            case ChangeEventArgs:
                eventDescription = "change_event";
                break;
            case MouseEventArgs:
                eventDescription = eventArgs.DuckCast<EventArgsProxy>().EventType;
                break;
            default:
                return CallTargetState.GetDefault();
        }

        string component = null;
        string resourceName = null;
        foreach (DictionaryEntry binding in instance.EventBindings)
        {
            if (eventHandlerId == (ulong)binding.Key)
            {
                // found the event binding, so break no matter what;
                // // TODO: use duck typing. We can't current
                // _fieldInfo ??= typeof(TEventFieldInfo)
                //            .Assembly
                //            .GetType("Microsoft.AspNetCore.Components.EventCallback")
                //            .GetField("Receiver", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var eventCallback = binding.Value.DuckCast<IEventCallback>();
                if (eventCallback.Receiver is { } componentObject)
                {
                    // FullName or ShortName?
                    component = componentObject.GetType().FullName;
                    resourceName = $"{eventDescription} {component}";
                }

                // component = _fieldInfo.GetValue(binding.Value)?.GetType().FullName;
                // resourceName = $"{eventDescription} {component}";

                // var eventCallback = binding.Value.DuckCast<IEventCallback>();
                // if (eventCallback.Receiver is { } component)
                // {
                //     resourceName = $"{eventType} {component.GetType().Name}";
                // }

                break;
            }
        }

        // This extra duck type is a bit annoying. The instance is a RemoteRenderer, which is where
        // the connection ID is, but the method we're instrumenting is in the abstract base class, Render.
        var connectionId = instance.Instance.DuckCast<RemoteRendererProxy>().CircuitClientProxyProxy.ConnectionId;
        var tags = new AspNetCoreBlazorTags
        {
            EventType = eventDescription,
            Component = component,
            ConnectionId = connectionId
        };
        var scope = Tracer.Instance.StartActiveInternal("blazor.render", tags: tags);
        var span = scope.Span;
        span.ResourceName = resourceName ?? eventType;
        span.Type = SpanTypes.Web;
        return new CallTargetState(scope);
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return new CallTargetReturn<TReturn>(returnValue);
    }
}
