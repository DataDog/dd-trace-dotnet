// <copyright file="GooglePubSubConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Google.CloudPubSub
{
    internal static class GooglePubSubConstants
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.GoogleCloudPubSub);

        internal const string AssemblyName = "Google.Cloud.PubSub.V1";
        internal const string PublisherClientImplTypeName = "Google.Cloud.PubSub.V1.PublisherClientImpl";
        internal const string SubscriberClientImplTypeName = "Google.Cloud.PubSub.V1.SubscriberClientImpl";
        internal const string SimpleSubscriptionHandlerTypeName = "Google.Cloud.PubSub.V1.SimpleSubscriptionHandler";
        internal const string PubsubMessageTypeName = "Google.Cloud.PubSub.V1.PubsubMessage";
        internal const string SubscriptionHandlerTypeName = "Google.Cloud.PubSub.V1.SubscriptionHandler";
        internal const string TaskStringTypeName = "System.Threading.Tasks.Task`1[System.String]";
        internal const string TaskReplyTypeName = "System.Threading.Tasks.Task`1[Google.Cloud.PubSub.V1.SubscriberClient+Reply]";
    }
}
