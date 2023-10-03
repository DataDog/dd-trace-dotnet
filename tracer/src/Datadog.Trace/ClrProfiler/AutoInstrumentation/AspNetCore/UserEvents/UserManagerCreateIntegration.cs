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
        if (security.TrackUserEvents)
        {
            var span = state.Scope.Span;
            var setTag = TaggingUtils.GetSpanSetter(span, out _);
            var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);

            if (returnValue.Succeeded)
            {
                setTag(Tags.AppSec.EventsUsers.SignUpEvent.SuccessTrack, "true");
                setTag(Tags.AppSec.EventsUsers.SignUpEvent.SuccessAutoMode, security.Settings.UserEventsAutomatedTracking);
                if (security.IsExtendedUserTrackingEnabled)
                {
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.SuccessUserId, user!.Id?.ToString());
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.SuccessEmail, user.Email);
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.SuccessUserName, user.UserName);
                }
                else if (user?.Id is Guid || Guid.TryParse(user?.Id?.ToString(), out _))
                {
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.SuccessUserId, user!.Id!.ToString());
                }
            }
            else
            {
                setTag(Tags.AppSec.EventsUsers.SignUpEvent.FailureTrack, "true");
                setTag(Tags.AppSec.EventsUsers.SignUpEvent.FailureAutoMode, security.Settings.UserEventsAutomatedTracking);
                if (security.IsExtendedUserTrackingEnabled)
                {
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.FailureUserId, user!.Id?.ToString());
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.FailureEmail, user.Email);
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.FailureUserName, user.UserName);
                }
                else if (user?.Id is Guid || Guid.TryParse(user?.Id?.ToString(), out _))
                {
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.FailureUserId, user!.Id!.ToString());
                }
            }

            security.SetTraceSamplingPriority(span);
        }

        return returnValue;
    }
}
#endif
