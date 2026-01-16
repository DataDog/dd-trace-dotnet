using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.FeatureFlags;

namespace Samples.FeatureFlags;
class Evaluator
{
    static Action? _onNewConfig = null;

    public static bool Init()
    {
        Console.WriteLine("FeatureFlags SDK Sample");
        if (FeatureFlagsSdk.IsAvailable())
        {
            Datadog.Trace.FeatureFlags.FeatureFlagsSdk.RegisterOnNewConfigEventHandler(() => _onNewConfig?.Invoke());
            return true;
        }

        return false;
    }

    public static void RegisterOnNewConfigEventHandler(Action onNewConfig)
    {
        _onNewConfig = onNewConfig;
    }

    public static (string? Value, string? Error)? Evaluate(string key)
    {
        var evaluation = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(key, Datadog.Trace.FeatureFlags.ValueType.String, "Not found", new EvaluationContext(key));

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
