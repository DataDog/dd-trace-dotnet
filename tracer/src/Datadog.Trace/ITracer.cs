// <copyright file="ITracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace
{
    /// <summary>
    /// The tracer is responsible for creating spans and flushing them to the Datadog agent
    /// </summary>
    public interface ITracer
    {
        /// <summary>
        /// Gets the active scope
        /// </summary>
        IScope ActiveScope { get; }

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        ImmutableTracerSettings Settings { get; }

        /// <summary>
        /// This creates a new span with the given parameters and makes it active.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <returns>A scope wrapping the newly created span</returns>
        IScope StartActive(string operationName);

        /// <summary>
        /// This creates a new span with the given parameters and makes it active.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <param name="settings">Settings for the new <see cref="IScope"/></param>
        /// <returns>A scope wrapping the newly created span</returns>
        IScope StartActive(string operationName, SpanCreationSettings settings);

        /// <summary>
        /// Sets the details of the user on the local root span
        /// </summary>
        /// <param name="email">The user's email</param>
        /// <param name="name">The user's name</param>
        /// <param name="id">The user's id</param>
        /// <param name="sessionId">The user's sessionId</param>
        /// <param name="role">The user's role</param>
        public void SetUser(
            string email = null,
            string name = null,
            string id = null,
            string sessionId = null,
            string role = null);
    }
}
