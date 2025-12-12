using System;
using System.Diagnostics;
using Datadog.Trace.FeatureFlags;

namespace Samples.OpenFeature_2_9;

class Program
{

    private static void Main(string[] args)
    {
        // See https://aka.ms/new-console-template for more information
        Console.WriteLine("OpenFeature 2.9 FeatureFlags SDK Sample");

        if (!Datadog.Trace.FeatureFlags.FeatureFlagsSdk.IsAvailable())
        {
            Console.WriteLine($"<NOT INSTRUMENTED>");
            return;
        }

        Console.WriteLine($"<INSTRUMENTED>");

        var ev = Evaluate("nonexistent");
        if (ev != null && ev.Error is "FeatureFlagsSdk is disabled")
        {
            return;
        }


        int configUpdates = 0;
        Datadog.Trace.FeatureFlags.FeatureFlagsSdk.RegisterOnNewConfigEventHandler(() => configUpdates++);

        int attempts = 5;
        while (configUpdates == 0)
        {
            if (attempts-- == 0)
            {
                Console.WriteLine($"No RC received");
                return;
            }
            Console.WriteLine($"Waiting for RC...");
            Thread.Sleep(1000);
        }

        Evaluate("exposure-flag");
        Evaluate("simple-string");
        Evaluate("rule-based-flag");
        Evaluate("numeric-rule-flag");
        Evaluate("time-based-flag");

        IEvaluation? Evaluate(string key)
        {
            var context = new EvaluationContext(key);
            var evaluation = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(key, typeof(string), "Not found", context);

            if (evaluation is null)
            {
                Console.WriteLine($"Eval ({key}) : <NULL>");
            }
            else if (evaluation.Error is not null)
            {
                Console.WriteLine($"Eval ({key}) : <ERROR: {evaluation?.Error}>");
            }
            else
            {
                Console.WriteLine($"Eval ({key}) : <OK: {evaluation.Value ?? "<NULL>"}>");
            }

            return evaluation;
        }

    }
}
