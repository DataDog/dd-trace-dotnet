// <copyright file="UserManagerCreateIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

#if !NETFRAMEWORK
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

/// <summary>
/// UserManagerCreateIntegration for sign up events
/// </summary>
[InstrumentMethod(
    AssemblyName = AssemblyName,
    TypeName = "Microsoft.AspNetCore.Identity.UserManager`1",
    MethodName = "CreateAsync",
    ParameterTypeNames = new[] { "!0" },
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Identity.IdentityResult]",
    MinimumVersion = "2",
    MaximumVersion = "8",
    IntegrationName = nameof(IntegrationId.AspNetCore),
    InstrumentationCategory = InstrumentationCategory.AppSec)]
[InstrumentMethod(
    AssemblyName = AssemblyName,
    TypeName = "Microsoft.AspNetCore.Identity.UserManager`1",
    MethodName = "CreateAsync",
    ParameterTypeNames = new[] { "!0" },
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Identity.IdentityResult]",
    MinimumVersion = "2",
    MaximumVersion = "8",
    IntegrationName = nameof(IntegrationId.AspNetCore),
    CallTargetIntegrationKind = CallTargetKind.Derived,
    InstrumentationCategory = InstrumentationCategory.AppSec)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class UserManagerCreateIntegration
{
    private const string AssemblyName = "Microsoft.Extensions.Identity.Core";

    internal static CallTargetState OnMethodBegin<TTarget, TUser>(TTarget instance, TUser user)
        where TUser : IIdentityUser
    {
        var security = Security.Instance;
        if (security.TrackUserEvents)
        {
            var tracer = Tracer.Instance;
            var scope = tracer.InternalActiveScope;
            return new CallTargetState(scope, user);
        }

        return CallTargetState.GetDefault();
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        where TReturn : IIdentityResult
    {
        var security = Security.Instance;
        var user = state.State as IIdentityUser;
        if (security.TrackUserEvents
            && state.Scope is { Span: { } span }
            && UserEventsCommon.GetId(user) is { } id)
        {
            var setTag = TaggingUtils.GetSpanSetter(span, out _);
            var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);

            if (returnValue.Succeeded)
            {
                setTag(Tags.AppSec.EventsUsers.SignUpEvent.SuccessTrack, "true");
                setTag(Tags.AppSec.EventsUsers.SignUpEvent.SuccessAutoMode, security.Settings.UserEventsAutoInstrumentationMode);
                if (security.IsAnonUserTrackingMode)
                {
                    var anonId = UserEventsCommon.GetAnonId(id);
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.SuccessUserId, anonId);
                }
                else
                {
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.SuccessUserId, id);
                }
            }
            else
            {
                setTag(Tags.AppSec.EventsUsers.SignUpEvent.FailureTrack, "true");
                setTag(Tags.AppSec.EventsUsers.SignUpEvent.FailureAutoMode, security.Settings.UserEventsAutoInstrumentationMode);
                if (security.IsAnonUserTrackingMode)
                {
                    var anonId = UserEventsCommon.GetAnonId(id);
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.FailureUserId, anonId);
                }
                else
                {
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.FailureUserId, id);
                }
            }

            security.SetTraceSamplingPriority(span);
        }

        return returnValue;
    }
}
#endif
