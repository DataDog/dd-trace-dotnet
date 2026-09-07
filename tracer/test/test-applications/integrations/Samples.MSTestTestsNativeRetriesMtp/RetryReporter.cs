// <copyright file="RetryReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Samples.MSTestTestsNativeRetriesMtp;

public sealed class RetryReporter : IDataConsumer
{
    private readonly object _writeLock = new();

    public string Uid => "dd-mstest-retry-reporter";

    public string Version => "1.0.0";

    public string DisplayName => "Retry history recorder";

    public string Description => "Records the retry history delivered to MTP consumers.";

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
        => builder.TestHost.AddDataConsumer(_ => new RetryReporter());

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task ConsumeAsync(IDataProducer producer, IData data, CancellationToken cancellationToken)
    {
        if (data is TestNodeUpdateMessage message &&
            message.TestNode.Properties.SingleOrDefault<RetryAttemptProperty>() is { } retry &&
            Environment.GetEnvironmentVariable("MSTEST_RETRY_HISTORY_FILE") is { Length: > 0 } output)
        {
            lock (_writeLock)
            {
                File.AppendAllText(output, $"{message.TestNode.DisplayName}|{retry.AttemptNumber}|{retry.IsSuperseded}{Environment.NewLine}");
            }
        }

        return Task.CompletedTask;
    }
}
