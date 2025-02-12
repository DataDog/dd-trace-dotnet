using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;

namespace BootlecapLeakRepro;

internal static class Program
{
    public static async Task Main()
    {
        int i = 0;

        while (true)
        {
            try
            {
                Console.WriteLine($"Iteration {i++}");

                var requestBuilder = new LambdaRequestBuilder();
                var scope = LambdaCommon.SendStartInvocation(requestBuilder, data: string.Empty, context: null);
                await LambdaCommon.EndInvocationAsync(string.Empty, exception: null, scope, requestBuilder);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
