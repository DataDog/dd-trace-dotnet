using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS
{
    internal static class AwsConstants
    {
        internal const string IntegrationName = nameof(IntegrationIds.AwsSdk);
        internal const string OperationName = "aws.request";
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
    }
}
