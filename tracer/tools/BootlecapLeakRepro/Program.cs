using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;

namespace BootlecapLeakRepro;

internal static class Program
{
    public static async Task Main()
    {
        while (true)
        {
            var requestBuilder = new LambdaRequestBuilder();
            var scope = LambdaCommon.SendStartInvocation(requestBuilder, data: string.Empty, context: null);
            await LambdaCommon.EndInvocationAsync(string.Empty, exception: null, scope, requestBuilder);
        }
    }
}
