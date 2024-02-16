using System;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace Samples.Wcf.Server
{
    internal class DispatchMessageInspector : IDispatchMessageInspector
    {
        private static readonly Type _tracerType = Type.GetType("Datadog.Trace.Tracer, Datadog.Trace", throwOnError: false);
        private static readonly Type _iscopeType = Type.GetType("Datadog.Trace.IScope, Datadog.Trace", throwOnError: false);
        private static readonly Type _ispanType = Type.GetType("Datadog.Trace.ISpan, Datadog.Trace", throwOnError: false);
        private static readonly PropertyInfo _instanceProperty = _tracerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly PropertyInfo _activeScopeProperty = _tracerType?.GetProperty("ActiveScope", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo _spanProperty = _iscopeType?.GetProperty("Span", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _setTagMethod = _ispanType?.GetMethod("SetTag", [typeof(string), typeof(string)]);

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            var tracer = _instanceProperty?.GetValue(null);
            var scope = tracer is null ? null : _activeScopeProperty?.GetValue(tracer);
            var span = scope is null ? null : _spanProperty?.GetValue(scope);

            if (span is not null)
            {
                _setTagMethod.Invoke(span, ["custom-tag", nameof(DispatchMessageInspector)]);
            }

            LoggingHelper.WriteLineWithDate($"[Server] AfterReceiveRequest | ActiveScope = {scope}");

            return default;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
        }
    }
}
