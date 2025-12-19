using System;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.FeatureFlags;

namespace Samples.FeatureFlags;
class Evaluator
{
    public static void Init()
    {
        Console.WriteLine("FeatureFlags SDK Sample");
    }

    public static (string? Value, string? Error)? Evaluate(string key)
    {
        var context = new EvaluationContext(key);
        var evaluation = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(key, Datadog.Trace.FeatureFlags.ValueType.STRING, "Not found", context);

        if (evaluation is null)
        {
            Console.WriteLine($"Eval ({key}) : <NULL> (FeatureFlagsSdk is disabled)");
            return null;
        }
        
        if (evaluation.Error is not null)
        {
            Console.WriteLine($"Eval ({key}) : <ERROR: {evaluation?.Error}>");
        }
        else
        {
            Console.WriteLine($"Eval ({key}) : <OK: {evaluation.Value ?? "<NULL>"}>");
        }

        return (evaluation.Value as string, evaluation.Error);
    }
}
