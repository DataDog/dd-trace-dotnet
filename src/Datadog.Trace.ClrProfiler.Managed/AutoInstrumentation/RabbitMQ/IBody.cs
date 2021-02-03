using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// Body interface for ducktyping
    /// </summary>
    public interface IBody
    {
        /// <summary>
        /// Gets the length of the message body
        /// </summary>
        int Length { get; }
    }
}
