using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Wraps a RedisNativeClient.
    /// </summary>
    public static class ServiceStackRedisIntegration
    {
        private const string Major4 = "4";
        private const string Major5 = "5";
        private const string RedisNativeClient = "ServiceStack.Redis.RedisNativeClient";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.ServiceStackRedis));
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceStackRedisIntegration));

        /// <summary>
        /// Traces SendReceive.
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="redisNativeClient">The redis native client</param>
        /// <param name="cmdWithBinaryArgs">The command with args</param>
        /// <param name="fn">The function</param>
        /// <param name="completePipelineFn">An optional function to call to complete a pipeline</param>
        /// <param name="sendWithoutRead">Whether or to send without waiting for the result</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original result</returns>
        [InterceptMethod(
            CallerAssembly = "ServiceStack.Redis",
            TargetAssembly = "ServiceStack.Redis",
            TargetType = RedisNativeClient,
            TargetSignatureTypes = new[] { "T", "System.Byte[][]", "System.Func`1<T>", "System.Action`1<System.Func`1<T>>", ClrNames.Bool },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        public static T SendReceive<T>(
            object redisNativeClient,
            byte[][] cmdWithBinaryArgs,
            object fn,
            object completePipelineFn,
            bool sendWithoutRead,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (redisNativeClient == null)
            {
                throw new ArgumentNullException(nameof(redisNativeClient));
            }

            Func<object, byte[][], object, object, bool, T> instrumentedMethod;

            try
            {
                var instrumentedType = redisNativeClient.GetInstrumentedType(RedisNativeClient);
                instrumentedMethod =
                    MethodBuilder<Func<object, byte[][], object, object, bool, T>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(SendReceive))
                       .WithConcreteType(instrumentedType)
                       .WithParameters(cmdWithBinaryArgs, fn, completePipelineFn, sendWithoutRead)
                       .WithMethodGenerics(typeof(T))
                       .WithNamespaceAndNameFilters(ClrNames.Ignore, "System.Byte[][]", "System.Func`1", "System.Action`1", ClrNames.Bool)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: RedisNativeClient,
                    methodName: nameof(SendReceive),
                    instanceType: redisNativeClient.GetType().AssemblyQualifiedName);
                throw;
            }

            RedisNativeClientData clientData = redisNativeClient.As<RedisNativeClientData>();

            using (var scope = RedisHelper.CreateScope(
                Tracer.Instance,
                IntegrationId,
                clientData.Host ?? string.Empty,
                clientData.Port.ToString(CultureInfo.InvariantCulture),
                GetRawCommand(cmdWithBinaryArgs)))
            {
                try
                {
                    return instrumentedMethod(redisNativeClient, cmdWithBinaryArgs, fn, completePipelineFn, sendWithoutRead);
                }
                catch (Exception ex)
                {
                    scope?.Span?.SetException(ex);
                    throw;
                }
            }
        }

        private static string GetRawCommand(byte[][] cmdWithBinaryArgs)
        {
            return string.Join(
                " ",
                cmdWithBinaryArgs.Select(
                    bs =>
                    {
                        try
                        {
                            return Encoding.UTF8.GetString(bs);
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    }));
        }

        /*
         * Ducktyping types
         */

        /// <summary>
        /// Redis native client struct data for duck typing
        /// </summary>
        [DuckCopy]
        public struct RedisNativeClientData
        {
            /// <summary>
            /// Client Hostname
            /// </summary>
            public string Host;

            /// <summary>
            /// Client Port
            /// </summary>
            public int Port;
        }
    }
}
