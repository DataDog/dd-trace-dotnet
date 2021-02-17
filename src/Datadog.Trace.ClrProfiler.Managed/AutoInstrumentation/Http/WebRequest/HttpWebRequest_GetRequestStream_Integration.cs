using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest
{
    /// <summary>
    /// CallTarget integration for HttpWebRequest.GetRequestStream
    /// </summary>
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetFrameworkAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = ClrNames.Stream,
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major4,
        IntegrationName = WebRequestCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = WebRequestCommon.NetCoreAssembly,
        TypeName = WebRequestCommon.HttpWebRequestTypeName,
        MethodName = MethodName,
        ReturnTypeName = ClrNames.Stream,
        MinimumVersion = WebRequestCommon.Major4,
        MaximumVersion = WebRequestCommon.Major5,
        IntegrationName = WebRequestCommon.IntegrationName)]
    public class HttpWebRequest_GetRequestStream_Integration
    {
        private const string MethodName = "GetRequestStream";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            if (instance is HttpWebRequest request && WebRequestCommon.IsTracingEnabled(request))
            {
                var tracer = Tracer.Instance;

                if (tracer.Settings.IsIntegrationEnabled(WebRequestCommon.IntegrationId))
                {
                    var spanContext = ScopeFactory.CreateHttpSpanContext(tracer, WebRequestCommon.IntegrationId);

                    if (spanContext != null)
                    {
                        // Add distributed tracing headers to the HTTP request.
                        // The expected sequence of calls is GetRequestStream -> GetResponse. Headers can't be modified after calling GetRequestStream.
                        // At the same time, we don't want to set an active scope now, because it's possible that GetResponse will never be called.
                        // Instead, we generate a spancontext and inject it in the headers. GetResponse will fetch them and create an active scope with the right id.
                        SpanContextPropagator.Instance.Inject(spanContext, request.Headers.Wrap());
                    }
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
