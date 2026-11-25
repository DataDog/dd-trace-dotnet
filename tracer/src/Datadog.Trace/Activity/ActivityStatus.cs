// <copyright file="ActivityStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Activity.DuckTypes;

namespace Datadog.Trace.Activity
{
    /// <summary>
    /// An Activity status read from <see cref="IActivity6"/>.
    /// <c>default</c> means Unset, which is also what shapes without a Status report.
    /// </summary>
    internal readonly struct ActivityStatus
    {
        public readonly ActivityStatusCode Code;
        public readonly string? Description;

        public ActivityStatus(ActivityStatusCode code, string? description)
        {
            Code = code;
            Description = description;
        }

        /// <summary>
        /// Reads the status, and the description only when the status is Error.
        /// </summary>
        public static ActivityStatus From<T>(T activity)
            where T : IActivity6
        {
            var code = activity.Status;
            return code == ActivityStatusCode.Error
                       ? new ActivityStatus(code, activity.StatusDescription)
                       : new ActivityStatus(code, null);
        }
    }
}
