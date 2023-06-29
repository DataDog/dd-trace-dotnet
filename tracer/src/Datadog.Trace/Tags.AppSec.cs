// <copyright file="Tags.AppSec.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace;

/// <summary>
/// Standard AppSec span tags used by integrations.
/// </summary>
public static partial class Tags
{
    internal static class AppSec
    {
        internal const string Events = "appsec.events.";

        internal static string Track(string eventName) => $"{Events}{eventName}.track";

        internal static class EventsUsers
        {
            internal const string EventsUsersRoot = Events + "users";

            internal static class LoginEvent
            {
                private const string Root = EventsUsersRoot + ".login";
                internal const string Success = Root + ".success";
                internal const string SuccessSdkSource = $"_dd.{Success}.sdk";
                internal const string SuccessAutoMode = $"_dd.{Success}.auto.mode";
                internal const string SuccessTrack = Success + ".track";

                internal const string Failure = Root + ".failure";
                internal const string FailureUserId = Failure + ".usr.id";
                internal const string FailureUserExists = Failure + ".usr.exists";
                internal const string FailureEmail = Failure + ".email";
                internal const string FailureUserName = Failure + ".username";
                internal const string FailureAutoMode = $"_dd.{Failure}.auto.mode";
                internal const string FailureSdkSource = $"_dd.{Failure}.sdk";
                internal const string FailureTrack = Failure + ".track";
            }

            internal static class SignUpEvent
            {
                private const string Root = EventsUsersRoot + ".signup";
                private const string Success = Root + ".success";
                internal const string SuccessUserId = Root + ".usr.id";
                internal const string SuccessEmail = Root + ".usr.email";
                internal const string SuccessUserName = Root + ".usr.username";

                internal const string SuccessAutoMode = $"_dd.{Success}.auto.mode";
                internal const string SuccessTrack = Success + ".track";

                private const string RootFailure = Root + ".failure";
                internal const string FailureUserId = RootFailure + ".usr.id";
                internal const string FailureEmail = RootFailure + ".usr.email";
                internal const string FailureUserName = RootFailure + ".usr.username";
                internal const string FailureAutoMode = $"_dd.{RootFailure}.auto.mode";
                internal const string FailureTrack = RootFailure + ".track";
            }
        }
    }
}
