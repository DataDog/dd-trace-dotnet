// <copyright file="PublishRequestProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.IO;
using System.Linq;
using Amazon.SimpleNotificationService.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNSTests
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:A field should not follow a property", Justification = "Reviewed.")]
    public class PublishRequestProxy : IContainsMessageAttributes
    {
        public MemoryStream MemoryStream { get; set; }

        private readonly PublishRequest _publishRequest;

        public PublishRequestProxy(PublishRequest publishRequest)
        {
            _publishRequest = publishRequest;
        }

        public IDictionary MessageAttributes
        {
            get
            {
                // Convert Dictionary<string, MessageAttributeValue> to IDictionary
                return _publishRequest.MessageAttributes;
            }

            set
            {
                // Convert IDictionary back to Dictionary<string, MessageAttributeValue>
                _publishRequest.MessageAttributes = value
                                                   .Cast<DictionaryEntry>()
                                                   .ToDictionary(
                                                        entry => entry.Key.ToString(),
                                                        entry => entry.Value as MessageAttributeValue);
            }
        }

        public void CloseMemoryStream()
        {
            MemoryStream?.Close();
            MemoryStream = null;
        }
    }
}
