// <copyright file="SignInManagerPasswordSignInUserIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

#if !NETFRAMEWORK
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

/// <summary>
/// SignInManagerIntegration for when the user has been found, but need to check password
/// </summary>
[InstrumentMethod(
    AssemblyName = AssemblyName,
    TypeName = "Microsoft.AspNetCore.Identity.SignInManager`1",
    MethodName = "PasswordSignInAsync",
    ParameterTypeNames = new[] { "!0", ClrNames.String, ClrNames.Bool, ClrNames.Bool },
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Identity.SignInResult]",
    MinimumVersion = "2",
    MaximumVersion = "8",
    IntegrationName = nameof(IntegrationId.AspNetCore),
    InstrumentationCategory = InstrumentationCategory.AppSec)]
[InstrumentMethod(
    AssemblyName = AssemblyName,
    TypeName = "Microsoft.AspNetCore.Identity.SignInManager`1",
    MethodName = "PasswordSignInAsync",
    ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, ClrNames.Bool, ClrNames.Bool },
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Identity.SignInResult]",
    MinimumVersion = "2",
    MaximumVersion = "8",
    IntegrationName = nameof(IntegrationId.AspNetCore),
    CallTargetIntegrationKind = CallTargetKind.Derived,
    InstrumentationCategory = InstrumentationCategory.AppSec)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SignInManagerPasswordSignInUserIntegration
{
    private const string AssemblyName = "Microsoft.AspNetCore.Identity";

    internal static CallTargetState OnMethodBegin<TTarget, TUser>(TTarget instance, TUser user, string password, bool isPersistent, bool lockoutOnFailure)
        where TUser : IIdentityUser
    {
        var security = Security.Instance;
        if (security.IsTrackUserEventsEnabled)
        {
            var tracer = Tracer.Instance;
            var scope = tracer.InternalActiveScope;
            return new CallTargetState(scope, user);
        }

        return CallTargetState.GetDefault();
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        where TReturn : ISignInResult
    {
        if (Security.Instance is { IsTrackUserEventsEnabled: true } security && state.Scope is { Span: { } span })
        {
            var userExists = (state.State as IDuckType)?.Instance is not null;
            var user = state.State as IIdentityUser;
            var id = UserEventsCommon.GetId(user);

            if (id == null)
            {
                TelemetryFactory.Metrics.RecordCountMissingUserId(MetricTags.AuthenticationFramework.AspNetCoreIdentity);
                return returnValue;
            }

            var setTag = TaggingUtils.GetSpanSetter(span, out _);
            var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);
            if (!returnValue.Succeeded)
            {
                setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureTrack, "true");
                setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureAutoMode, security.Settings.UserEventsAutoInstrumentationMode);
                tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserExists, userExists ? "true" : "false");

                if (security.IsAnonUserTrackingMode)
                {
                    var anonId = UserEventsCommon.GetAnonId(id);
                    tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserId, anonId);
                }
                else
                {
                    tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserId, id);
                }
            }
            else if (userExists)
            {
                // AuthenticatedHttpcontExtextensions should fill these, but on core <3.1 email doesnt appear in claims
                // so let's try to fill these up here if we have the chance to come here
                if (security.IsAnonUserTrackingMode)
                {
                    var anonId = UserEventsCommon.GetAnonId(id);
                    tryAddTag(Tags.User.Id, anonId);
                }
                else
                {
                    tryAddTag(Tags.User.Id, id);
                }
            }

            security.SetTraceSamplingPriority(span);
        }

        return returnValue;
    }
}
#endif
