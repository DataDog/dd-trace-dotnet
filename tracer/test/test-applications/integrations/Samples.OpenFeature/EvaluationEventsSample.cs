using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using OpenFeature.Constant;
using OpenFeature.Model;
using Samples;

namespace Samples.FeatureFlags;

internal static class EvaluationEventsSample
{
    internal static async Task RunStartupGateAsync()
    {
        using var original = new Datadog.FeatureFlags.OpenFeature.DatadogProvider();
        Check(!HasEventHook(original), "disabled provider has no EVP hook");
        await global::OpenFeature.Api.Instance.SetProviderAsync(original);
        var client = global::OpenFeature.Api.Instance.GetClient();
        var context = EvaluationContext.Builder().Set("targetingKey", "evp-startup-subject").Build();
        var before = await client.GetStringDetailsAsync("simple-string", "caller-default", context);
        Check(before.Value == "test-value", "disabled provider still evaluates");
        Check(before.FlagMetadata?.GetDouble("__dd_eval_timestamp_ms") is null, "disabled provider omits EVP timestamp");

        Environment.SetEnvironmentVariable("DD_FLAGGING_EVALUATION_COUNTS_ENABLED", "true");
        // Reconfigure the automatic tracer explicitly: the shared helper targets the older public API.
        var settingsType = Type.GetType("Datadog.Trace.Configuration.TracerSettings, Datadog.Trace", throwOnError: true)!;
        var settings = settingsType.GetMethod("FromDefaultSourcesInternal", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);
        var tracerType = Type.GetType("Datadog.Trace.Tracer, Datadog.Trace", throwOnError: true)!;
        tracerType.GetMethod("Configure", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new[] { settings });
        await original.InitializeAsync(EvaluationContext.Builder().Build());
        Check(!HasEventHook(original), "existing disabled provider keeps its startup decision");
        var unchanged = await client.GetStringDetailsAsync("simple-string", "caller-default", context);
        Check(unchanged.Value == "test-value", "existing provider still evaluates after tracer reconfiguration");
        await SampleHelpers.ForceTracerFlushAsync();

        using var replacement = new Datadog.FeatureFlags.OpenFeature.DatadogProvider();
        Check(HasEventHook(replacement), "new provider sees enabled setting before initialization");
        await global::OpenFeature.Api.Instance.SetProviderAsync(replacement);
        var after = await client.GetStringDetailsAsync("simple-string", "caller-default", context);
        Check(after.Value == "test-value", "replacement provider still evaluates");
        Check(after.FlagMetadata?.GetDouble("__dd_eval_timestamp_ms") is not null, "enabled provider captures EVP timestamp");
        await SampleHelpers.ForceTracerFlushAsync();
        Console.WriteLine("<EVP: PROVIDER RECREATION OK>");

        static bool HasEventHook(Datadog.FeatureFlags.OpenFeature.DatadogProvider provider)
            => provider.GetProviderHooks().Any(hook => hook.GetType().Name == "FlagEvalEVPHook");
    }

    internal static async Task RunWithoutTracerAsync()
    {
        await global::OpenFeature.Api.Instance.SetProviderAsync(new Datadog.FeatureFlags.OpenFeature.DatadogProvider());
        var client = global::OpenFeature.Api.Instance.GetClient();
        var context = EvaluationContext.Builder().Set("targetingKey", "evp-subject-canary@example.test").Build();
        var result = await client.GetStringDetailsAsync("simple-string", "caller-default", context);
        Check(result.Value == "caller-default" && result.ErrorType == ErrorType.ProviderNotReady, "uninstrumented provider default");
        Console.WriteLine("<EVP: UNINSTRUMENTED DEFAULT OK>");
    }

    internal static async Task RunSynchronousAsync()
    {
        using var provider = new Datadog.FeatureFlags.OpenFeature.DatadogProvider();
        var context = EvaluationContext.Builder()
                                       .Set("targetingKey", "evp-subject-canary@example.test")
                                       .Set("privateAttribute", "evp-private-attribute-canary")
                                       .Build();
        await provider.InitializeAsync(context);
#pragma warning disable DDFF001 // Exercises EVP through the experimental synchronous provider API.
        for (var i = 0; i < 3; i++)
        {
            var result = provider.ResolveStringValue("simple-string", "caller-default", context);
            Check(result.Value == "test-value" && result.ErrorType == ErrorType.None, "sync simple-string result");
        }

        var exposure = provider.ResolveStringValue("exposure-flag", "caller-default", context);
        Check(exposure.Value == "tracked-value" && exposure.ErrorType == ErrorType.None, "sync exposure-flag result");
        var missing = provider.ResolveStringValue("missing-flag", "caller-default", context);
        Check(missing.Value == "caller-default" && missing.ErrorType == ErrorType.FlagNotFound, "sync missing flag default");
        var wrongType = provider.ResolveBooleanValue("simple-string", false, context);
        Check(!wrongType.Value && wrongType.ErrorType == ErrorType.TypeMismatch, "sync wrong type default");
#pragma warning restore DDFF001

        Console.WriteLine("<EVP: VALUES AND DEFAULTS OK>");
        await SampleHelpers.ForceTracerFlushAsync();
        Console.WriteLine("<EVP: FLUSHED>");
    }

    internal static async Task RunAsync()
    {
        var client = global::OpenFeature.Api.Instance.GetClient();
        var context = EvaluationContext.Builder()
                                       .Set("targetingKey", "evp-subject-canary@example.test")
                                       .Set("privateAttribute", "evp-private-attribute-canary")
                                       .Build();
        for (var i = 0; i < 3; i++)
        {
            var result = await client.GetStringDetailsAsync("simple-string", "caller-default", context);
            Check(result.Value == "test-value" && result.ErrorType == ErrorType.None, "simple-string result");
        }

        var exposure = await client.GetStringDetailsAsync("exposure-flag", "caller-default", context);
        Check(exposure.Value == "tracked-value" && exposure.ErrorType == ErrorType.None, "exposure-flag result");
        var missing = await client.GetStringDetailsAsync("missing-flag", "caller-default", context);
        Check(missing.Value == "caller-default" && missing.ErrorType == ErrorType.FlagNotFound, "missing flag default");
        var wrongType = await client.GetBooleanDetailsAsync("simple-string", false, context);
        Check(!wrongType.Value && wrongType.ErrorType == ErrorType.TypeMismatch, "wrong type default");

        Console.WriteLine("<EVP: VALUES AND DEFAULTS OK>");
        var advanceUrl = Environment.GetEnvironmentVariable("FFE_TEST_ADVANCE_URL");
        if (!string.IsNullOrEmpty(advanceUrl))
        {
            var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Datadog.FeatureFlags.OpenFeature.DatadogProvider.RegisterOnNewConfigEventHandler(() => changed.TrySetResult(true));
            using var http = new HttpClient();
            using var response = await http.GetAsync(advanceUrl);
            response.EnsureSuccessStatusCode();
            Check(await Task.WhenAny(changed.Task, Task.Delay(5_000)) == changed.Task, "configuration update notification");
            var afterChange = await client.GetStringDetailsAsync("simple-string", "caller-default", context);
            Check(afterChange.Value == "test-value" && afterChange.ErrorType == ErrorType.None, "result after configuration update");
            Console.WriteLine("<EVP: CONFIGURATION CHANGED>");
        }

        await SampleHelpers.ForceTracerFlushAsync();
        Console.WriteLine("<EVP: FLUSHED>");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Evaluation assertion failed: " + description);
        }
    }
}
