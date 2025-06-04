// <copyright file="RetryMessageBus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

internal class RetryMessageBus : IMessageBus
{
    private readonly Dictionary<string, RetryTestCaseMetadata> _testMethodMetadata = new();
    private readonly IMessageBus _innerMessageBus;
    private readonly int _totalExecutions;
    private readonly int _executionNumber;

    public RetryMessageBus(IMessageBus innerMessageBus, int totalExecutions, int executionNumber)
    {
        _innerMessageBus = innerMessageBus;
        _totalExecutions = totalExecutions;
        _executionNumber = executionNumber;
    }

    public TestCaseMetadata GetMetadata(string uniqueID)
    {
        Common.Log.Debug("RetryMessageBus.GetMetadata: Looking for: {Id}", uniqueID);
#if NET6_0_OR_GREATER
        ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(_testMethodMetadata, uniqueID, out _);
        if (value is null)
        {
            Common.Log.Debug("RetryMessageBus.GetMetadata: Not found, creating new one for value {Id}", uniqueID);
            value = new RetryTestCaseMetadata(uniqueID, _totalExecutions, _executionNumber);
        }
#else
        if (!_testMethodMetadata.TryGetValue(uniqueID, out var value))
        {
            Common.Log.Debug("RetryMessageBus.GetMetadata: Not found, creating new one for value {Id}", uniqueID);
            value = new RetryTestCaseMetadata(uniqueID, _totalExecutions, _executionNumber);
            _testMethodMetadata[uniqueID] = value;
        }
#endif

        return value;
    }

    [DuckReverseMethod]
    public void Dispose()
    {
        _innerMessageBus.Dispose();
    }

    [DuckReverseMethod]
    public bool QueueMessage(object? message)
    {
        if (message is null)
        {
            return false;
        }

        string? uniqueID;
        if (message.TryDuckCast<ITestCaseMessage>(out var testCaseMessage))
        {
            uniqueID = testCaseMessage.TestCase.UniqueID;
        }
        else if (message.TryDuckCast<ITestCaseMessageV3>(out var testCaseMessageV3))
        {
            uniqueID = testCaseMessageV3.TestMethodUniqueID;
        }
        else if (message.TryDuckCast<ITestMethodMetadataV3>(out var testMethodMetadataV3))
        {
            uniqueID = testMethodMetadataV3.TestMethodUniqueID;
        }
        else
        {
            Common.Log.Debug("RetryMessageBus.QueueMessage: Message is not a supported message. Flushing: {Message}", message);
            return _innerMessageBus.QueueMessage(message);
        }

        Common.Log.Debug("RetryMessageBus.QueueMessage: Message: {Message} | UniqueID: {UniqueID}", message, uniqueID);

        if (uniqueID is not null)
        {
            var metadata = (RetryTestCaseMetadata)GetMetadata(uniqueID);
            if (metadata.Disposed)
            {
                Common.Log.Debug("RetryMessageBus.QueueMessage: Metadata is disposed for: {UniqueID} direct flush of the message.", uniqueID);
                return _innerMessageBus.QueueMessage(message);
            }

            var totalExecutions = metadata.TotalExecutions;

            // Let's store all messages for all executions of the given test, when the test case is finished,
            // we will try to find a passing execution to flush, if not we will flush the first one.
            var currentExecutionNumber = metadata.CountDownExecutionNumber + 1;
            var index = totalExecutions - currentExecutionNumber;
            if (metadata.ListOfMessages is null)
            {
                Common.Log.Debug<int>("RetryMessageBus.QueueMessage: Creating list of messages for {Executions} executions.", totalExecutions);
                metadata.ListOfMessages = new List<object>[totalExecutions];
            }
            else if (metadata.ListOfMessages.Length < totalExecutions)
            {
                Common.Log.Debug<int>("RetryMessageBus.QueueMessage: Resizing array with list of messages for {Executions} executions.", totalExecutions);
                metadata.ResizeListOfMessages(totalExecutions);
            }

            if (index < 0)
            {
                Common.Log.Error<int>("RetryMessageBus.QueueMessage: Execution index {Index} is less than 0.", index);
                FlushMessages(uniqueID);
                throw new Exception($"Execution index {index} is less than 0.");
            }

            if (metadata.ListOfMessages[index] is not { } lstRetryInstance)
            {
                lstRetryInstance = [];
                metadata.ListOfMessages[index] = lstRetryInstance;
            }

            lstRetryInstance.Add(message);
            return true;
        }

        Common.Log.Error("RetryMessageBus.QueueMessage: Message doesn't have an UniqueID. Flushing: {Message}", message);
        return _innerMessageBus.QueueMessage(message);
    }

    public bool FlushMessages(string uniqueID)
    {
        Common.Log.Debug("RetryMessageBus.FlushMessages: Flushing messages for: {UniqueID}", uniqueID);

        var metadata = (RetryTestCaseMetadata)GetMetadata(uniqueID);
        var listOfMessages = metadata.ListOfMessages;
        if (listOfMessages is null || listOfMessages.Length == 0 || metadata.Disposed)
        {
            Common.Log.Debug("RetryMessageBus.FlushMessages: Nothing to flush for: {UniqueID}", uniqueID);
            return true;
        }

        // Let's check for a passing execution to flush that one.
        List<object>? defaultMessages = null;
        foreach (var messages in listOfMessages)
        {
            if (messages is not null)
            {
                defaultMessages ??= messages;
                foreach (var sinkMessage in messages)
                {
                    if (sinkMessage.GetType().Name == "TestPassed")
                    {
                        return InternalFlushMessages(messages);
                    }
                }
            }
        }

        // If we don't detect any passing execution, we just flush the first not null one.
        if (defaultMessages is null)
        {
            return false;
        }

        return InternalFlushMessages(defaultMessages);

        bool InternalFlushMessages(List<object> messages)
        {
            var retValue = true;
            foreach (var messageInList in messages)
            {
                retValue = retValue && _innerMessageBus.QueueMessage(messageInList);
            }

            Common.Log.Debug<int, string>("RetryMessageBus.InternalFlushMessages: {Count} messages flushed for: {UniqueID}", messages.Count, uniqueID);

            Array.Clear(listOfMessages, 0, listOfMessages.Length);
            return retValue;
        }
    }

#pragma warning disable SA1201 // ElementsMustAppearInTheCorrectOrder
    internal interface ITestCaseMessage
    {
        ITestCase TestCase { get; }
    }

    internal interface ITestMethodMetadataV3
    {
        string TestMethodUniqueID { get; }
    }

    internal interface ITestCaseMessageV3 : ITestMethodMessageV3
    {
        string? TestCaseUniqueID { get; set; }
    }

    internal interface ITestMethodMessageV3
    {
        string? TestMethodUniqueID { get; set; }
    }

    private class RetryTestCaseMetadata(string uniqueID, int totalExecution, int executionNumber) : TestCaseMetadata(uniqueID, totalExecution, executionNumber)
    {
        private List<object>?[]? _listOfMessages;

        public List<object>?[]? ListOfMessages
        {
            get => _listOfMessages;
            set => _listOfMessages = value;
        }

        public bool Disposed { get; set; }

        public void ResizeListOfMessages(int totalExecutions)
        {
            Array.Resize(ref _listOfMessages, totalExecutions);
        }
    }
}
