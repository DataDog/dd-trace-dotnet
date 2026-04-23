// <copyright file="UserManagerCreateIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
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
    ParameterTypeNames = ["!0"],
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Identity.IdentityResult]",
    MinimumVersion = "2",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = nameof(IntegrationId.AspNetCore),
    InstrumentationCategory = InstrumentationCategory.AppSec)]
[InstrumentMethod(
    AssemblyName = AssemblyName,
    TypeName = "Microsoft.AspNetCore.Identity.UserManager`1",
    MethodName = "CreateAsync",
    ParameterTypeNames = ["!0"],
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Identity.IdentityResult]",
    MinimumVersion = "2",
    MaximumVersion = SupportedVersions.LatestDotNet,
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
        if (security.IsTrackUserEventsEnabled)
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
        if (security.IsTrackUserEventsEnabled && state.Scope is { Span: Span span })
        {
            if (returnValue.Succeeded)
            {
                var userId = UserEventsCommon.GetId(user);
                var userLogin = UserEventsCommon.GetLogin(user);
                var foundUserId = !string.IsNullOrEmpty(userId);
                var foundLogin = !string.IsNullOrEmpty(userLogin);
                UserEventsCommon.RecordMetricsSignupIfNotFound(foundUserId, foundLogin);
                Func<string, string>? processPii = null;
                string successAutoMode;

                if (security.IsAnonUserTrackingMode)
                {
                    processPii = UserEventsCommon.Anonymize;
                    successAutoMode = SecuritySettings.UserTrackingAnonMode;
                }
                else
                {
                    successAutoMode = SecuritySettings.UserTrackingIdentMode;
                }

                string? processedUserId = null;
                string? processedUserLogin = null;

                var setTag = TaggingUtils.GetSpanSetter(span, out _);
                var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);

                setTag(Tags.AppSec.EventsUsers.SignUpEvent.Track, "true");
                setTag(Tags.AppSec.EventsUsers.SignUpEvent.AutoMode, successAutoMode);

                if (foundUserId)
                {
                    processedUserId = processPii?.Invoke(userId!) ?? userId!;
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.UserId, processedUserId);
                    tryAddTag(Tags.AppSec.EventsUsers.InternalUserId, processedUserId);
                }

                if (foundLogin)
                {
                    processedUserLogin = processPii?.Invoke(userLogin!) ?? userLogin!;
                    tryAddTag(Tags.AppSec.EventsUsers.SignUpEvent.Login, processedUserLogin);
                    tryAddTag(Tags.AppSec.EventsUsers.InternalLogin, processedUserLogin);
                }

                security.SetTraceSamplingPriority(span);
                var securityCoordinator = SecurityCoordinator.TryGet(security, span);
                if (securityCoordinator.HasValue)
                {
                    securityCoordinator.Value.Reporter.CollectHeaders();
                    var result = securityCoordinator.Value.RunWafForUser(
                        userId: processedUserId,
                        userLogin: processedUserLogin,
                        otherTags: new Dictionary<string, string> { { AddressesConstants.UserBusinessSignup, string.Empty } });
                    securityCoordinator.Value.BlockAndReport(result);
                }
            }
        }

        return returnValue;
    }
}
#endif
