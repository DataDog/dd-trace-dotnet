// <copyright file="IbmMqConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq
{
    internal static class IbmMqConstants
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.IbmMq);
        internal const string IbmMqAssemblyName = "amqmdnetstd";
        internal const string MqDestinationTypeName = "IBM.WMQ.MQDestination";
        internal const string MqMessageTypeName = "IBM.WMQ.MQMessage";
        internal const string MqMessagePutOptionsTypeName = "IBM.WMQ.MQPutMessageOptions";
        internal const string MqMessageGetOptionsTypeName = "IBM.WMQ.MQGetMessageOptions";
        internal const string QueueType = "ibmmq";
    }
}
