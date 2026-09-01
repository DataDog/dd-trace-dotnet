// <copyright file="RetryMessageBus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

internal sealed class RetryMessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<string, RetryTestCaseMetadata> _testCaseMetadata = new(StringComparer.Ordinal);
    private readonly IMessageBus _innerMessageBus;
    private readonly int _totalExecutions;
    private readonly int _executionNumber;
    private int _disposed;

    public RetryMessageBus(IMessageBus innerMessageBus, int totalExecutions, int executionNumber)
    {
        _innerMessageBus = innerMessageBus;
        _totalExecutions = totalExecutions;
        _executionNumber = executionNumber;
    }

    public TestCaseMetadata GetMetadata(string uniqueID)
    {
        if (_testCaseMetadata.TryGetValue(uniqueID, out var metadata))
        {
            return metadata;
        }

        var newMetadata = new RetryTestCaseMetadata(uniqueID, _totalExecutions, _executionNumber);
        return _testCaseMetadata.GetOrAdd(uniqueID, newMetadata);
    }

    public bool TryGetMetadata(string uniqueID, out TestCaseMetadata? metadata)
    {
        if (_testCaseMetadata.TryGetValue(uniqueID, out var existingMetadata))
        {
            metadata = existingMetadata;
            return true;
        }

        metadata = null;
        return false;
    }

    [DuckReverseMethod]
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        foreach (var uniqueID in _testCaseMetadata.Keys)
        {
            FlushMessages(uniqueID);
        }

        _innerMessageBus.Dispose();
    }

    [DuckReverseMethod]
    public bool QueueMessage(object? message)
    {
        if (message is null || Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        var uniqueID = GetTestCaseUniqueID(message);
        if (uniqueID is null)
        {
            Common.Log.Debug("RetryMessageBus.QueueMessage: Message has no test case identity. Forwarding: {Message}", message);
            return InternalQueueMessage(message);
        }

        var metadata = (RetryTestCaseMetadata)GetMetadata(uniqueID);
        var forwardDirectly = false;
        var invalidExecutionIndex = false;

        lock (metadata.SyncRoot)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

            if (metadata.Disposed)
            {
                forwardDirectly = true;
            }
            else
            {
                var totalExecutions = metadata.TotalExecutions;
                if (metadata.ListOfMessages is null)
                {
                    metadata.ListOfMessages = new List<object>?[totalExecutions];
                }
                else if (metadata.ListOfMessages.Length < totalExecutions)
                {
                    metadata.ResizeListOfMessages(totalExecutions);
                }

                var currentExecutionNumber = metadata.CountDownExecutionNumber + 1;
                var index = totalExecutions - currentExecutionNumber;
                if (index < 0 || index >= metadata.ListOfMessages.Length)
                {
                    invalidExecutionIndex = true;
                    forwardDirectly = true;
                }
                else
                {
                    var executionMessages = metadata.ListOfMessages[index] ??= [];
                    executionMessages.Add(message);

                    var messageTypeName = message.GetType().Name;
                    if (messageTypeName is "TestStarting" or "TestClassConstructionStarting" or "TestClassConstructionFinished")
                    {
                        forwardDirectly = (!metadata.Skipped && metadata.BypassedMessageTypes.Add(messageTypeName)) ||
                                          metadata.IsEarlyFlakeDetection;
                    }
                }
            }
        }

        if (invalidExecutionIndex)
        {
            Common.Log.Error("RetryMessageBus.QueueMessage: Invalid execution index for test case {UniqueID}. Forwarding the message.", uniqueID);
        }

        return forwardDirectly ? InternalQueueMessage(message) : true;
    }

    public bool FlushMessages(string uniqueID, XUnitFrameworkResult frameworkResult = XUnitFrameworkResult.Unknown)
    {
        if (!_testCaseMetadata.TryGetValue(uniqueID, out var metadata))
        {
            return true;
        }

        List<object>? messagesToFlush = null;
        lock (metadata.SyncRoot)
        {
            if (metadata.Disposed)
            {
                return true;
            }

            metadata.Disposed = true;
            var messagesByExecution = metadata.ListOfMessages;
            metadata.ListOfMessages = null;

            if (metadata.Skipped || messagesByExecution is null || messagesByExecution.Length == 0)
            {
                return true;
            }

            List<object>? firstCompletedExecution = null;
            foreach (var executionMessages in messagesByExecution)
            {
                if (executionMessages is null)
                {
                    continue;
                }

                firstCompletedExecution ??= executionMessages;
                foreach (var sinkMessage in executionMessages)
                {
                    var messageResult = GetFrameworkResult(sinkMessage);
                    var isSelectedResult = frameworkResult == XUnitFrameworkResult.Unknown
                                               ? messageResult == XUnitFrameworkResult.Passed
                                               : messageResult == frameworkResult;
                    if (isSelectedResult)
                    {
                        messagesToFlush = [.. executionMessages];
                        break;
                    }
                }

                if (messagesToFlush is not null)
                {
                    break;
                }
            }

            messagesToFlush ??= firstCompletedExecution is null ? null : [.. firstCompletedExecution];
        }

        if (messagesToFlush is null)
        {
            return false;
        }

        var result = true;
        foreach (var message in messagesToFlush)
        {
            var messageTypeName = message.GetType().Name;
            if (messageTypeName is "TestStarting" or "TestClassConstructionStarting" or "TestClassConstructionFinished")
            {
                continue;
            }

            result = InternalQueueMessage(message) && result;
        }

        return result;
    }

    private static string? GetTestCaseUniqueID(object message)
    {
        if (message.TryDuckCast<ITestCaseMessage>(out var testCaseMessage))
        {
            return testCaseMessage.TestCase.UniqueID;
        }

        if (message.TryDuckCast<ITestCaseMessageV3>(out var testCaseMessageV3) &&
            testCaseMessageV3.TestCaseUniqueID is { Length: > 0 } testCaseUniqueID)
        {
            return testCaseUniqueID;
        }

        return null;
    }

    private static XUnitFrameworkResult GetFrameworkResult(object message)
        => message.GetType().Name switch
        {
            "TestPassed" => XUnitFrameworkResult.Passed,
            "TestFailed" => XUnitFrameworkResult.Failed,
            "TestSkipped" => XUnitFrameworkResult.Skipped,
            "TestNotRun" => XUnitFrameworkResult.NotRun,
            _ => XUnitFrameworkResult.Unknown,
        };

    private bool InternalQueueMessage(object message)
    {
        try
        {
            return _innerMessageBus.QueueMessage(message);
        }
        catch (Exception ex)
        {
            Common.Log.Error(ex, "RetryMessageBus.InternalQueueMessage: Error while queueing message: {Message}", message);
            return false;
        }
    }

#pragma warning disable SA1201 // ElementsMustAppearInTheCorrectOrder
    internal interface ITestCaseMessage
    {
        ITestCase TestCase { get; }
    }

    internal interface ITestCaseMessageV3
    {
        string? TestCaseUniqueID { get; }
    }

    private sealed class RetryTestCaseMetadata(string uniqueID, int totalExecution, int executionNumber) : TestCaseMetadata(uniqueID, totalExecution, executionNumber)
    {
        private List<object>?[]? _listOfMessages;

        public object SyncRoot { get; } = new();

        public List<object>?[]? ListOfMessages
        {
            get => _listOfMessages;
            set => _listOfMessages = value;
        }

        public bool Disposed { get; set; }

        public HashSet<string> BypassedMessageTypes { get; } = new();

        public void ResizeListOfMessages(int totalExecutions)
        {
            Array.Resize(ref _listOfMessages, totalExecutions);
        }
    }
}
