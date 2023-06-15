// <copyright file="SignInHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.UserEvents;

internal static class SignInHelper
{
    internal static void FillSpanWithFailureLoginEvent<T>(Security security, in CallTargetState state, T returnValue, bool userExist = false)
        where T : ISignInResult
    {
        var span = state.Scope.Span;
        var setTag = TaggingUtils.GetSpanSetter(span, out _);
        var tryAddTag = TaggingUtils.GetSpanSetter(span, out _, replaceIfExists: false);

        setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureTrack, "true");
        setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureAutoMode, security.Settings.UserEventsAutomatedTracking);
        tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserExists, userExist ? "true" : "false");
        if (state.State is Guid || Guid.TryParse(state.State?.ToString(), out _))
        {
            tryAddTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserId, state.State!.ToString());
        }

        security.SetTraceSamplingPriority(span);
    }
}
