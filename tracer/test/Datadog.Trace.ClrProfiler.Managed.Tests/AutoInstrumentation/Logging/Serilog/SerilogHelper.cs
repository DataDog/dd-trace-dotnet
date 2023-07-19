// <copyright file="SerilogHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Vendors.Serilog.Capturing;
using Datadog.Trace.Vendors.Serilog.Core;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Serilog
{
    internal static class SerilogHelper
    {
        public static MessageTemplateProcessor GetSerilogMessageProcessor()
        {
            // Use some "default" values
            return new MessageTemplateProcessor(
                new PropertyValueConverter(
                    maximumDestructuringDepth: 10,
                    maximumStringLength: int.MaxValue,
                    maximumCollectionCount: int.MaxValue,
                    additionalScalarTypes: Enumerable.Empty<Type>(),
                    additionalDestructuringPolicies: Enumerable.Empty<IDestructuringPolicy>(),
                    propagateExceptions: false));
        }

        internal class TestSink : IDirectSubmissionLogSink
        {
            public ConcurrentQueue<DirectSubmissionLogEvent> Events { get; } = new();

            public void EnqueueLog(DirectSubmissionLogEvent logEvent)
            {
                Events.Enqueue(logEvent);
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
}
