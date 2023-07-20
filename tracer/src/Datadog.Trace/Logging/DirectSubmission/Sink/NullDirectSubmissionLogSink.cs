// <copyright file="NullDirectSubmissionLogSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

#nullable enable

namespace Datadog.Trace.Logging.DirectSubmission.Sink
{
    internal class NullDirectSubmissionLogSink : IDirectSubmissionLogSink
    {
        public void EnqueueLog(DirectSubmissionLogEvent logEvent)
        {
        }

        public void Start()
        {
        }

        public Task FlushAsync()
        {
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
