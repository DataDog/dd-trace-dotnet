using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Samples.OpenFeature_2._9
{
    internal class DataDogProvider : OpenFeature.FeatureProvider
    {
        Metadata _metadata = new Metadata("datadog-openfeature-provider");
        public override Metadata? GetMetadata() => _metadata;

        public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, typeof(bool), defaultValue, GetContext(context));
                return GetResolutionDetails<bool>(res);
            }, 
            cancellationToken);
        }

        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, typeof(double), defaultValue, GetContext(context));
                return GetResolutionDetails<double>(res);
            },
            cancellationToken);
        }

        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, typeof(int), defaultValue, GetContext(context));
                return GetResolutionDetails<int>(res);
            },
            cancellationToken);
        }

        public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, typeof(string), defaultValue, GetContext(context));
                return GetResolutionDetails<string>(res);
            },
            cancellationToken);
        }

        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var res = Datadog.Trace.FeatureFlags.FeatureFlagsSdk.Evaluate(flagKey, typeof(Value), defaultValue, GetContext(context));
                return GetResolutionDetails<Value>(res);
            },
            cancellationToken);
        }

        private static Datadog.Trace.FeatureFlags.IEvaluationContext? GetContext(EvaluationContext? context)
        {
            if (context == null) { return new Datadog.Trace.FeatureFlags.EvaluationContext(""); }
            var values = context.AsDictionary().Select(p => new KeyValuePair<string, object?>(p.Key, ToObject(p.Value))).ToDictionary(p => p.Key, p => p.Value);
            var res = new Datadog.Trace.FeatureFlags.EvaluationContext(context.TargetingKey!, values);
            return res;
        }

        private static object? ToObject(Value value)
        {
            if (value == null) { return null; }
            else if (value.IsBoolean) { return value.AsBoolean; }
            else if (value.IsString) { return value.AsString; }
            else if (value.IsNumber) { return value.AsDouble; }
            else { return value.AsObject; }
        }

        private static ResolutionDetails<T> GetResolutionDetails<T>(Datadog.Trace.FeatureFlags.IEvaluation? evaluation)
        {
            if (evaluation == null) { return default!; }
            var res = new ResolutionDetails<T>(
                evaluation.FlagKey, 
                (T)(evaluation.Value ?? default(T)!),
                ToErrorType(evaluation.Reason), 
                evaluation.Reason.ToString(), 
                evaluation.Variant, 
                evaluation.Error, 
                ToMetadata(evaluation.FlagMetadata!)
                );
            return res;
        }

        private static ErrorType ToErrorType(Datadog.Trace.FeatureFlags.EvaluationReason reason)
        {
            return ErrorType.None; // TODO: Map error types properly
        }

        private static ImmutableMetadata ToMetadata(IDictionary<string, string> metadata)
        {
            return default!; // TODO: Map metadata properly
        }
    }
}
