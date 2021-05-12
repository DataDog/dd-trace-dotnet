using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    /// <summary>
    /// message queue proxy
    /// </summary>
    public interface IMessageQueue
    {
        /// <summary>
        ///     Gets the unique queue name that Message Queuing generated at the time of the
        ///     queue's creation.
        ///
        /// Returns:
        ///     The name for the queue, which is unique on the network.
        ///
        /// Exceptions:
        ///   T:System.Messaging.MessageQueueException:
        ///     The System.Messaging.MessageQueue.Path is not set. -or- An error occurred when
        ///     accessing a Message Queuing method.
        /// </summary>
        string FormatName { get; }

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
    }
}
