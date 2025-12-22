using System;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.FeatureFlags;

namespace Samples.FeatureFlags;
class Program
{

    private static void Main(string[] args)
    {
        Evaluator.Init();

        if (!Datadog.Trace.FeatureFlags.FeatureFlagsSdk.IsAvailable())
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
        Datadog.Trace.FeatureFlags.FeatureFlagsSdk.RegisterOnNewConfigEventHandler(() => Interlocked.Increment(ref configUpdates));

        int attempts = 5;
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
    }


}
