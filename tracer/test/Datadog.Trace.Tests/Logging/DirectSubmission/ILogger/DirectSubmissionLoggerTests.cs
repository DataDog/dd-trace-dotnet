// <copyright file="DirectSubmissionLoggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if NETCOREAPP3_1_OR_GREATER
using System.Text.Json;
#endif
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tests.Logging.DirectSubmission.Sink;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.ILogger;

public class DirectSubmissionLoggerTests
{
    private static readonly BatchingSinkOptions BatchingOptions = new(batchSizeLimit: 100, queueLimit: 100_000, TimeSpan.MaxValue);
    private static readonly LogFormatter Formatter = LogSettingsHelper.GetFormatter();

    private static readonly Dictionary<string, string> ExpectedAttributes = new()
    {
        // based on the defaults in the formatter
        { "host", "some_host" },
        { "ddsource", "csharp" },
        { "service", "MyTestService" },
        { "dd_env", "integration_tests" },
        { "dd_version", "1.0.0" },
    };

    private readonly ITestOutputHelper _output;

    public DirectSubmissionLoggerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CanLogMessage()
    {
        var logger = GetLogger(out var api, out var sink);

        logger.LogWarning(123, "This is {s}", "someValue");

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        var expectedExtraValues = new Dictionary<string, string>
        {
            { "@m", "This is someValue" },
            { "s", "someValue" },
            { "@i", "123" },
            { "@l", "Warning" },
        };

        AssertLogs(api, expectedLogs: 1, expectedExtraValues);
    }

    [Fact]
    public async Task CanLogMessageWithException()
    {
        var logger = GetLogger(out var api, out var sink);
        try
        {
            throw new InvalidOperationException("Throwing test exception");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "This is {s}", "someValue");
        }

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        var expectedExtraValues = new Dictionary<string, string>
        {
            { "@m", "This is someValue" },
            { "s", "someValue" },
            { "@l", "Warning" },
        };

