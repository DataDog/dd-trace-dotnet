// <copyright file="Log4NetDuckTypingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission;
using FluentAssertions;
using log4net.Appender;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Log4Net
{
    public class Log4NetDuckTypingTests
    {
        [Fact]
        public void CanIncreaseSizeOfIAppenderArray()
        {
            var appenders = new IAppender[] { };
            var success = Log4NetCommon<IAppender[]>.TryAddAppenderToResponse(
                appenders,
                new DirectSubmissionLog4NetAppender(null, 0),
                out var results);

            success.Should().BeTrue();
            results.Length.Should().Be(1);
        }

        [Fact]
        public void CanWriteLogToAppender()
        {
            var appenders = new IAppender[] { };
            var success = Log4NetCommon<IAppender[]>.TryAddAppenderToResponse(
                appenders,
                new DirectSubmissionLog4NetAppender(null, 0),
                out var results);

            success.Should().BeTrue();
            results.Length.Should().Be(1);

            foreach (var result in results)
            {
                result.Name.Should().Be("Datadog");
            }
        }
    }
}
