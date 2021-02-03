using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// BasicProperties interface for ducktyping
    /// </summary>
    public interface IBasicProperties
    {
        /// <summary>
        /// Gets or sets the headers of the message
        /// </summary>
        /// <returns>Message headers</returns>
        IDictionary<string, object> Headers { get; set; }

        /// <summary>
        /// Gets the delivery mode of the message
        /// </summary>
        byte DeliveryMode { get; }

        /// <summary>
        /// Returns true if the DeliveryMode property is present
        /// </summary>
        /// <returns>true if the DeliveryMode property is present</returns>
        bool IsDeliveryModePresent();
    }
}
