// <copyright file="IDirectSubmissionLogSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Logging.DirectSubmission.Sink
{
    internal interface IDirectSubmissionLogSink
    {
        /// <summary>
        /// Emit the provided log event to the sink. If the sink is being disposed or
        /// the app domain unloaded, then the event is ignored.
        /// </summary>
        /// <param name="logEvent">Log event to emit.</param>
        /// <exception cref="ArgumentNullException">The event is null.</exception>
        void EnqueueLog(DirectSubmissionLogEvent logEvent);

        /// <summary>
        /// Start the background process to send logs to the backend
        /// </summary>
        void Start();

        /// <summary>
        /// Flushes the sink
        /// </summary>
        Task FlushAsync();

        /// <summary>
        /// Disposes the instance asynchronously
        /// </summary>
        Task DisposeAsync();
    }
}
