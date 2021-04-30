using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// TypedDeliveryHandlerShim_Action for duck-typing
    /// </summary>
    public interface ITypedDeliveryHandlerShimAction
    {
        /// <summary>
        /// Sets the delivery report handler
        /// </summary>
        [Duck(Kind = DuckKind.Field)]
        public object Handler { set; }
    }
}
