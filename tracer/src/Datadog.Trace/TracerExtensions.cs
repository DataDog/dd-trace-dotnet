// <copyright file="TracerExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// Extension methods for the current <see cref="Tracer"/> class
    /// </summary>
    public static class TracerExtensions
    {
        /// <summary>
        /// Sets the details of the user on the local root span
        /// </summary>
        /// <param name="tracer">The tracer object, that controls the datadog tracer</param>
        /// <param name="userDetails">An object that specifies the details of the current user</param>
        public static void SetUser(this Tracer tracer, UserDetails userDetails)
        {
            tracer.LocalRootSpan.SetUser(userDetails);
        }
    }
}
