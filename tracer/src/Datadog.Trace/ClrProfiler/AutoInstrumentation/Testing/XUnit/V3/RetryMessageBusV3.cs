// <copyright file="RetryMessageBusV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

internal class RetryMessageBusV3 : RetryMessageBus
{
    public RetryMessageBusV3(IMessageBus innerMessageBus, int totalExecutions, int executionNumber)
        : base(innerMessageBus, totalExecutions, executionNumber)
    {
    }

    public bool FlushMessages(string testCaseUniqueId)
    {
        Common.Log.Debug("EFD: RetryMessageBus.FlushMessages: Flushing messages");
        if (ListOfMessages is null || ListOfMessages.Length == 0)
        {
            return true;
        }

        // Let's check for a passing execution to flush that one.
        List<object>? defaultMessages = null;
        foreach (var messages in ListOfMessages)
        {
            if (messages is not null)
            {
                if (messages.Any(m => m.DuckAs<ITestCaseMessage>()?.TestCaseUniqueID == testCaseUniqueId))
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
                retValue = retValue && InnerMessageBus.QueueMessage(messageInList);
            }

            return retValue;
        }
    }
}
