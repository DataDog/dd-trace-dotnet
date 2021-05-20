using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// A delegate representing a request
    /// </summary>
    /// <param name="context">http context</param>
    /// <returns>a continuation</returns>
    public delegate Task RequestDelegate(IHttpContext context);
}