        AssertLogs(api, expectedLogs: 1, expectedExtraValues);
    }

    [Fact]
    public async Task CanLogMessageWithOnlyException()
    {
        var logger = GetLogger(out var api, out var sink);
        try
        {
            throw new InvalidOperationException("Throwing test exception");
        }
        catch (Exception ex)
        {
            logger.Log<object>(logLevel: LogLevel.Warning, eventId: 123, state: null, exception: ex, (s, e) => e.ToString());
        }

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        var expectedExtraValues = new Dictionary<string, string>
        {
            { "@l", "Warning" },
        };

        AssertLogs(api, expectedLogs: 1, expectedExtraValues);
    }

    [Fact]
    public async Task CanLogMessageWithOnlyExceptionThatReturnsNull()
    {
        var logger = GetLogger(out var api, out var sink);
        try
        {
            throw new InvalidOperationException("Throwing test exception");
        }
        catch (Exception ex)
        {
            logger.Log<object>(logLevel: LogLevel.Warning, eventId: 123, state: null, exception: ex, (s, e) => null);
        }

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        var expectedExtraValues = new Dictionary<string, string>
        {
            { "@l", "Warning" },
        };

        AssertLogs(api, expectedLogs: 1, expectedExtraValues);
    }

    [Fact]
    public async Task CanLogMultipleMessages()
    {
        var logger = GetLogger(out var api, out var sink);

        logger.LogWarning(123, "This is {s}", "someValue");
        logger.LogWarning(123, "This is {s}", "someValue");
        logger.LogWarning(123, "This is {s}", "someValue");

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        var expectedExtraValues = new Dictionary<string, string>
        {
            { "@m", "This is someValue" },
            { "s", "someValue" },
            { "@i", "123" },
            { "@l", "Warning" },
        };

        AssertLogs(api, expectedLogs: 3, expectedExtraValues);
    }

    [Fact]
    public async Task LogsValueWhenMessageIsNull()
    {
        var logger = GetLogger(out var api, out var sink);

        logger.LogWarning(123, null);

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        var expectedExtraValues = new Dictionary<string, string>
        {
            { "@m", "[null]" },
            { "@i", "123" },
            { "@l", "Warning" },
        };

        AssertLogs(api, expectedLogs: 1, expectedExtraValues);
    }

    [Fact]
    public async Task LogsValueWhenFormatterReturnsNull()
    {
        var logger = GetLogger(out var api, out var sink);

        logger.Log<object>(logLevel: LogLevel.Warning, eventId: 123, state: null, exception: null, (s, e) => null);

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        var expectedExtraValues = new Dictionary<string, string>
        {
            { "@m", "[null]" },
            { "@i", "123" },
            { "@l", "Warning" },
        };

        AssertLogs(api, expectedLogs: 1, expectedExtraValues);
    }

    [Fact]
    public async Task LogsValueWhenFormatterReturnsNullAndHaveException()
    {
        var logger = GetLogger(out var api, out var sink);

        var ex = new InvalidOperationException("Something went wrong!");
        logger.Log<object>(logLevel: LogLevel.Warning, eventId: 123, state: null, exception: ex, (s, e) => null);

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        var expectedExtraValues = new Dictionary<string, string>
        {
            { "@m", "Something went wrong!" },
            { "@i", "123" },
            { "@l", "Warning" },
        };

        AssertLogs(api, expectedLogs: 1, expectedExtraValues);
    }

    [Fact]
    public async Task LogsValueWhenNotEnabledDoesNotSubmitLogs()
    {
        var logger = GetLogger(out var api, out var sink);

        logger.LogTrace(123, "Some message");

        logger.IsEnabled(LogLevel.Trace).Should().BeFalse();

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        api.Logs.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatesValidJsonWhenMessageIsNull()
    {
        var logger = GetLogger(out var api, out var sink);

        logger.LogWarning(123, "This is {s}", "someValue");
        logger.LogWarning(123, null);
        logger.LogWarning("This is {s}", "someValue");

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        AssertLogs(api, expectedLogs: 3);
    }

    [Fact]
    public async Task CreatesValidJsonWhenFormatterReturnsNull()
    {
        var logger = GetLogger(out var api, out var sink);

        logger.LogWarning(123, "This is {s}", "someValue");
        logger.Log<object>(logLevel: LogLevel.Warning, eventId: 123, state: null, exception: null, (s, e) => null);
        logger.LogWarning("This is {s}", "someValue");

        // dispose the sink to flush the logs
        await sink.DisposeAsync();

        AssertLogs(api, expectedLogs: 3);
    }

    private static Microsoft.Extensions.Logging.ILogger GetLogger(out DatadogSinkTests.TestLogsApi api, out DatadogSink sink)
    {
        api = new DatadogSinkTests.TestLogsApi();
        sink = new DatadogSink(api, Formatter, BatchingOptions);

        var rawLogger = new DirectSubmissionLogger(
            name: nameof(DirectSubmissionLoggerTests),
            scopeProvider: null,
            sink: sink,
            logFormatter: Formatter,
            minimumLogLevel: DirectSubmissionLogLevel.Debug);

        var logger = (Microsoft.Extensions.Logging.ILogger)rawLogger.DuckImplement(typeof(Microsoft.Extensions.Logging.ILogger));
        return logger;
    }

    private void AssertLogs(
        DatadogSinkTests.TestLogsApi api,
        int expectedLogs,
        Dictionary<string, string> expectedValues = null)
    {
        var sentMessage = api.Logs.Should().ContainSingle().Subject;
        var serializedLogs = sentMessage.Logs;
        sentMessage.NumberOfLogs.Should().Be(expectedLogs);
        serializedLogs.Count.Should().BeGreaterThan(0);

        // create a copy
        var expected = ExpectedAttributes.ToDictionary(x => x.Key, x => x.Value);
        if (expectedValues is not null)
        {
            foreach (var kvp in expectedValues)
            {
                expected[kvp.Key] = kvp.Value;
            }
        }

        AssertSystemTextJson(_output, serializedLogs, expectedLogs, expected);
        AssertNewtonsoft(_output, serializedLogs, expectedLogs, expected);

        static void AssertNewtonsoft(ITestOutputHelper output, ArraySegment<byte> logs, int expectedCount, Dictionary<string, string> expected)
        {
            // should be able to deserialize it
            using var ms = new MemoryStream(logs.Array!, logs.Offset, logs.Count);
            using var sr = new StreamReader(ms);
            using var reader = new JsonTextReader(sr);

            var array = JArray.Load(reader);

            output.WriteLine(array?.ToString());

            array.Count.Should().Be(expectedCount);

            foreach (var element in array)
            {
                var log = element.Should().BeOfType<JObject>().Subject;
                foreach (var kvp in expected)
                {
                    var value = log[kvp.Key]?.ToString();
                    value.Should().Be(kvp.Value, $"because '{kvp.Key}' should have correct value");
                }
            }
        }

        static void AssertSystemTextJson(ITestOutputHelper output, ArraySegment<byte> logs, int expectedCount, Dictionary<string, string> expected)
        {
#if NETCOREAPP3_1_OR_GREATER
            // should be able to deserialize it using
            // System.Text.Json (which is less forgiving)
            // Enable the strictest settings
            var settings = new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false, };
            var jsonDocument = JsonDocument.Parse(logs, settings);

            var root = jsonDocument.RootElement;
            output.WriteLine(root.ToString());

            root.ValueKind.Should().Be(JsonValueKind.Array);
            var elements = root.EnumerateArray().ToList();
            elements.Count.Should().Be(expectedCount);

            foreach (var log in elements)
            {
                log.ValueKind.Should().Be(JsonValueKind.Object);
                foreach (var kvp in expected)
                {
                    log.TryGetProperty(kvp.Key, out var prop).Should().BeTrue($"because '{kvp.Key}' should be present");
                    prop.ToString().Should().Be(kvp.Value, $"because '{kvp.Key}' should have correct value");
                }
            }
#endif
        }
    }
}
#endif
