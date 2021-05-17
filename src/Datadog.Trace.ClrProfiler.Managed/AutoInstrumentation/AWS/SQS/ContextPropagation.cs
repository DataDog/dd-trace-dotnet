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

        public static void Inject<TMessageRequest>(SpanContext context, IDictionary messageAttributes)
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
            messageAttributes[SqsKey] = CachedMessageHeadersHelper<TMessageRequest>.CreateMessageAttributeValue(stringRepresentation);
        }

        public static void InjectHeadersIntoMessage<TMessageRequest>(IContainsMessageAttributes carrier, SpanContext spanContext)
        {
            // add distributed tracing headers to the message
            if (carrier.MessageAttributes == null)
            {
                // TODO: Create a new dictionary
                // basicProperties.Headers = new Dictionary<string, object>();
            }

            // SQS allows a maximum of 10 message attributes: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html#sqs-message-attributes
            // Only inject if there's room
            if (carrier.MessageAttributes.Count < 10)
            {
                Inject<TMessageRequest>(spanContext, carrier.MessageAttributes);
            }
        }
    }
}
