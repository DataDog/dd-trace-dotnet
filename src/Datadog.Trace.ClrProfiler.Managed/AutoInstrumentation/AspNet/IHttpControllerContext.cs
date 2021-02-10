#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// HttpControllerContext interface for ducktyping
    /// </summary>
    public interface IHttpControllerContext
    {
    }
}
#endif
