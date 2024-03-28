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
    private readonly int _totalExecutions;
    private readonly StrongBox<int> _executionNumber;
    private List<object>?[]? _listOfMessages;

    public RetryMessageBus(IMessageBus innerMessageBus, int totalExecutions, StrongBox<int> executionNumber)
    {
        _innerMessageBus = innerMessageBus;
        _totalExecutions = totalExecutions;
        _executionNumber = executionNumber;
    }

    [DuckReverseMethod]
    public void Dispose()
    {
    }

    [DuckReverseMethod]
    public bool QueueMessage(object? message)
    {
        if (message is null)
        {
            return false;
        }

        var messageType = message.GetType();

        // Let's store all messages for all executions of the given test, when the test case is finished,
        // we will try to find a passing execution to flush, is not we will flush the first one.
        Common.Log.Debug<string, int>("QueueMessage: Found ITestCaseMessage: {Type} [{TotalExecutions}]", messageType.Name, _totalExecutions);
        var currentExecutionNumber = _executionNumber.Value + 1;
        var index = _totalExecutions - currentExecutionNumber;

        Common.Log.Debug<int, int>("QueueMessage: Current execution number is {CurrentExecutionNumber}, index is {Index}.", currentExecutionNumber, index);
        _listOfMessages ??= new List<object>[_totalExecutions];
        if (_listOfMessages[index] is not { } lstRetryInstance)
        {
            lstRetryInstance = [];
            _listOfMessages[index] = lstRetryInstance;
        }

        lstRetryInstance.Add(message);

        if (messageType.Name == "TestFinished" && currentExecutionNumber == 1)
        {
            return FlushMessages();
        }

        return true;
    }

    private bool FlushMessages()
    {
        Common.Log.Debug("Flushing messages");
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
