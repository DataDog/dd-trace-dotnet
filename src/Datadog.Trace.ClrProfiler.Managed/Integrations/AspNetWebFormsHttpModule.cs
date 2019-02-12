#if !NETSTANDARD2_0

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    ///     IHttpModule used to trace within an ASP.NET WebForms HttpApplication request, only used to proxy the proper operation-name to the base module
    /// </summary>
    public class AspNetWebFormsHttpModule : AspNetHttpModule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetWebFormsHttpModule" /> class.
        /// </summary>
        public AspNetWebFormsHttpModule()
            : base(AspNetWebFormsIntegration.OperationName)
        {
        }
    }
}

#endif
