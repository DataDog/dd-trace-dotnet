// <copyright file="SignInManagerPasswordSignInIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

/// <summary>
/// this integration is for a first entry point password sign in method, and we only wanna track the failed result. Indeed if it succeeds,
/// it's the other integration that will take care of it as we will be able to get ahold of the user object in the other one
/// </summary>
[InstrumentMethod(
    AssemblyName = AssemblyName,
    TypeName = "Microsoft.AspNetCore.Identity.SignInManager`1",
    MethodName = "PasswordSignInAsync",
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.Bool, ClrNames.Bool],
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Identity.SignInResult]",
    MinimumVersion = "2",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = nameof(IntegrationId.AspNetCore),
    InstrumentationCategory = InstrumentationCategory.AppSec)]
[InstrumentMethod(
    AssemblyName = AssemblyName,
    TypeName = "Microsoft.AspNetCore.Identity.SignInManager`1",
    MethodName = "PasswordSignInAsync",
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.Bool, ClrNames.Bool],
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Identity.SignInResult]",
    MinimumVersion = "2",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = nameof(IntegrationId.AspNetCore),
    CallTargetIntegrationKind = CallTargetKind.Derived,
    InstrumentationCategory = InstrumentationCategory.AppSec)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SignInManagerPasswordSignInIntegration
{
    private const string AssemblyName = "Microsoft.AspNetCore.Identity";

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string user, string password, bool isPersistent, bool lockoutOnFailure)
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
        if (!returnValue.Succeeded
         && Security.Instance is { IsTrackUserEventsEnabled: true } security
         && state is { Scope.Span: Span span })
        {
            // the new user semantics events must only be collected if either the user login or the user ID are available
            // here as it's the first login step, state.State is the username, db hasn't been hit yet.
            if (state.State is not string login || string.IsNullOrEmpty(login))
            {
                UserEventsCommon.RecordMetricsLoginFailureIfNotFound(true, foundLogin: false);
                return returnValue;
            }

            var setTag = TaggingUtils.GetSpanSetter(span, out _);
            var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);

            setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureTrack, Tags.AppSec.EventsUsers.True);
            tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserExists, Tags.AppSec.EventsUsers.False);
            var processedLogin = login;
            if (security.IsAnonUserTrackingMode)
            {
                processedLogin = UserEventsCommon.Anonymize(login);
                tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserLogin, processedLogin);
                setTag(Tags.AppSec.EventsUsers.InternalLogin, processedLogin);
                setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureAutoMode, SecuritySettings.UserTrackingAnonMode);
            }
            else
            {
                tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserLogin, login);
                setTag(Tags.AppSec.EventsUsers.InternalLogin, login);
                setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureAutoMode, SecuritySettings.UserTrackingIdentMode);
            }

            var duckCast = instance.TryDuckCast<ISignInManager>(out var value);
            if (duckCast && value is not null)
            {
                var httpContext = value.Context;
                var securityCoordinator = SecurityCoordinator.Get(security, span, httpContext!);
                securityCoordinator.Reporter.CollectHeaders();
                security.SetTraceSamplingPriority(span);

                if (security.AddressEnabled(AddressesConstants.UserLogin))
                {
                    var result = securityCoordinator.RunWafForUser(userLogin: processedLogin, otherTags: new() { { AddressesConstants.UserBusinessLoginFailure, string.Empty } });
                    securityCoordinator.BlockAndReport(result);
                }
            }
        }

        return returnValue;
    }
}
#endif
