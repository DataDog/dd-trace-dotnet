using System;
using System.Net.Http;
using System.Threading.Tasks;
using OpenFeature.Constant;
using OpenFeature.Model;
using Samples;

namespace Samples.FeatureFlags;

internal static class EvaluationEventsSample
{
    internal static async Task RunWithoutTracerAsync()
    {
        await global::OpenFeature.Api.Instance.SetProviderAsync(new Datadog.FeatureFlags.OpenFeature.DatadogProvider());
        var client = global::OpenFeature.Api.Instance.GetClient();
        var context = EvaluationContext.Builder().Set("targetingKey", "evp-subject-canary@example.test").Build();
        var result = await client.GetStringDetailsAsync("simple-string", "caller-default", context);
        Check(result.Value == "caller-default" && result.ErrorType == ErrorType.ProviderNotReady, "uninstrumented provider default");
        Console.WriteLine("<EVP: UNINSTRUMENTED DEFAULT OK>");
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
