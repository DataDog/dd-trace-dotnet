using System;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Wcf.Server;

class HttpCalculator : IHttpCalculator
{
    public double ServerSyncAddJson(string n1, string n2) => GetResult(n1, n2);

    public double ServerSyncAddXml(string n1, string n2) => GetResult(n1, n2);

    public async Task<double> ServerTaskAddPost(CalculatorArguments arguments)
    {
        await Task.Yield();
        return GetResult(arguments.Arg1.ToString(), arguments.Arg2.ToString());
    }

    public double ServerSyncAddWrapped(string n1, string n2) => GetResult(n1, n2);

    private static double GetResult(string n1, string n2, [CallerMemberName] string member = null)
    {
        LoggingHelper.WriteLineWithDate($"[Server] Received {member}({n1},{n2})");
        var result = double.Parse(n1) + double.Parse(n2);

        LoggingHelper.WriteLineWithDate($"[Server] Return {member}: {result}");
        return result;
    }

    public async Task<double> ServerComplexHttpFlow(CalculatorArguments arguments)
    {
        await Task.Yield(); // Ensure async context

        LoggingHelper.WriteLineWithDate($"[Server] Starting ServerComplexHttpFlow");

        var httpClient = new HttpClient();

        // 1. Regular HTTP call during request
        try
        {
            LoggingHelper.WriteLineWithDate($"[Server] Performing inline HttpClient call");
            var response = await httpClient.GetAsync("https://httpbin.org/get");
            LoggingHelper.WriteLineWithDate($"[Server] Inline call result: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            LoggingHelper.WriteLineWithDate($"[Server] Inline call failed: {ex.Message}");
        }

        // 2. Blocking thread
        var thread1 = new Thread(() =>
        {
            try
            {
                LoggingHelper.WriteLineWithDate($"[Server] [Thread1] Blocking thread HTTP call starting...");
                var result = httpClient.GetAsync("https://httpbin.org/status/200").Result;
                LoggingHelper.WriteLineWithDate($"[Server] [Thread1] HTTP call result: {result.StatusCode}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Server] [Thread1] Exception: {ex.Message}");
            }
        });
        thread1.Start();
        thread1.Join(); // Wait to finish

        // 3. Delayed non-blocking task
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000); // ensure WCF span ends
            try
            {
                LoggingHelper.WriteLineWithDate($"[Server] [Task] Delayed background HTTP call starting...");
                var response = await httpClient.GetAsync("https://httpbin.org/status/201");
                LoggingHelper.WriteLineWithDate($"[Server] [Task] HTTP call result: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                LoggingHelper.WriteLineWithDate($"[Server] [Task] Exception: {ex.Message}");
            }
        });

        LoggingHelper.WriteLineWithDate($"[Server] Returning response to WCF caller");

        return arguments.Arg1 + arguments.Arg2;
    }

}
