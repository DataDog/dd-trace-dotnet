using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    /// <summary>
    /// message queue proxy
    /// </summary>
    public interface IMessageQueue
    {
        /// <summary>
        ///     gets a value indicating whether a cache of connections will be maintained
        ///     by the application.
        /// Returns:
        ///   true to create and use a connection cache; otherwise, false.
        /// </summary>
        bool EnableConnectionCache { get; }

        /// <summary>
        ///     Gets a value indicating whether gets a value that indicates whether this System.Messaging.MessageQueue
        ///     has exclusive access to receive messages from the Message Queuing queue.
        /// </summary>
        bool DenySharedReceive { get; }

        /// <summary>
        ///     Gets the unique Message Queuing identifier of the queue.
        /// Returns:
        ///     A System.Messaging.MessageQueue.Id that represents the message identifier generated
        ///     by the Message Queuing application.
        ///
        /// Exceptions:
        ///   T:System.Messaging.MessageQueueException:
        ///     An error occurred when accessing a Message Queuing method.
        /// </summary>
        Guid Id { get; }

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
        ///     Gets  the maximum size of the queue.
        ///
        /// Returns:
        ///     The maximum size, in kilobytes, of the queue. The Message Queuing default specifies
        ///     that no limit exists.
        ///
        /// Exceptions:
        ///   T:System.ArgumentException:
        ///     The maximum queue size contains a negative value.
        ///
        ///   T:System.Messaging.MessageQueueException:
        ///     An error occurred when accessing a Message Queuing method.
        /// </summary>
        long MaximumQueueSize { get; }

        /// <summary>
        ///   Gets the multicast address associated with the queue.Introduced in MSMQ 3.0.
        /// Returns:
        ///     A System.String that contains a valid multicast address (in the form shown below)
        ///     or null, which indicates that the queue is not associated with a multicast address.
        ///     &lt;address&gt;:&lt;port&gt;
        ///
        /// Exceptions:
        ///   T:System.PlatformNotSupportedException:
        ///     MSMQ 3.0 is not installed.
        /// </summary>
        string MulticastAddress { get; }

        /// <summary>
        ///     Gets  the queue's path.
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
        string Path { get; }

        /// <summary>
        ///     Gets the friendly name that identifies the queue.
        ///
        /// Returns:
        ///     The name that identifies the queue referenced by this System.Messaging.MessageQueue.
        ///     The value cannot be null.
        ///
        /// Exceptions:
        ///   T:System.ArgumentException:
        ///     The queue name is null.
        ///   </summary>
        string QueueName { get; }

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
        ///     Gets the queue description.
        ///
        /// Returns:
        ///     The label for the message queue. The default is an empty string ("").
        ///
        /// Exceptions:
        ///   T:System.ArgumentException:
        ///     The label was set to an invalid value.
        ///
        ///   T:System.Messaging.MessageQueueException:
        ///     An error occurred when accessing a Message Queuing method.
        /// </summary>
        string Label { get; }
    }
}
