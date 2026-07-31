using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Samples;

namespace Samples.FeatureFlags;
class Program
{

    private async static Task Main(string[] args)
    {
        // When run with the "enrich" argument, the sample wraps flag evaluation in an APM root span
        // (plus a child span across an await) so the FFE span-enrichment integration test can assert
        // the ffe_* tags land on the root span and child spans across async continuations propagate
        // to that same root. Without the argument the sample behaves exactly as before.
        var enrichMode = args.Contains("enrich");

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

        if (ev is { Error: "PROVIDER_NOT_READY" })
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

        if (enrichMode)
        {
            await RunEnriched();
        }
        else
        {
            Evaluator.Evaluate("exposure-flag");
            Evaluator.Evaluate("simple-string");
            Evaluator.Evaluate("rule-based-flag");
            Evaluator.Evaluate("numeric-rule-flag");
            Evaluator.Evaluate("time-based-flag");
        }

        Console.WriteLine("Extra checks...");
        Evaluator.ExtraChecks();
        Console.WriteLine("Exit. OK");
    }

    private static async Task RunEnriched()
    {
        // Root APM span: every flag evaluated while this scope is active (including inside the child
        // span and across the await below) must aggregate its ffe_* metadata onto THIS root span.
        using (SampleHelpers.CreateScope("ffe.root"))
        {
            // Evaluated directly in the root span.
            Evaluator.Evaluate("exposure-flag");
            Evaluator.Evaluate("simple-string");

            // Child span on the same trace: the enrichment must still target the LOCAL ROOT, not the
            // child. The child span itself must NOT receive the ffe_* tags.
            using (SampleHelpers.CreateScope("ffe.child"))
            {
                Evaluator.Evaluate("rule-based-flag");

                // Cross an async continuation to exercise AsyncLocal root-span resolution: the eval
                // after the await must still resolve back to "ffe.root".
                await Task.Yield();
                Evaluator.Evaluate("numeric-rule-flag");
            }

            Evaluator.Evaluate("time-based-flag");
        }

        // Flush so the mock agent receives the finished spans before the sample exits.
        await SampleHelpers.ForceTracerFlushAsync();
    }
}
