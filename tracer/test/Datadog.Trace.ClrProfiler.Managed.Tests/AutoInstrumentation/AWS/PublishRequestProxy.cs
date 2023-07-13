// <copyright file="PublishRequestProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Linq;
using Amazon.SimpleNotificationService.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNSTests
{
    public class PublishRequestProxy : IContainsMessageAttributes
    {
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

        public PublishRequest GetPublishRequest()
        {
            return _publishRequest;
        }
    }
}
