using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// Duct type for HttpResponse
    /// </summary>
    public interface IHttpResponse
    {
        /// <summary>
        /// Gets or sets the status code
        /// </summary>
        int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets content type
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// writes aync
        /// </summary>
        /// <param name="text">some text</param>
        /// <returns>a continuation</returns>
        Task WriteAsync(string text);
    }
}
