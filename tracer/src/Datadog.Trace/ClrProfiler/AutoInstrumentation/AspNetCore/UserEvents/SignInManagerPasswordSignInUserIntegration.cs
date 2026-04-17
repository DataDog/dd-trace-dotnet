// <copyright file="SignInManagerPasswordSignInUserIntegration.cs" company="Datadog">
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
/// SignInManagerIntegration for when the user has been found, but need to check password
/// </summary>
[InstrumentMethod(
    AssemblyName = AssemblyName,
    TypeName = "Microsoft.AspNetCore.Identity.SignInManager`1",
    MethodName = "PasswordSignInAsync",
    ParameterTypeNames = ["!0", ClrNames.String, ClrNames.Bool, ClrNames.Bool],
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
        if (Security.Instance is { IsTrackUserEventsEnabled: true } security && state.Scope is { Span: Span span })
        {
            var userExists = (state.State as IDuckType)?.Instance is not null;
            if (state.State is not IIdentityUser user)
            {
                UserEventsCommon.RecordMetricsLoginFailureIfNotFound(false, false);
                return returnValue;
            }

            var foundUserId = false;
            var foundLogin = false;
            Func<string, string?>? processPii = null;
            string autoMode;
            if (security.IsAnonUserTrackingMode)
            {
                processPii = UserEventsCommon.Anonymize;
                autoMode = SecuritySettings.UserTrackingAnonMode;
            }
            else
            {
                autoMode = SecuritySettings.UserTrackingIdentMode;
            }

            var setTag = TaggingUtils.GetSpanSetter(span, out _);
            var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);
            if (!returnValue.Succeeded)
            {
                setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureTrack, "true");
                setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureAutoMode, autoMode);

                var userId = UserEventsCommon.GetId(user);
                var userLogin = UserEventsCommon.GetLogin(user);
                if (!StringUtil.IsNullOrEmpty(userId))
                {
                    foundUserId = true;
                    userId = processPii?.Invoke(userId) ?? userId;
                    setTag(Tags.AppSec.EventsUsers.InternalUserId, userId);
                    setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserId, userId);
                }

                if (!StringUtil.IsNullOrEmpty(userLogin))
                {
                    foundLogin = true;
                    var login = processPii?.Invoke(userLogin) ?? userLogin;
                    setTag(Tags.AppSec.EventsUsers.InternalLogin, login);
                    tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserLogin, login);
                }

                var duckCast = instance.TryDuckCast<ISignInManager>(out var value);
                if (duckCast && value is not null)
                {
                    var httpContext = value.Context;
                    var securityCoordinator = SecurityCoordinator.Get(security, span, httpContext!);
                    securityCoordinator.Reporter.CollectHeaders();
                    UserEventsCommon.RecordMetricsLoginFailureIfNotFound(foundUserId, foundLogin);
                    tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserExists, userExists ? Tags.AppSec.EventsUsers.True : Tags.AppSec.EventsUsers.False);
                    if (userLogin is not null)
                    {
                        // userId must not be provided on login failure
                        var result = securityCoordinator.RunWafForUser(userLogin: userLogin, otherTags: new() { { AddressesConstants.UserBusinessLoginFailure, string.Empty } });
                        securityCoordinator.BlockAndReport(result);
                    }
                }
            }
#if !NETCOREAPP3_1_OR_GREATER
            else if (userExists)
            {
                // AuthenticatedHttpcontExtextensions should fill these, but on core <3.1 email doesn't appear in claims
                // so let's try to fill these up here if we have the chance to come here
                var userLogin = UserEventsCommon.GetLogin(user);

                if (!string.IsNullOrEmpty(userLogin))
                {
                    var login = processPii?.Invoke(userLogin!) ?? userLogin!;
                    setTag(Tags.AppSec.EventsUsers.InternalLogin, login);
                    tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessLogin, login);
                }
            }
#endif

            security.SetTraceSamplingPriority(span);
        }

        return returnValue;
    }
}
#endif
