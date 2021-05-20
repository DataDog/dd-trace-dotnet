using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// Duct type for HttpContext
    /// </summary>
    public interface IHttpContext
    {
        /// <summary>
        /// Gets the response
        /// </summary>
        IHttpResponse Response { get; }

        /// <summary>
        /// Gets or sets the items dictionary
        /// </summary>
        IDictionary<object, object> Items { get; set; }
    }
}
