using System;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    internal class CustomNLogLogProvider : NLogLogProvider
    {
        public void RegisterLayoutRenderers()
        {
            var logEventInfoType = FindType("NLog.LogEventInfo", "NLog");
            var wrapFunc = (Func<Func<object>, object>)typeof(CustomNLogLogProvider).GetMethod(nameof(WrapFunc), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(logEventInfoType)
                .CreateDelegate(typeof(Func<Func<object>, object>));

            var layoutRendererType = FindType("NLog.LayoutRenderers.LayoutRenderer", "NLog");
            var register = GetRegisterLayoutRendererMethod(layoutRendererType, logEventInfoType);

            register(CorrelationIdentifier.TraceIdKey, wrapFunc(() => Tracer.Instance?.ActiveScope?.Span.TraceId.ToString()));
            register(CorrelationIdentifier.SpanIdKey, wrapFunc(() => Tracer.Instance?.ActiveScope?.Span.SpanId.ToString()));
            register(CorrelationIdentifier.VersionKey, wrapFunc(() => Tracer.Instance?.Settings.ServiceVersion));
            register(CorrelationIdentifier.EnvKey, wrapFunc(() => Tracer.Instance?.Settings.Environment));
            register(CorrelationIdentifier.ServiceKey, wrapFunc(() => Tracer.Instance?.DefaultServiceName));
        }

        private static object WrapFunc<T>(Func<object> action)
        {
            return new Func<T, object>(_ => action());
        }

        private Action<string, object> GetRegisterLayoutRendererMethod(Type layoutRendererType, Type logEventInfoType)
        {
            var funcType = typeof(Func<,>).MakeGenericType(logEventInfoType, typeof(object));
            var registerMethod = layoutRendererType.GetMethod("Register", typeof(string), funcType);

            var nameParam = Expression.Parameter(typeof(string), "name");
            var funcParam = Expression.Parameter(typeof(object), "func");

            var castFuncParam = Expression.Convert(funcParam, funcType);

            var call = Expression.Call(registerMethod, nameParam, castFuncParam);

            return Expression.Lambda<Action<string, object>>(call, nameParam, funcParam).Compile();
        }
    }
}
