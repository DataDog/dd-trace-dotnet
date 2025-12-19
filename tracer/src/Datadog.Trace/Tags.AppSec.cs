// <copyright file="Tags.AppSec.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace;

/// <summary>
/// Standard AppSec span tags used by integrations.
/// </summary>
internal static partial class Tags
{
    internal static class AppSec
    {
        internal const string Events = "appsec.events.";

        internal static string Track(string eventName) => $"{Events}{eventName}.track";

        internal static class EventsUsers
        {
            private const string EventsUsersRoot = Events + "users";
            private const string PropagatedPrefix = "_dd.appsec.usr";
            internal const string CollectionMode = "_dd.appsec.user.collection_mode";
            internal const string InternalUserId = $"{PropagatedPrefix}.id";
            internal const string InternalLogin = $"{PropagatedPrefix}.login";
            internal const string True = "true";
            internal const string False = "false";
            internal const string Sdk = "sdk";

            internal static class LoginEvent
            {
                private const string Root = EventsUsersRoot + ".login";
                internal const string Success = Root + ".success";
                internal const string SuccessSdkSource = $"_dd.{Success}.sdk";
                internal const string SuccessAutoMode = $"_dd.{Success}.auto.mode";
                internal const string SuccessTrack = Success + ".track";
                internal const string SuccessLogin = Success + ".usr.login";

                internal const string Failure = Root + ".failure";
                internal const string FailureUserId = Failure + ".usr.id";
                internal const string FailureUserLogin = Failure + ".usr.login";
                internal const string FailureUserExists = Failure + ".usr.exists";
                internal const string FailureAutoMode = $"_dd.{Failure}.auto.mode";
                internal const string FailureSdkSource = $"_dd.{Failure}.sdk";
                internal const string FailureTrack = Failure + ".track";
            }

            internal static class SignUpEvent
            {
                private const string Root = EventsUsersRoot + ".signup";
                internal const string UserId = Root + ".usr.id";

                /// <summary>
                /// In the case of aspnet core it will come down to username which is supposed to be unique (unless custom db is provided)
                /// </summary>
                internal const string Login = Root + ".usr.login";

                internal const string AutoMode = $"_dd.{Root}.auto.mode";
                internal const string Track = Root + ".track";
            }
        }
    }
}
