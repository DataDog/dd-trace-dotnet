using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracing integration for RabbitMQ.Client
    /// </summary>
    public static class RabbitMQIntegration
    {
        internal const string IntegrationName = nameof(IntegrationIds.RabbitMQ);

        private const string OperationName = "amqp.command";
        private const string ServiceName = "rabbitmq";

        private const string Major3Minor6Patch9 = "3.6.9";
        private const string Major6 = "6";
        private const string RabbitMQAssembly = "RabbitMQ.Client";
        private const string RabbitMQImplModelBase = "RabbitMQ.Client.Impl.ModelBase";
        private const string IBasicPropertiesTypeName = "RabbitMQ.Client.IBasicProperties";
        private const string IDictionaryArgumentsTypeName = "System.Collections.Generic.IDictionary`2[System.String,System.Object]";

        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.RabbitMQ));

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(MongoDbIntegration));

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it
        /// </summary>
        /// <param name="model">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="queue">Name of the queue.</param>
        /// <param name="passive">The original passive setting</param>
        /// <param name="durable">The original duable setting</param>
        /// <param name="exclusive">The original exclusive settings</param>
        /// <param name="autoDelete">The original autoDelete setting</param>
        /// <param name="nowait">The original nowait setting</param>
        /// <param name="arguments">The original arguments setting</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            TargetAssembly = RabbitMQAssembly,
            TargetType = RabbitMQImplModelBase,
            TargetMethod = "_Private_QueueDeclare",
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.String, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Ignore },
            TargetMinimumVersion = Major3Minor6Patch9,
            TargetMaximumVersion = Major6)]
        public static void QueueDeclare(
            object model,
            string queue,
            bool passive,
            bool durable,
            bool exclusive,
            bool autoDelete,
            bool nowait,
            object arguments,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (model == null) { throw new ArgumentNullException(nameof(model)); }

            const string methodName = "_Private_QueueDeclare";
            const string command = "queue.declare";
            Action<object, string, bool, bool, bool, bool, bool, object> instrumentedMethod;
            var modelType = model.GetType();

            try
            {
                instrumentedMethod =
                    MethodBuilder<Action<object, string, bool, bool, bool, bool, bool, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(modelType)
                       .WithParameters(queue, passive, durable, exclusive, autoDelete, nowait, arguments)
                       .WithNamespaceAndNameFilters(ClrNames.Void, ClrNames.String, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, ClrNames.Ignore)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: RabbitMQImplModelBase,
                    methodName: methodName,
                    instanceType: modelType.AssemblyQualifiedName);
                throw;
            }

            RabbitMQTags tags = null;
            using (var scope = CreateScope(Tracer.Instance, out tags, command, queue: queue))
            {
                try
                {
                    instrumentedMethod(model, queue, passive, durable, exclusive, autoDelete, nowait, arguments);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        internal static Scope CreateScope(Tracer tracer, out RabbitMQTags tags, string command, ISpanContext parentContext = null, string queue = null, string exchange = null, string routingKey = null)
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                Span parent = tracer.ActiveScope?.Span;

                tags = new RabbitMQTags();
                scope = tracer.StartActiveWithTags(OperationName, tags: tags, serviceName: $"{tracer.DefaultServiceName}-{IntegrationName}");
                var span = scope.Span;

                span.Type = SpanTypes.MessageClient;
                span.ResourceName = command;
                tags.Command = command;

                tags.Queue = queue;
                tags.Exchange = exchange;
                tags.RoutingKey = routingKey;

                tags.InstrumentationName = IntegrationName;
                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }
    }
}
