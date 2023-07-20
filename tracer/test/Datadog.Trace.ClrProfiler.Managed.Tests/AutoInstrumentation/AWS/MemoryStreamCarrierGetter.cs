// <copyright file="MemoryStreamCarrierGetter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Amazon.SimpleNotificationService.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNSTests;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Propagators;
using Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNSTests
{
    public struct MemoryStreamCarrierGetter : ICarrierGetter<PublishRequestProxy>
    {
        private const string DatadogKey = "_datadog";

        public IEnumerable<string> Get(PublishRequestProxy carrier, string key)
        {
            object attributeValue;

            if (carrier.MessageAttributes.TryGetValue(DatadogKey, out attributeValue))
            {
                var messageAttributeValue = attributeValue as MessageAttributeValue;

                if (messageAttributeValue != null && messageAttributeValue.BinaryValue is MemoryStream memoryStream)
                {
                    memoryStream.Position = 0;
                    using var reader = new StreamReader(memoryStream, Encoding.UTF8, true, 1024, true);
                    var jsonString = reader.ReadToEnd();
                    var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

                    if (data.TryGetValue(key, out var value))
                    {
                        yield return value;
                    }
                }
                else if (messageAttributeValue != null && messageAttributeValue.StringValue != null)
                {
                    if (DatadogKey == key)
                    {
                        yield return messageAttributeValue.StringValue;
                    }
                }
            }
        }
    }
}
