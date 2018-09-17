using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Wraps a ServiceStack.Redis client and traces all requests.
    /// </summary>
    public static class ServiceStackRedis
    {
        private static ConcurrentDictionary<Type, Func<object, object, object, object, bool, object>> _sendReceiveMethods =
            new ConcurrentDictionary<Type, Func<object, object, object, object, bool, object>>();

        /// <summary>
        /// Calls the underlying RedisNativeClient SendReceive and traces the request.
        /// </summary>
        /// <param name="redisNativeClient">The native client to wrap.</param>
        /// <param name="cmdWithBinaryArgs">The data to send.</param>
        /// <param name="fn">A function which returns the result.</param>
        /// <param name="completePipelineFn">A function which completes a pipeline.</param>
        /// <param name="sendWithoutRead">Will be true if no response will be read.</param>
        /// <param name="methodHandle">the original method handle</param>
        /// <returns>A generic result based on fn.</returns>
        public static object SendReceive(object redisNativeClient, object cmdWithBinaryArgs, object fn, object completePipelineFn, bool sendWithoutRead, IntPtr methodHandle)
        {
            var constructor = typeof(RuntimeMethodHandle).
                    Assembly.
                    GetType("System.RuntimeMethodInfoStub").
                    GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, Type.DefaultBinder, new Type[] { typeof(IntPtr), typeof(object) }, null);
            var mhi = constructor.Invoke(new object[] { methodHandle, redisNativeClient });
            var handle = (RuntimeMethodHandle)typeof(RuntimeMethodHandle).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0].Invoke(new object[] { mhi });

            var methodBase = MethodBase.GetMethodFromHandle(handle);

            var resultType = GetResultTypeFromFn(fn);
            if (!_sendReceiveMethods.TryGetValue(resultType, out var originalMethod))
            {
                var asm = redisNativeClient.GetType().Assembly;

                var redisNativeClientType = asm.GetType("ServiceStack.Redis.RedisNativeClient");
                var cmdWithBinaryArgsType = typeof(byte[][]);
                var fnType = typeof(Func<>).MakeGenericType(resultType);
                var completePipelineFnType = typeof(Action<>).MakeGenericType(fnType);
                var sendWithoutReadType = typeof(bool);

                originalMethod = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, object, object, object, bool, object>>(
                    redisNativeClientType,
                    "SendReceive",
                    new Type[] { cmdWithBinaryArgsType, fnType, completePipelineFnType, sendWithoutReadType },
                    new Type[] { resultType });
                _sendReceiveMethods[resultType] = originalMethod;
            }

            using (var scope = CreateScope())
            {
                return originalMethod(redisNativeClient, cmdWithBinaryArgs, fn, completePipelineFn, sendWithoutRead);
            }
        }

        private static Scope CreateScope()
        {
            return Redis.CreateScope("HOST", "PORT", "COMMAND");
        }

        private static Type GetResultTypeFromFn(object fn)
        {
            var type = fn.GetType();
            while (type != null)
            {
                if (type.GenericTypeArguments.Length > 0)
                {
                    return type.GenericTypeArguments[0];
                }

                type = type.BaseType;
            }

            return typeof(object);
        }
    }
}
