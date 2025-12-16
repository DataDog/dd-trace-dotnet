using System;
using OpenFeature.Model;

namespace Samples.OpenFeature_2_9;

class Program
{

    private static void Main(string[] args)
    {
        // See https://aka.ms/new-console-template for more information
        Console.WriteLine("OpenFeature 2.9 FeatureFlags SDK Sample");

        OpenFeature.Api.Instance.SetProviderAsync(new Datadog.FeatureFlags.OpenFeature.DatadogProvider()).Wait();
        var client = OpenFeature.Api.Instance.GetClient();

        if (!Datadog.Trace.FeatureFlags.FeatureFlagsSdk.IsAvailable())
        {
            Console.WriteLine($"<NOT INSTRUMENTED>");
            return;
        }

        Console.WriteLine($"<INSTRUMENTED>");

        var ev = Evaluate("nonexistent");
        if (ev != null && ev.ErrorMessage is "FeatureFlagsSdk is disabled")
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
            System.Threading.Thread.Sleep(1000);
        }

        Evaluate("exposure-flag");
        Evaluate("simple-string");
        Evaluate("rule-based-flag");
        Evaluate("numeric-rule-flag");
        Evaluate("time-based-flag");

        FlagEvaluationDetails<string>? Evaluate(string key)
        {
            var context = EvaluationContext.Builder().Set("targetingKey", key).Build();
            var evaluation = client.GetStringDetailsAsync(key, "Not found", context).Result;

            if (evaluation is null)
            {
                Console.WriteLine($"Eval ({key}) : <NULL>");
            }
            else if (evaluation.ErrorMessage is not null)
            {
                Console.WriteLine($"Eval ({key}) : <ERROR: {evaluation?.ErrorMessage}>");
            }
            else
            {
                Console.WriteLine($"Eval ({key}) : <OK: {evaluation.Value ?? "<NULL>"}>");
            }

            return evaluation;
        }

    }
}
