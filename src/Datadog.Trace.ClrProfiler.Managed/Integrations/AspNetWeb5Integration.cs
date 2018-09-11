using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// ApsNetWeb5Integration wraps the Web API.
    /// </summary>
    public static class AspNetWeb5Integration
    {
        /// <summary>
        /// ExecuteAsync calls the underlying ExecuteAsync and traces the request.
        /// </summary>
        /// <param name="this">The Api Controller</param>
        /// <param name="controllerContext">The controller context for the call</param>
        /// <param name="cancellationTokenSource">The cancellation token source</param>
        /// <returns>A task with the result</returns>
        public static dynamic ExecuteAsync(dynamic @this, dynamic controllerContext, dynamic cancellationTokenSource)
        {
            var task = @this.ExecuteAsync(controllerContext, ((CancellationTokenSource)cancellationTokenSource).Token);

            return task;
        }
    }
}
