using System;
using System.Collections.Generic;
using Amazon.SQS.Model;
using Newtonsoft.Json;

namespace Samples.AWS.SQS
{
    public class Common
    {
        private const string TraceId = "x-datadog-trace-id";
        private const string ParentId = "x-datadog-parent-id";

        public static void AssertDistributedTracingHeaders(List<Message> messages)
        {
            foreach (var message in messages)
            {
                Dictionary<string, string> dictSpanContext = new();
                var jsonSpanContext = message.MessageAttributes["_datadog"]?.StringValue;
                if (jsonSpanContext is not null)
                {
                    dictSpanContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonSpanContext);
                }

                var activeTraceId = SampleHelpers.GetCorrelationIdentifierTraceId();
                if (dictSpanContext[ParentId] is null ||
                    !ulong.TryParse(dictSpanContext[TraceId], out ulong result) ||
                    result != activeTraceId)
                {
                    throw new Exception($"The span context was not injected into the message properly. parent-id: {dictSpanContext[ParentId]}, trace-id: {dictSpanContext[TraceId]}, active trace-id: {activeTraceId}");
                }
            }
        }

        public static void AssertNoDistributedTracingHeaders(List<Message> messages)
        {
            foreach (var message in messages)
            {
                if (message.MessageAttributes.ContainsKey("_datadog"))
                {
                    throw new Exception($"The \"_datadog\" header was found in the message, with value: {message.MessageAttributes["_datadog"].StringValue}");
                }
            }
        }
    }
}
