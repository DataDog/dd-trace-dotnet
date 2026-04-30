// <copyright file="MassTransitConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    internal static class MassTransitConstants
    {
        // IntegrationName must match the enum member name for source generators
        internal const string IntegrationName = nameof(IntegrationId.MassTransit);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.MassTransit;

        // ComponentTagName is the lowercase value for the component tag
        internal const string ComponentTagName = "masstransit";

        // Operation types for resource naming and tags
        internal const string OperationSend = "send";
        internal const string OperationReceive = "receive";
        internal const string OperationProcess = "process";

        // Span operation names — full "masstransit.{operation}" form. Defined as constants so
        // construction sites and string-equality comparison sites (e.g. the parent-override branch
        // in MassTransitDiagnosticObserver.OnConsumeStart) share one source of truth.
        internal const string SendOperationName = "masstransit." + OperationSend;
        internal const string ReceiveOperationName = "masstransit." + OperationReceive;
        internal const string ProcessOperationName = "masstransit." + OperationProcess;

        internal static string GetOperationName(string operation) => operation switch
        {
            OperationSend => SendOperationName,
            OperationReceive => ReceiveOperationName,
            OperationProcess => ProcessOperationName,
            _ => "masstransit." + operation,
        };
    }
}
