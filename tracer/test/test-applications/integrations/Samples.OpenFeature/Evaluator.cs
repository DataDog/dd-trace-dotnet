using System;
using System.Diagnostics;
using System.Threading.Tasks;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Samples.FeatureFlags;

class Evaluator
{
    static global::OpenFeature.FeatureClient client = null!; // assigned by Init()
    static Datadog.FeatureFlags.OpenFeature.DatadogProvider provider = null!; // assigned by Init()
    static Action? _onNewConfig = null;

    public static async Task<bool> Init()
    {
        Console.WriteLine("OpenFeature FeatureFlags SDK Sample");
        if (!Datadog.FeatureFlags.OpenFeature.DatadogProvider.IsAvailable)
        {
            return false;
        }

        // SetProviderAsync awaits the provider's InitializeAsync, which starts agentless delivery
        // and waits for the first configuration.
        provider = new Datadog.FeatureFlags.OpenFeature.DatadogProvider();
        await global::OpenFeature.Api.Instance.SetProviderAsync(provider);
        client = global::OpenFeature.Api.Instance.GetClient();
        Datadog.FeatureFlags.OpenFeature.DatadogProvider.RegisterOnNewConfigEventHandler(() => _onNewConfig?.Invoke());
        return true;
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
            Console.WriteLine($"Eval ({key}) : <ERROR: {evaluation.ErrorMessage}>");
        }
        else
        {
            Console.WriteLine($"Eval ({key}) : <OK: {evaluation.Value ?? "<NULL>"}>");
        }

        return (evaluation.Value, evaluation.ErrorMessage);
    }

    public static void ExtraChecks()
    {
        var key = "simple-json";
        var context = EvaluationContext.Builder().Set("targetingKey", key).Build();

        var defaultValue = new Value("Not found");
        var evaluation = client.GetObjectDetailsAsync(key, defaultValue, context).Result;

        Assert(evaluation is not null, "Null eval");
        Assert(evaluation!.ErrorMessage is null, $"Non Null error ({evaluation.ErrorMessage})");
        Assert(evaluation.Value != defaultValue, "Default value");
        Assert(evaluation.Value.IsStructure, "No structure value");
        Assert(evaluation.Value.AsStructure!.ContainsKey("integer"), "Integer value not found");
        Assert(evaluation.Value.AsStructure!.GetValue("integer").AsInteger == 1, "Wrong Integer value");

        // Exercise synchronous resolution through native instrumentation and remote configuration.
        var stringContext = EvaluationContext.Builder().SetTargetingKey("simple-string").Build();
        var asyncString = client.GetStringDetailsAsync("simple-string", "Not found", stringContext).Result;
#pragma warning disable DDFF001 // Exercises the experimental synchronous provider API.
        var syncString = provider.ResolveStringValue("simple-string", "Not found", stringContext);
        var syncJson = provider.ResolveStructureValue(key, defaultValue, context);
#pragma warning restore DDFF001
        Assert(syncString.ErrorType == ErrorType.None, $"Sync string error ({syncString.ErrorType})");
        Assert(syncString.Value == asyncString.Value, $"Sync string value ({syncString.Value} != {asyncString.Value})");
        Assert(syncString.Reason == asyncString.Reason, "Wrong sync string reason");
        Assert(syncString.Variant == asyncString.Variant, "Wrong sync string variant");
        Assert(syncJson.ErrorType == ErrorType.None, $"Sync json error ({syncJson.ErrorType})");
        Assert(syncJson.Value.AsStructure?.GetValue("integer").AsInteger == 1, "Wrong sync Integer value");

        static void Assert(bool condition, string message = "")
        {
            if (!condition)
            {
                var error = $"ERROR: Assertion failed. {message}";
                Console.WriteLine(error);
                throw new Exception(error);
            }
        }
    }
}
