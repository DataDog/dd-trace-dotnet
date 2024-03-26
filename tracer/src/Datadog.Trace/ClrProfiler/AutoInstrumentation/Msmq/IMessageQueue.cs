// <copyright file="IMessageQueue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    /// <summary>
    /// message queue proxy
    /// </summary>
    internal interface IMessageQueue
    {
        /// <summary>
        ///     Gets a value indicating whether the queue accepts only transactions.
        ///
        /// Returns:
        ///     true if the queue accepts only messages sent as part of a transaction; otherwise,
        ///     false.
        ///
        /// Exceptions:
        ///   T:System.Messaging.MessageQueueException:
        ///     An error occurred when accessing a Message Queuing method.
        /// </summary>
        bool Transactional { get; }

        /// <summary>
        ///     Gets the machine name that corresponds to the queue's path. Setting the System.Messaging.MessageQueue.MachineName
        ///     causes the System.Messaging.MessageQueue to point to a new queue.
        ///
        /// Returns:
        ///     The machine name  that is referenced by the System.Messaging.MessageQueue. The default
        ///     depends on which System.Messaging.MessageQueue.#ctor constructor you use; it
        ///     is either null or is specified by the constructor's path parameter.
        ///
        /// Exceptions:
        ///   T:System.ArgumentException:
        ///     The machine name is not valid, possibly because the syntax is not valid.
        /// </summary>
        string? MachineName { get; }

        /// <summary>
        ///     Gets the queue's path. Setting the System.Messaging.MessageQueue.Path
        ///     causes the System.Messaging.MessageQueue to point to a new queue.
        ///
        /// Returns:
        ///     The queue that is referenced by the System.Messaging.MessageQueue. The default
        ///     depends on which System.Messaging.MessageQueue.#ctor constructor you use; it
        ///     is either null or is specified by the constructor's path parameter.
        ///
        /// Exceptions:
        ///   T:System.ArgumentException:
        ///     The path is not valid, possibly because the syntax is not valid.
        /// </summary>
        string? Path { get; }
    }
}
