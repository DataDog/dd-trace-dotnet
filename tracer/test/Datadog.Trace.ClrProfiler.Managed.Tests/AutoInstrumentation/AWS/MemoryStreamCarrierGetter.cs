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
        private const string CacheKey = "TestCacheKey";  // constant key
        private static readonly Dictionary<string, Dictionary<string, string>> Cache = new();

        public IEnumerable<string> Get(PublishRequestProxy carrier, string key)
        {
            object attributeValue;

            if (!Cache.TryGetValue(CacheKey, out var cachedData) && carrier.MessageAttributes.TryGetValue(DatadogKey, out attributeValue))
            {
                var messageAttributeValue = attributeValue as MessageAttributeValue;

                if (messageAttributeValue != null && messageAttributeValue.BinaryValue is MemoryStream memoryStream)
                {
                    memoryStream.Position = 0;
                    using var reader = new StreamReader(memoryStream, Encoding.UTF8);
                    var jsonString = reader.ReadToEnd();
                    cachedData = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
                    Cache[CacheKey] = cachedData;
                }
                else if (messageAttributeValue != null && messageAttributeValue.StringValue != null)
                {
                    cachedData = new Dictionary<string, string>() { { DatadogKey, messageAttributeValue.StringValue } };
                    Cache[CacheKey] = cachedData;
                }
            }

            if (cachedData != null && cachedData.TryGetValue(key, out var value))
            {
                yield return value;
            }
        }
    }
}
