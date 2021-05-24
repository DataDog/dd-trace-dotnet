using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            // Consolidate headers into one JSON object with <header_name>:<value>
            StringBuilder sb = new();
            sb.Append("{");
            SpanContextPropagator.Instance.Inject(context, sb, ((sb, key, value) => sb.Append($"\"{key}\":\"{value}\",")));
            sb.Remove(startIndex: sb.Length - 1, length: 1); // Remove trailing comma
            sb.Append("}");

            var resultString = sb.ToString();
            messageAttributes[SqsKey] = CachedMessageHeadersHelper<TMessageRequest>.CreateMessageAttributeValue(resultString);
        }

        public static void InjectHeadersIntoMessage<TMessageRequest>(IContainsMessageAttributes carrier, SpanContext spanContext)
        {
            // add distributed tracing headers to the message
            if (carrier.MessageAttributes == null)
            {
                carrier.MessageAttributes = CachedMessageHeadersHelper<TMessageRequest>.CreateMessageAttributes();
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
