using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;

namespace Samples.FeatureFlags;
class Evaluator
{
    static Action? _onNewConfig = null;

    public static async Task<bool> Init()
    {
        Console.WriteLine("FeatureFlags SDK Sample");
        if (!FeatureFlagsSdk.IsAvailable())
        {
            return false;
        }

        Datadog.Trace.FeatureFlags.FeatureFlagsSdk.RegisterOnNewConfigEventHandler(() => _onNewConfig?.Invoke());

        // Starts agentless delivery; with the remote_config source it waits for the first update.
        await Datadog.Trace.FeatureFlags.FeatureFlagsSdk.InitializeAsync(CancellationToken.None);
        return true;
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
            Console.WriteLine($"Eval ({key}) : <ERROR: {evaluation.Error}>");
        }
        else
        {
            Console.WriteLine($"Eval ({key}) : <OK: {evaluation.Value ?? "<NULL>"}>");
        }

        return (evaluation.Value as string, evaluation.Error);
    }

    public static void ExtraChecks()
    {
    }
}
