// <copyright file="SignInManagerCheckPasswordSignInAsync.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if !NETFRAMEWORK
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Aspects.System.Web.Extensions;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

/// <summary>
/// CheckPasswordSignInAsync(TUser,System.String,System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Identity",
    TypeName = "Microsoft.AspNetCore.Identity.SignInManager`1",
    MethodName = "CheckPasswordSignInAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Identity.SignInResult]",
    ParameterTypeNames = ["!0", ClrNames.String, ClrNames.Bool],
    MinimumVersion = "2",
    MaximumVersion = "8",
    IntegrationName = nameof(IntegrationId.AspNetCore),
    InstrumentationCategory = InstrumentationCategory.AppSec)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SignInManagerCheckPasswordSignInAsync
{
    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        where TReturn : ISignInResult
    {
        // Fake return value: Blocking by authentication
        if (Security.Instance.Enabled && CoreHttpContextStore.Instance.Get() is { } httpContext)
        {
            var transport = new SecurityCoordinator.HttpTransport(httpContext);
            if (transport.IsBlockedByAuthentication)
            {
                var tracer = Tracer.Instance;
                var scope = tracer.InternalActiveScope;
                var span = scope.Span;
                var setTag = TaggingUtils.GetSpanSetter(span);
                setTag(Tags.AppSec.EventsUsers.LoginEvent.Blocked, "true");

                if (returnValue.Succeeded)
                {
                    setTag(Tags.AppSec.EventsUsers.LoginEvent.BlockedWithSuccess, "true");
                }

                return (TReturn)returnValue.Failed;
            }
        }

        return returnValue;
    }
}
#endif
