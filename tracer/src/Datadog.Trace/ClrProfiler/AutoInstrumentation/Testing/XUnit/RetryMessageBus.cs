// <copyright file="RetryMessageBus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

internal class RetryMessageBus : IMessageBus
{
    private readonly IMessageBus _innerMessageBus;
    private List<object>?[]? _listOfMessages;

    public RetryMessageBus(IMessageBus innerMessageBus, int totalExecutions, int executionNumber)
    {
        _innerMessageBus = innerMessageBus;
        TotalExecutions = totalExecutions;
        ExecutionNumber = executionNumber;
    }

    public int TotalExecutions { get; set; }

    public int ExecutionNumber { get; set; }

    public int ExecutionIndex => TotalExecutions - (ExecutionNumber + 1);

    public bool? TestIsNew { get; set; }

    public bool AbortByThreshold { get; set; }

    public bool FlakyRetryEnabled { get; set; }

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

        var messageType = message.GetType();
        var totalExecutions = TotalExecutions;

        // Let's store all messages for all executions of the given test, when the test case is finished,
        // we will try to find a passing execution to flush, if not we will flush the first one.
        var currentExecutionNumber = ExecutionNumber + 1;
        var index = totalExecutions - currentExecutionNumber;
        if (_listOfMessages is null)
        {
            Common.Log.Debug<int>("EFD: RetryMessageBus.QueueMessage: Creating list of messages for {Executions} executions.", totalExecutions);
            _listOfMessages = new List<object>[totalExecutions];
        }
        else if (_listOfMessages.Length < totalExecutions)
        {
            Common.Log.Debug<int>("EFD: RetryMessageBus.QueueMessage: Resizing array with list of messages for {Executions} executions.", totalExecutions);
            Array.Resize(ref _listOfMessages, totalExecutions);
        }

        if (_listOfMessages[index] is not { } lstRetryInstance)
        {
            lstRetryInstance = [];
            _listOfMessages[index] = lstRetryInstance;
        }

        lstRetryInstance.Add(message);

        return true;
    }

    public bool FlushMessages()
    {
        Common.Log.Debug("EFD: RetryMessageBus.FlushMessages: Flushing messages");
        if (_listOfMessages is null || _listOfMessages.Length == 0)
        {
            return true;
        }

        // Let's check for a passing execution to flush that one.
        List<object>? defaultMessages = null;
        foreach (var messages in _listOfMessages)
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

            if (_listOfMessages is not null)
            {
                Array.Clear(_listOfMessages, 0, _listOfMessages.Length);
            }

            return retValue;
        }
    }
}
