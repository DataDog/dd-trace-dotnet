using System;
using System.Threading;
using OpenFeature.Model;

namespace Samples.FeatureFlags;

class Evaluator
{
    static global::OpenFeature.FeatureClient client;
    static Action? _onNewConfig = null;

    public static bool Init()
    {
        Console.WriteLine("OpenFeature FeatureFlags SDK Sample");
        if (Datadog.FeatureFlags.OpenFeature.DatadogProvider.IsAvailable)
        {

            global::OpenFeature.Api.Instance.SetProviderAsync(new Datadog.FeatureFlags.OpenFeature.DatadogProvider()).Wait();
            client = global::OpenFeature.Api.Instance.GetClient();
            Datadog.FeatureFlags.OpenFeature.DatadogProvider.RegisterOnNewConfigEventHandler(() => _onNewConfig?.Invoke());
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
        var context = EvaluationContext.Builder().Set("targetingKey", key).Build();
        var evaluation = client.GetStringDetailsAsync(key, "Not found", context).Result;

        if (evaluation is null || string.IsNullOrEmpty(evaluation.FlagKey))
        {
            Console.WriteLine($"Eval ({key}) : <NULL> (FeatureFlagsSdk is disabled)");
            return null;
        }
        
        if (evaluation.ErrorMessage is not null)
        {
            Console.WriteLine($"Eval ({key}) : <ERROR: {evaluation?.ErrorMessage}>");
        }
        else
        {
            Console.WriteLine($"Eval ({key}) : <OK: {evaluation.Value ?? "<NULL>"}>");
        }

        return (evaluation.Value, evaluation.ErrorMessage);
    }
}
