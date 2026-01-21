using System;
using System.Threading;

namespace Samples.FeatureFlags;
class Program
{

    private static void Main(string[] args)
    {
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


        int configUpdates = 0;
        Evaluator.RegisterOnNewConfigEventHandler(() => Interlocked.Increment(ref configUpdates));

        int attempts = 50;
        while (configUpdates == 0)
        {
            if (attempts-- == 0)
            {
                Console.WriteLine($"No RC received");
                return;
            }
            Console.WriteLine($"Waiting for RC...");
            System.Threading.Thread.Sleep(1000);
        }

        Evaluator.Evaluate("exposure-flag");
        Evaluator.Evaluate("simple-string");
        Evaluator.Evaluate("rule-based-flag");
        Evaluator.Evaluate("numeric-rule-flag");
        Evaluator.Evaluate("time-based-flag");

        Console.WriteLine("Extra checks...");
        Evaluator.ExtraChecks();
    }


}
