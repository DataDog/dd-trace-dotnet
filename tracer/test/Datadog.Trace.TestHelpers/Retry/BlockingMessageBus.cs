// <copyright file="BlockingMessageBus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Based on https://github.dev/JoshKeegan/xRetry

using System.Collections.Concurrent;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers.Retry
{
    /// <summary>
    /// An XUnit message bus that can block messages from being passed until we want them to be.
    /// </summary>
    public class BlockingMessageBus : IMessageBus
    {
        private readonly IMessageBus underlyingMessageBus;
        private ConcurrentQueue<IMessageSinkMessage> messageQueue = new ConcurrentQueue<IMessageSinkMessage>();

        public BlockingMessageBus(IMessageBus underlyingMessageBus)
        {
            this.underlyingMessageBus = underlyingMessageBus;
        }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            messageQueue.Enqueue(message);

            // Returns if execution should continue. Since we are intercepting the message, we
            //  have no way of checking this so always continue...
            return true;
        }

        public void Clear()
        {
            messageQueue = new ConcurrentQueue<IMessageSinkMessage>();
        }

        /// <summary>
        /// Write the cached messages to the underlying message bus
        /// </summary>
        public void Flush()
        {
            while (messageQueue.TryDequeue(out IMessageSinkMessage message))
            {
                underlyingMessageBus.QueueMessage(message);
            }
        }

        public void Dispose()
        {
            // Do not dispose of the underlying message bus - it is an externally owned resource
        }
    }
}
