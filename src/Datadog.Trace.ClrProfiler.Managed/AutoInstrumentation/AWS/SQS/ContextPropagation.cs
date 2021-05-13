using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    internal static class ContextPropagation
    {
        private const string SqsKey = "_datadog";

        private static readonly Func<string, object> CreateMessageAttributeWithString;

        public static readonly Func<IDictionary<string, object>, string, IEnumerable<string>> HeadersGetter = ((carrier, key) =>
         {
             if (carrier.TryGetValue(key, out object value) && value is byte[] bytes)
             {
                 return new[] { Encoding.UTF8.GetString(bytes) };
             }
             else
             {
                 return Enumerable.Empty<string>();
             }
         });

        static ContextPropagation()
        {
            var messageAttributeValueType = Type.GetType("Amazon.SQS.Model.MessageAttributeValue, AWSSDK.SQS");
            var ctor = messageAttributeValueType.GetConstructor(new Type[0]);

            DynamicMethod createHeadersMethod = new DynamicMethod(
                $"KafkaCachedMessageHeadersHelpers",
                messageAttributeValueType,
                parameterTypes: new Type[] { typeof(string) },
                typeof(DuckType).Module,
                true);

            ILGenerator il = createHeadersMethod.GetILGenerator();
            il.Emit(OpCodes.Newobj, ctor);

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldstr, "String");
            il.Emit(OpCodes.Callvirt, messageAttributeValueType.GetProperty("DataType").GetSetMethod());

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, messageAttributeValueType.GetProperty("StringValue").GetSetMethod());
            // Set property

            il.Emit(OpCodes.Ret);

            CreateMessageAttributeWithString = (Func<string, object>)createHeadersMethod.CreateDelegate(typeof(Func<string, object>));
        }

        public static void Inject(SpanContext context, IDictionary messageAttributes)
        {
            /* TODO: Either use the optimized StringBuilder or decide the optimization is not worth it
            StringBuilder sb = new();
            sb.Append("{");
            SpanContextPropagator.Instance.Inject(context, sb, ((sb, key, value) => sb.Append($"\"{key}\":\"{value}\",")));
            sb.Remove(startIndex: sb.Length - 1, length: 1); // Remove trailing comma
            sb.Append("}");
            var resultString = sb.ToString();
            */

            // Consolidate separate headers into one
            Dictionary<string, string> contextMapping = new();
            SpanContextPropagator.Instance.Inject(context, contextMapping, ((dict, key, value) => dict[key] = value));

            // Emit the value as a JSON string
            var stringWriter = new StringWriter();
            using (var writer = new JsonTextWriter(stringWriter))
            {
                writer.WriteStartObject();

                foreach (var kvp in contextMapping)
                {
                    writer.WritePropertyName(kvp.Key);
                    writer.WriteValue(kvp.Value);
                }

                writer.WriteEndObject();
            }

            var stringRepresentation = stringWriter.ToString();
            messageAttributes[SqsKey] = CreateMessageAttributeWithString(stringRepresentation);
        }

        public static void InjectHeadersIntoMessage(ISendMessageRequest request, SpanContext spanContext)
        {
            // add distributed tracing headers to the message
            if (request.MessageAttributes == null)
            {
                // TODO: Create a new dictionary
                // basicProperties.Headers = new Dictionary<string, object>();
            }

            // SQS allows a maximum of 10 message attributes: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html#sqs-message-attributes
            // Only inject if there's room
            if (request.MessageAttributes.Count < 10)
            {
                Inject(spanContext, request.MessageAttributes);
            }
        }
    }
}
