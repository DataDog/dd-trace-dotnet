// <copyright file="AzureStorageQueuesCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.StorageQueues;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.StorageQueues;

public class AzureStorageQueuesCommonTests
{
    [Fact]
    public async Task InjectPathwayContextIntoMessageText_PreservesNumericTokensVerbatim()
    {
        var dataStreamsManager = GetDataStreamsManager();
        try
        {
            var context = new PathwayContext(new PathwayHash(123), 1234, 5678);
            const string message = "{\"amount\":0.1234567890123456789,\"big\":123456789012345678901234567890,\"tiny\":1e-29,\"text\":\"hello\"}";

            var injected = AzureStorageQueuesCommon.InjectPathwayContextIntoMessageText(dataStreamsManager, message, context);

            injected.Should().NotBeNull();
            injected.Should().NotBe(message);
            injected.Should().Contain("\"amount\":0.1234567890123456789");
            injected.Should().Contain("\"big\":123456789012345678901234567890");
            injected.Should().Contain("\"tiny\":1e-29");
            injected.Should().Contain("\"text\":\"hello\"");
            AzureStorageQueuesCommon.ExtractPathwayContextFromMessageText(dataStreamsManager, injected).Should().NotBeNull();
        }
        finally
        {
            await dataStreamsManager.DisposeAsync();
        }
    }

    [Fact]
    public async Task InjectPathwayContextIntoMessageText_ReplacesExistingDatadogEntry()
    {
        var dataStreamsManager = GetDataStreamsManager();
        try
        {
            var originalContext = new PathwayContext(new PathwayHash(123), 1234, 5678);
            var forwardedContext = new PathwayContext(new PathwayHash(456), 1234, 5678);
            const string message = "{\"key\":\"value\"}";

            var forwarded = AzureStorageQueuesCommon.InjectPathwayContextIntoMessageText(
                dataStreamsManager,
                AzureStorageQueuesCommon.InjectPathwayContextIntoMessageText(dataStreamsManager, message, originalContext),
                forwardedContext);

            forwarded.Should().NotBeNull();
            forwarded.Should().Contain("\"key\":\"value\"");
            CountOccurrences(forwarded!, "\"_datadog\":").Should().Be(1);

            var extracted = AzureStorageQueuesCommon.ExtractPathwayContextFromMessageText(dataStreamsManager, forwarded);
            extracted.Should().NotBeNull();
            extracted!.Value.Hash.Value.Should().Be(forwardedContext.Hash.Value);
        }
        finally
        {
            await dataStreamsManager.DisposeAsync();
        }
    }

    [Fact]
    public async Task InjectPathwayContextIntoMessageText_ReplacesEscapedDatadogEntry()
    {
        var dataStreamsManager = GetDataStreamsManager();
        try
        {
            var context = new PathwayContext(new PathwayHash(123), 1234, 5678);
            const string message = @"{""\u005fdatadog"":{""old"":""1""},""key"":""value""}";

            var injected = AzureStorageQueuesCommon.InjectPathwayContextIntoMessageText(dataStreamsManager, message, context);

            injected.Should().NotBeNull();
            injected.Should().Contain("\"key\":\"value\"");
            CountOccurrences(injected!, "\"_datadog\":").Should().Be(1);
            AzureStorageQueuesCommon.ExtractPathwayContextFromMessageText(dataStreamsManager, injected).Should().NotBeNull();
        }
        finally
        {
            await dataStreamsManager.DisposeAsync();
        }
    }

    [Fact]
    public async Task InjectPathwayContextIntoMessageText_NonJsonMessage_ReturnsOriginalText()
    {
        var dataStreamsManager = GetDataStreamsManager();
        try
        {
            var context = new PathwayContext(new PathwayHash(123), 1234, 5678);
            const string message = "plain-text-message";

            var injected = AzureStorageQueuesCommon.InjectPathwayContextIntoMessageText(dataStreamsManager, message, context);

            injected.Should().BeSameAs(message);
        }
        finally
        {
            await dataStreamsManager.DisposeAsync();
        }
    }

    [Fact]
    public async Task EncodeMessageTextWithPathwayContext_Base64EncodesInjectedText()
    {
        var dataStreamsManager = GetDataStreamsManager();
        try
        {
            var context = new PathwayContext(new PathwayHash(123), 1234, 5678);
            const string message = "{\"key\":\"value\"}";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(message));

            var rewritten = AzureStorageQueuesCommon.EncodeMessageTextWithPathwayContext(dataStreamsManager, encoded, isBase64: true, context);

            rewritten.Should().NotBeNullOrEmpty();
            rewritten.Should().NotBe(encoded);

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(rewritten!));
            decoded.Should().Contain("\"key\":\"value\"");
            AzureStorageQueuesCommon.ExtractPathwayContextFromMessageText(dataStreamsManager, decoded).Should().NotBeNull();
        }
        finally
        {
            await dataStreamsManager.DisposeAsync();
        }
    }

    [Fact]
    public async Task EncodeMessageTextWithPathwayContext_Base64NearSizeLimit_ReturnsNoRewrite()
    {
        var dataStreamsManager = GetDataStreamsManager();
        try
        {
            var context = new PathwayContext(new PathwayHash(123), 1234, 5678);

            // The message fits the 64 KiB limit on its own, and the decoded text still fits after
            // injection, but the Base64-encoded message (what Azure enforces) crosses the limit
            // only once the context bytes are added.
            var message = $"{{\"data\":\"{new string('x', 49_100)}\"}}";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(message));
            encoded.Length.Should().BeLessOrEqualTo(AzureStorageQueuesCommon.MaxMessageSizeBytes);

            var rewritten = AzureStorageQueuesCommon.EncodeMessageTextWithPathwayContext(dataStreamsManager, encoded, isBase64: true, context);

            rewritten.Should().BeNull();
        }
        finally
        {
            await dataStreamsManager.DisposeAsync();
        }
    }

    private static DataStreamsManager GetDataStreamsManager()
    {
        var settings = TracerSettings.Create(
            new()
            {
                { ConfigurationKeys.Environment, "foo" },
                { ConfigurationKeys.ServiceName, "bar" },
                { ConfigurationKeys.DataStreamsMonitoring.Enabled, "true" },
            });

        return new DataStreamsManager(settings, Mock.Of<IDataStreamsWriter>(), Mock.Of<IDiscoveryService>());
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
