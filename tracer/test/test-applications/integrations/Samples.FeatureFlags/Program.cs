using System;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.FeatureFlags;
class Program
{

    private async static Task Main(string[] args)
    {
        int configUpdates = 0;
        Evaluator.RegisterOnNewConfigEventHandler(() => Interlocked.Increment(ref configUpdates));

        if (!Evaluator.Init())
        {
            Console.WriteLine($"<NOT INSTRUMENTED>");
            return;
        }

        Console.WriteLine($"<INSTRUMENTED>");

        var ev = Evaluator.Evaluate("nonexistent");
        if (ev == null || ev.Value.Error is "FeatureFlagsSdk is disabled")
        {
            return;
        }

        if (ev is { Error: "No config loaded" })
        {
            int attempts = 180;
            while (configUpdates == 0)
            {
                if (attempts-- == 0)
                {
                    Console.WriteLine($"No RC received");
                    return;
                }
                Console.WriteLine($"Waiting for RC...");
                await Task.Delay(1_000);
            }
        }

        Evaluator.Evaluate("exposure-flag");
        Evaluator.Evaluate("simple-string");
        Evaluator.Evaluate("rule-based-flag");
        Evaluator.Evaluate("numeric-rule-flag");
        Evaluator.Evaluate("time-based-flag");

        Console.WriteLine("Extra checks...");
        Evaluator.ExtraChecks();
        Console.WriteLine("Exit. OK");
    }
}
