// <copyright file="FeatureFlagsEvaluator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.FeatureFlags
{
    internal sealed class FeatureFlagsEvaluator
    {
        internal const string MetadataAllocationKey = "__dd_allocation_key";
        internal const string MetadataErrorCode = "errorCode";

        internal const string MetadataSplitSerialId = FeatureFlagMetadataKeys.SplitSerialId;
        internal const string MetadataDoLog = FeatureFlagMetadataKeys.DoLog;

        internal const string FeatureDestinationHook = "HOOK";
        internal const string FeatureDestinationExposure = "EXPOSURE";
        internal const string FeatureDestinationEvaluation = "EVALUATION";

        // A newer configuration can name destinations this SDK does not know; warn once per name,
        // not per evaluation, and stop after this many distinct names.
        private const int MaxWarnedDestinations = 64;

        private static readonly ConcurrentDictionary<string, byte> WarnedDestinations = new(StringComparer.Ordinal);

        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsEvaluator));

        private const int DefaultMaxDependencyDepth = 10;

        private static readonly string[] SupportedCapabilities = [FeatureFlagCapabilities.DependentFlagVariantKeyV1];

        private readonly ReportExposureDelegate? _onExposureEvent;
        private readonly ServerConfiguration? _config;
        private readonly bool _spanEnrichmentEnabled;
        private readonly IDatadogLogger _log;
        private readonly Lazy<int>? _maxDependencyDepth;
        private readonly HashSet<string>? _dependentFlagKeys;

        public FeatureFlagsEvaluator(
            ReportExposureDelegate? onExposureEvent,
            ServerConfiguration? config,
            bool spanEnrichmentEnabled = false,
            IDatadogLogger? logger = null)
        {
            _onExposureEvent = onExposureEvent;
            _config = config;
            _spanEnrichmentEnabled = spanEnrichmentEnabled;
            _log = logger ?? Log;
            _dependentFlagKeys = GetDependentFlagKeys(config);
            _maxDependencyDepth = config is null
                                      ? null
                                      : new Lazy<int>(() => ParseMaxDependencyDepth(config, _log), LazyThreadSafetyMode.ExecutionAndPublication);
            if (_config is null)
            {
                _log.Debug("Creating Evaluator without config");
            }
            else
            {
                _log.Debug<int>("Creating Evaluator with {Flags} flags", _config.Flags?.Count ?? 0);
            }
        }

        internal delegate Exposure.ExposureRecordResult ReportExposureDelegate(in Exposure.Model.ExposureEvent ev);

        private delegate bool NumberEquality(double a, double b);

        /// <summary>
        /// Gets the configuration this evaluator resolves against, which diagnostics use to identify it.
        /// </summary>
        internal ServerConfiguration? Configuration => _config;

        private int MaxDependencyDepth => _maxDependencyDepth?.Value ?? DefaultMaxDependencyDepth;

        // Opt-in requires prerequisite evaluation, error propagation, and exposure reporting.
        internal static IReadOnlyCollection<string> GetCapabilities() => SupportedCapabilities;

        public Evaluation Evaluate(string flagKey, ValueType resultType, object? defaultValue, EvaluationContext? context)
            => Evaluate(flagKey, resultType, defaultValue, context, deferExposure: false);

        // Leaves an exposure on Evaluation.PendingExposure instead of recording and sending it, for the
        // caller to commit once the evaluation has completed.
        public Evaluation EvaluateWithDeferredExposure(string flagKey, ValueType resultType, object? defaultValue, EvaluationContext? context)
            => Evaluate(flagKey, resultType, defaultValue, context, deferExposure: true);

        internal Evaluation Evaluate(string flagKey, ValueType resultType, object? defaultValue, EvaluationContext? context, bool deferExposure)
        {
            if (TryEvaluateIndependent(flagKey, resultType, defaultValue, context, out var independent) && independent is not null)
            {
                return CompleteIndependentEvaluation(independent, deferExposure);
            }

            var trace = EvaluateWithoutDependencyTelemetry(flagKey, resultType, defaultValue, context);
            if (deferExposure)
            {
                if (trace.DependencyExposures.Count > 0)
                {
                    trace.Details.PendingExposure = trace.DependencyExposures[trace.DependencyExposures.Count - 1];
                }

                return trace.Details;
            }

            foreach (var exposure in trace.DependencyExposures)
            {
                try
                {
                    var result = _onExposureEvent?.Invoke(in exposure);
                    if (result is not null && exposure.Flag.Key == flagKey && trace.Details.FlagMetadata is { } metadata)
                    {
                        metadata[FeatureFlagMetadataKeys.ExposureCacheHit] = result == Exposure.ExposureRecordResult.Repeat ? "true" : "false";
                    }
                }
                catch
                {
                    // Exposure delivery is best effort and must not affect flag evaluation.
                }
            }

            return trace.Details;
        }

        internal bool TryEvaluateIndependent(string flagKey, ValueType resultType, object? defaultValue, EvaluationContext? context, out Evaluation? evaluation)
        {
            var consent = _config?.ObserveFullEvaluationData == true ? "true" : "false";
            if (HasDependencyCondition(flagKey))
            {
                evaluation = null;
                return false;
            }

            try
            {
                evaluation = EvaluateFlag(flagKey, resultType, defaultValue, context, session: null, DateTime.UtcNow, consent);
            }
            catch (FormatException ex)
            {
                evaluation = Error(flagKey, defaultValue, "PARSE_ERROR", ex.Message, consent);
            }
            catch (MissingTargetingKeyException)
            {
                evaluation = Error(flagKey, defaultValue, "TARGETING_KEY_MISSING", null, consent);
            }
            catch (Exception ex)
            {
                evaluation = Error(flagKey, defaultValue, "GENERAL", ex.Message, consent, ex.Message);
            }

            return true;
        }

        private Evaluation CompleteIndependentEvaluation(Evaluation evaluation, bool deferExposure)
        {
            if (evaluation.PendingExposure is not { } exposure)
            {
                return evaluation;
            }

            if (deferExposure)
            {
                return evaluation;
            }

            evaluation.PendingExposure = null;
            try
            {
                var result = _onExposureEvent?.Invoke(in exposure);
                if (result is not null && evaluation.FlagMetadata is { } metadata)
                {
                    metadata[FeatureFlagMetadataKeys.ExposureCacheHit] = result == Exposure.ExposureRecordResult.Repeat ? "true" : "false";
                }
            }
            catch
            {
                // Exposure delivery is best effort and must not affect flag evaluation.
            }

            return evaluation;
        }

        private bool HasDependencyCondition(string flagKey)
            => _dependentFlagKeys?.Contains(flagKey) == true;

        private HashSet<string>? GetDependentFlagKeys(ServerConfiguration? config)
        {
            if (config?.Flags is not { } flags)
            {
                return null;
            }

            HashSet<string>? dependentFlagKeys = null;
            foreach (var pair in flags.ValidFlags)
            {
                if (!FlagContainsDependency(pair.Value))
                {
                    continue;
                }

                dependentFlagKeys ??= new HashSet<string>();
                dependentFlagKeys.Add(pair.Key);
            }

            return dependentFlagKeys;
        }

        private bool FlagContainsDependency(Flag flag)
        {
            if (flag.Allocations is not { } allocations)
            {
                return false;
            }

            foreach (var allocation in allocations)
            {
                if (allocation.Rules is not { } rules)
                {
                    continue;
                }

                foreach (var rule in rules)
                {
                    if (rule.Conditions is not { } conditions)
                    {
                        continue;
                    }

                    foreach (var condition in conditions)
                    {
                        if (condition.FlagEvaluationField.IsPresent)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal EvaluationTrace EvaluateWithTrace(string flagKey, ValueType resultType, object? defaultValue, EvaluationContext? context)
            => EvaluateCore(flagKey, resultType, defaultValue, context, collectDependencyEvaluations: true);

        internal EvaluationTrace EvaluateWithoutDependencyTelemetry(string flagKey, ValueType resultType, object? defaultValue, EvaluationContext? context)
            => EvaluateCore(flagKey, resultType, defaultValue, context, collectDependencyEvaluations: false);

        private EvaluationTrace EvaluateCore(
            string flagKey,
            ValueType resultType,
            object? defaultValue,
            EvaluationContext? context,
            bool collectDependencyEvaluations)
        {
            // Capture before evaluating: even an exception after a configuration update keeps this consent.
            var consent = _config?.ObserveFullEvaluationData == true ? "true" : "false";
            DependencyEvaluationSession? session = null;
            try
            {
                var config = _config;
                if (config == null)
                {
                    return EvaluationTrace.Empty(new Evaluation(
                        flagKey,
                        defaultValue,
                        EvaluationReason.Error,
                        error: "PROVIDER_NOT_READY",
                        metadata: new Dictionary<string, string>
                        {
                            ["errorCode"] = "PROVIDER_NOT_READY",
                            [FeatureFlagMetadataKeys.ObserveFullEvaluationData] = consent
                        }));
                }

                var now = DateTime.UtcNow;
                session = new DependencyEvaluationSession(this, config, flagKey, context, now, consent, collectDependencyEvaluations);
                var evaluation = EvaluateFlag(flagKey, resultType, defaultValue, context, session, now, consent);
                return session.Complete(evaluation);
            }
            catch (DependencyGraphException ex)
            {
                var errorCode = ex.OriginFlagKey is not null && ex.OriginFlagKey != flagKey ? "GENERAL" : ex.ErrorCode;
                return CompleteError(Error(flagKey, defaultValue, errorCode, ex.Message, consent));
            }
            catch (FormatException ex)
            {
                return CompleteError(Error(flagKey, defaultValue, "PARSE_ERROR", ex.Message, consent));
            }
            catch (MissingTargetingKeyException)
            {
                return CompleteError(Error(flagKey, defaultValue, "TARGETING_KEY_MISSING", null, consent));
            }
            catch (Exception ex)
            {
                return CompleteError(Error(flagKey, defaultValue, "GENERAL", ex.Message, consent, ex.Message));
            }

            EvaluationTrace CompleteError(Evaluation error) => session?.Complete(error) ?? EvaluationTrace.Empty(error);
        }

#pragma warning disable SA1204 // Static helpers follow this recursive instance method for evaluator readability
        private Evaluation EvaluateFlag(
            string flagKey,
            ValueType resultType,
            object? defaultValue,
            EvaluationContext? context,
            DependencyEvaluationSession? session,
            DateTime now,
            string consent)
        {
            Flag? flag = null;
            var lookupResult = _config?.Flags?.Find(flagKey, out flag) ?? FlagLookupResult.NotFound;
            if (lookupResult == FlagLookupResult.NotFound)
            {
                return Error(flagKey, defaultValue, "FLAG_NOT_FOUND", null, consent);
            }

            if (lookupResult == FlagLookupResult.Invalid || flag is null)
            {
                return Error(flagKey, defaultValue, "PARSE_ERROR", null, consent);
            }

            if (flag.Enabled != true)
            {
                return new Evaluation(
                    flagKey,
                    defaultValue,
                    EvaluationReason.Disabled,
                    metadata: ConsentMetadata(consent));
            }

            if (flag.VariationType != resultType)
            {
                return Error(flagKey, defaultValue, "TYPE_MISMATCH", null, consent);
            }

            if (flag.Allocations is null or { Count: 0 })
            {
                return new Evaluation(
                    flagKey,
                    defaultValue,
                    EvaluationReason.Default,
                    metadata: ConsentMetadata(consent));
            }

            var targetingKey = context?.TargetingKey;

            foreach (var allocation in flag.Allocations)
            {
                if (!IsAllocationActive(allocation, now))
                {
                    continue;
                }

                var checkpoint = session?.Checkpoint() ?? default;
                var hadRules = false;
                if (allocation.Rules is { Count: > 0 } rules)
                {
                    hadRules = true;
                    if (!EvaluateRules(rules, context, session))
                    {
                        session?.Rollback(checkpoint);
                        continue;
                    }
                }

                if (allocation.Splits is { Count: > 0 })
                {
                    foreach (var split in allocation.Splits)
                    {
                        if (StringUtil.IsNullOrEmpty(split.VariationKey))
                        {
                            throw new FormatException($"Empty variation key in allocation {allocation.Key}");
                        }

                        var allShardsMatch = true;
                        var hadShards = false;
                        if (split.Shards is { Count: > 0 } shards)
                        {
                            hadShards = true;
                            foreach (var shard in shards)
                            {
                                if (!MatchesShard(shard, targetingKey))
                                {
                                    allShardsMatch = false;
                                    break;
                                }
                            }
                        }

                        if (allShardsMatch)
                        {
                            var isTemporalDefault = !hadRules &&
                                                    !hadShards &&
                                                    allocation.Splits.Count == 1 &&
                                                    (!StringUtil.IsNullOrEmpty(allocation.StartAt) ||
                                                     !StringUtil.IsNullOrEmpty(allocation.EndAt));
                            var reason = hadRules ? EvaluationReason.TargetingMatch
                                       : isTemporalDefault ? EvaluationReason.Default
                                       : hadShards ? EvaluationReason.Split
                                       : EvaluationReason.Static;

                            return ResolveVariant(flagKey, resultType, defaultValue, flag, split, allocation, reason, now, context, consent, session);
                        }
                    }
                }

                session?.Rollback(checkpoint);
            }

            return new Evaluation(flagKey, defaultValue, EvaluationReason.Default, metadata: ConsentMetadata(consent));
        }

        private static bool IsAllocationActive(Allocation allocation, DateTime now)
        {
            var startDate = ParseDate(allocation.StartAt);
            if (startDate.HasValue && now < startDate.Value)
            {
                return false;
            }

            var endDate = ParseDate(allocation.EndAt);
            if (endDate.HasValue && now >= endDate.Value)
            {
                return false;
            }

            return true;
        }

        private static bool EvaluateRules(List<Rule> rules, EvaluationContext? context, DependencyEvaluationSession? session)
        {
            foreach (var rule in rules)
            {
                if (rule.Conditions is null || rule.Conditions.Count == 0)
                {
                    continue;
                }

                var checkpoint = session?.Checkpoint() ?? default;
                var allConditionsMatch = true;
                foreach (var condition in rule.Conditions)
                {
                    if (!EvaluateCondition(condition, context, session))
                    {
                        allConditionsMatch = false;
                        break;
                    }
                }

                if (allConditionsMatch)
                {
                    return true;
                }

                session?.Rollback(checkpoint);
            }

            return false;
        }

        private static bool EvaluateCondition(ConditionConfiguration condition, EvaluationContext? context, DependencyEvaluationSession? session)
        {
            if (condition.Operator is null)
            {
                throw new FormatException("Condition operator can not be null");
            }

            if (!condition.TryValidateSource(out var isDependency, out var dependencyKey))
            {
                throw new FormatException("Condition source or dependency descriptor is invalid");
            }

            if (isDependency)
            {
                var variant = (session ?? throw new InvalidOperationException("Dependency evaluation requires a trace session"))
                             .Resolve(dependencyKey ?? throw new FormatException("Dependency key is missing"));
                return condition.Operator == ConditionOperator.ONE_OF
                           ? IsDependencyVariantOneOf(variant, condition.Value)
                           : !IsDependencyVariantOneOf(variant, condition.Value);
            }

            if (condition.Operator == ConditionOperator.IS_NULL)
            {
                var value = ResolveAttribute(condition.Attribute, context);
                var isNull = value == null;
                var expectedNull = condition.Value is bool b ? b : throw new FormatException("Bool value expected");
                return isNull == expectedNull;
            }

            var attributeValue = ResolveAttribute(condition.Attribute, context);
            if (attributeValue == null)
            {
                return false;
            }

            switch (condition.Operator)
            {
                case ConditionOperator.MATCHES:
                    return condition.MatchesRegex(attributeValue);
                case ConditionOperator.NOT_MATCHES:
                    return !condition.MatchesRegex(attributeValue);
                case ConditionOperator.ONE_OF:
                    return IsOneOf(attributeValue, condition.Value);
                case ConditionOperator.NOT_ONE_OF:
                    return !IsOneOf(attributeValue, condition.Value);
                case ConditionOperator.GTE:
                    return CompareNumber(attributeValue, condition.Value, (a, b) => a >= b);
                case ConditionOperator.GT:
                    return CompareNumber(attributeValue, condition.Value, (a, b) => a > b);
                case ConditionOperator.LTE:
                    return CompareNumber(attributeValue, condition.Value, (a, b) => a <= b);
                case ConditionOperator.LT:
                    return CompareNumber(attributeValue, condition.Value, (a, b) => a < b);
                case ConditionOperator.SEMVER_EQ:
                    return CompareSemanticVersion(attributeValue, condition.Value, result => result == 0);
                case ConditionOperator.SEMVER_NEQ:
                    return CompareSemanticVersion(attributeValue, condition.Value, result => result != 0);
                case ConditionOperator.SEMVER_LT:
                    return CompareSemanticVersion(attributeValue, condition.Value, result => result < 0);
                case ConditionOperator.SEMVER_LTE:
                    return CompareSemanticVersion(attributeValue, condition.Value, result => result <= 0);
                case ConditionOperator.SEMVER_GT:
                    return CompareSemanticVersion(attributeValue, condition.Value, result => result > 0);
                case ConditionOperator.SEMVER_GTE:
                    return CompareSemanticVersion(attributeValue, condition.Value, result => result >= 0);
                default:
                    throw new FormatException($"Unknown condition operator {condition.Operator.ToString()}");
            }
        }

        private static bool IsOneOf(object attributeValue, object? conditionValue)
        {
            if (conditionValue is string || conditionValue is not IEnumerable enumerable)
            {
                throw new FormatException($"Condition value is not an array {Convert.ToString(conditionValue ?? "<NULL>")}");
            }

            foreach (var value in enumerable)
            {
                if (CompareString(attributeValue, value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDependencyVariantOneOf(string variant, object? conditionValue)
        {
            if (conditionValue is string || conditionValue is not IEnumerable enumerable)
            {
                throw new FormatException($"Condition value is not an array {Convert.ToString(conditionValue ?? "<NULL>")}");
            }

            var matches = false;
            foreach (var value in enumerable)
            {
                var member = value switch
                {
                    string text => text,
                    JValue { Type: JTokenType.String, Value: string text } => text,
                    _ => throw new FormatException("Flag evaluation condition values must be strings"),
                };

                matches |= string.Equals(variant, member, StringComparison.Ordinal);
            }

            return matches;
        }

        private static int ParseMaxDependencyDepth(ServerConfiguration config, IDatadogLogger log)
        {
            try
            {
                if (!config.EvaluatorParamsField.IsPresent)
                {
                    return DefaultMaxDependencyDepth;
                }

                var evaluatorParams = config.EvaluatorParams;
                if (evaluatorParams is null || evaluatorParams.IsInvalid)
                {
                    throw new FormatException("evaluatorParams must be an object");
                }

                if (!evaluatorParams.MaxDependencyDepthField.IsPresent)
                {
                    return DefaultMaxDependencyDepth;
                }

                var configured = evaluatorParams.MaxDependencyDepth;

                if (configured is null or bool or string || configured is not IConvertible)
                {
                    throw new FormatException("maxDependencyDepth must be an integer from 0 through 255");
                }

                var value = Convert.ToDouble(configured, CultureInfo.InvariantCulture);
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 255 || Math.Truncate(value) != value)
                {
                    throw new FormatException("maxDependencyDepth must be an integer from 0 through 255");
                }

                return (int)value;
            }
            catch (Exception ex)
            {
                log.Error(ex, "Feature Flags evaluator options are malformed; using the default maximum dependency depth of 10");
                return DefaultMaxDependencyDepth;
            }
        }

        private static Evaluation Error(string flagKey, object? defaultValue, string code, string? message, string consent, string? error = null)
        {
            var metadata = ConsentMetadata(consent);
            metadata[MetadataErrorCode] = code;
            if (message is not null)
            {
                metadata["message"] = message;
            }

            return new Evaluation(flagKey, defaultValue, EvaluationReason.Error, error: error ?? code, metadata: metadata);
        }

        private static Dictionary<string, string> ConsentMetadata(string consent)
            => new() { [FeatureFlagMetadataKeys.ObserveFullEvaluationData] = consent };

        private static bool CompareString(object a, object? b)
        {
            if (b is null)
            {
                return false;
            }

            if (Equals(a, b))
            {
                return true;
            }

            if (a is string aTxt && b is string bTxt)
            {
                return aTxt.Equals(bTxt);
            }

            return string.Equals(ToString(a), ToString(b), StringComparison.Ordinal);

            static string? ToString(object obj)
            {
                if (obj is bool boolObj)
                {
                    return boolObj switch
                    {
                        true => "true",
                        _ => "false",
                    };
                }

                return Convert.ToString(obj);
            }
        }

        private static bool CompareNumber(object attributeValue, object? conditionValue, NumberEquality comparator)
        {
            if (conditionValue is null)
            {
                throw new FormatException("Condition value must not be null");
            }

            var a = ParseDouble(attributeValue);
            var b = ParseDouble(conditionValue);
            return comparator(a, b);
        }

        private static bool CompareSemanticVersion(object attributeValue, object? conditionValue, Func<int, bool> comparator)
        {
            return attributeValue is string attributeText
                && conditionValue is string conditionText
                && SemanticVersion.TryParse(attributeText, out var attributeVersion)
                && SemanticVersion.TryParse(conditionText, out var conditionVersion)
                && comparator(attributeVersion.CompareTo(conditionVersion));
        }

        private static bool MatchesShard(Shard shard, string? targetingKey)
        {
            if (shard.Ranges is null)
            {
                return false;
            }

            var assignedShard = GetShard(shard.Salt ?? string.Empty, targetingKey, shard.TotalShards);
            foreach (var range in shard.Ranges)
            {
                if (assignedShard >= range.Start && assignedShard < range.End)
                {
                    return true;
                }
            }

            return false;
        }

        [TestingAndPrivateOnly]
        internal static int GetShard(string salt, string? targetingKey, int totalShards)
        {
            if (targetingKey is null)
            {
                throw new MissingTargetingKeyException();
            }

            var hashKey = $"{salt}-{targetingKey}";
#if NETCOREAPP
            // MD5 always produces a 16 byte hash
            Span<byte> hashBytes = stackalloc byte[16];
            Md5Helper.ComputeMd5Hash(hashKey, hashBytes);
#else
            var hashBytes = Md5Helper.ComputeMd5Hash(hashKey);
#endif
            var intFromHash = (long)BinaryPrimitives.ReadUInt32BigEndian(hashBytes);
            return (int)(intFromHash % totalShards);
        }

        private static DateTime? ParseDate(string? dateString)
        {
            if (dateString == null)
            {
                return null;
            }

            // Using Parse instead of ParseExact to support RFC 3339 dates with
            // variable fractional second precision (0-9 digits). ParseExact would
            // require ~10 format strings. Since dates come from our controlled backend,
            // accepting broader date formats is acceptable.
            return DateTime.Parse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static object? ResolveAttribute(string? name, EvaluationContext? context)
        {
            if (name == null || context is null)
            {
                return null;
            }

            // Special case "id": if not present, use targeting key
            if (name == "id" && !context.Attributes.ContainsKey(name))
            {
                return context.TargetingKey;
            }

            return context.GetAttribute(name);
        }

        internal static object? MapValue(ValueType target, object? value)
        {
            if (value is null)
            {
                return default!;
            }

            if (target == ValueType.String)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (target == ValueType.Boolean)
            {
                if (value is IConvertible)
                {
                    if (value is IFormattable && value is not bool)
                    {
                        return (ParseDouble(value) != 0);
                    }

                    return Convert.ToBoolean(value);
                }

                return bool.Parse(value.ToString()!);
            }

            if (target == ValueType.Integer)
            {
                var number = ParseInteger(value);
                return (int)number;
            }

            if (target == ValueType.Numeric)
            {
                var number = ParseDouble(value);
                return (double)number;
            }

            // We return a BCL based representation of the object (basic types, object[] for JTokenType.Array and Dictionary<string, object> for JTokenType.Object
            if (target == ValueType.Json)
            {
                if (value is JObject json)
                {
                    return ConvertToken(json);
                }

                return value;
            }

            throw new ArgumentException($"Type not supported: {target}");

            static object? ConvertToken(JToken token) => token.Type switch
            {
                JTokenType.Object => ConvertObject((JObject)token),
                JTokenType.Array => ConvertArray((JArray)token),
                JTokenType.Integer => (long)token,
                JTokenType.Float => (double)token,
                JTokenType.String => token.ToString(),
                JTokenType.Boolean => (bool)token,
                JTokenType.Null => null,
                _ => ConvertObject(null)
            };

            static Dictionary<string, object?> ConvertObject(JObject? obj)
            {
                var dict = new Dictionary<string, object?>();
                if (obj is not null)
                {
                    foreach (var property in obj.Properties())
                    {
                        dict.Add(property.Name, ConvertToken(property.Value));
                    }
                }

                return dict;
            }

            static object?[] ConvertArray(JArray array)
            {
                return array.Select(ConvertToken).ToArray();
            }
        }

        private static double ParseDouble(object value)
        {
            if (value is string txt)
            {
                return double.Parse(txt, CultureInfo.InvariantCulture);
            }
            else if (value is IConvertible)
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            return double.Parse(Convert.ToString(value)!, CultureInfo.InvariantCulture);
        }

        private static double ParseInteger(object value)
        {
            if (value is string txt)
            {
                return int.Parse(txt, CultureInfo.InvariantCulture);
            }
            else if (value is IConvertible)
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            return int.Parse(Convert.ToString(value)!, CultureInfo.InvariantCulture);
        }

        internal static string? AllocationKey(Evaluation evaluation)
        {
            if (evaluation.FlagMetadata == null)
            {
                return null;
            }

            return evaluation.FlagMetadata.TryGetValue(MetadataAllocationKey, out var key) ? key : null;
        }

        /// <summary>
        /// Returns the structured error code, which is always one of a fixed set, unlike
        /// <see cref="Evaluation.Error"/>, which can carry an exception message.
        /// </summary>
        internal static string? ErrorCode(Evaluation evaluation)
        {
            if (evaluation.FlagMetadata == null)
            {
                return null;
            }

            return evaluation.FlagMetadata.TryGetValue(MetadataErrorCode, out var code) ? code : null;
        }

        internal static IDictionary<string, object?> FlattenContext(EvaluationContext? context)
        {
            var result = new Dictionary<string, object?>();
            if (context is null)
            {
                return result;
            }

            // Exposure context is intentionally scalar-only. Bound the work and never inspect a
            // collection that will be omitted, because customer collection implementations may be
            // arbitrarily large or throw from Count, indexers, or enumeration.
            const int maximumAttributes = 128;
            try
            {
                var inspected = 0;
                foreach (var pair in context.Attributes)
                {
                    if (inspected++ >= maximumAttributes)
                    {
                        break;
                    }

                    if (pair.Value is IList or IDictionary)
                    {
                        continue;
                    }

                    result[pair.Key] = pair.Value;
                }
            }
            catch
            {
                // Exposure construction is best effort and must not change flag resolution.
            }

            return result;
        }

#pragma warning restore SA1204

        /// <summary>
        /// Writes one metadata entry per (feature, destination) of the selected split, under the
        /// destination's prefix. On a repeated key the first valid feature wins for every
        /// destination. Returns the EXPOSURE-destination features for the exposure event.
        /// </summary>
        private static Dictionary<string, object>? AddFeatureMetadata(Dictionary<string, string> metadata, List<Feature>? features)
        {
            if (features is null or { Count: 0 })
            {
                return null;
            }

            Dictionary<string, object>? exposureFeatures = null;
            HashSet<string>? seenKeys = null;
            StringBuilder? types = null;

            foreach (var feature in features)
            {
                seenKeys ??= new HashSet<string>(StringComparer.Ordinal);
                if (!seenKeys.Add(feature.Key))
                {
                    continue;
                }

                var value = NormalizeFeatureValue(feature.Value);
                var written = false;
                foreach (var destination in feature.Destinations)
                {
                    string? prefix;
                    if (destination == FeatureDestinationHook)
                    {
                        prefix = FeatureFlagMetadataKeys.FeaturePrefix;
                    }
                    else if (destination == FeatureDestinationExposure)
                    {
                        prefix = FeatureFlagMetadataKeys.ExposureFeaturePrefix;
                    }
                    else if (destination == FeatureDestinationEvaluation)
                    {
                        prefix = FeatureFlagMetadataKeys.EvaluationFeaturePrefix;
                    }
                    else
                    {
                        WarnUnknownDestination(destination);
                        continue;
                    }

                    metadata[prefix + feature.Key] = FeatureMetadataValue(value);
                    written = true;
                    if (prefix == FeatureFlagMetadataKeys.ExposureFeaturePrefix)
                    {
                        exposureFeatures ??= new Dictionary<string, object>();
                        exposureFeatures[feature.Key] = value;
                    }
                }

                if (written && value is bool or double)
                {
                    types ??= new StringBuilder();
                    types.Append(feature.Key.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(feature.Key).Append(value is bool ? 'b' : 'n');
                }
            }

            if (types is not null)
            {
                metadata[FeatureFlagMetadataKeys.FeatureTypes] = types.ToString();
            }

            return exposureFeatures;

            static void WarnUnknownDestination(string destination)
            {
                if (WarnedDestinations.Count < MaxWarnedDestinations && WarnedDestinations.TryAdd(destination, 0))
                {
                    Log.Warning<string>("Ignoring unknown feature flag split feature destination {Destination}", destination);
                }
            }
        }

        // The contract allows a string, a double or a boolean; other number representations collapse
        // to double so every consumer sees one numeric type.
        private static object NormalizeFeatureValue(object value)
        {
            return value switch
            {
                bool or string or double => value,
                IConvertible convertible => Convert.ToDouble(convertible, CultureInfo.InvariantCulture),
                _ => value,
            };
        }

        private static string FeatureMetadataValue(object value)
        {
            return value switch
            {
                bool b => b ? "true" : "false",
                // "R" keeps the value round-trippable through the string-based metadata channel.
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                _ => (string)value,
            };
        }

        private Evaluation ResolveVariant(
            string flagKey,
            ValueType resultType,
            object? defaultValue,
            Flag flag,
            Split split,
            Allocation allocation,
            EvaluationReason reason,
            DateTime evalTime,
            EvaluationContext? context,
            string consent,
            DependencyEvaluationSession? session)
        {
            var variationKey = split.VariationKey!;

            if (StringUtil.IsNullOrEmpty(flag.Key))
            {
                return ParseError($"Variant not found for: {variationKey}");
            }

            if (StringUtil.IsNullOrEmpty(allocation.Key))
            {
                return ParseError($"Allocation key is null for {flagKey}");
            }

            if (flag.Variations is null || !flag.Variations.TryGetValue(variationKey, out var variant) || variant == null)
            {
                return ParseError($"Variant not found for: {variationKey}");
            }

            var mappedValue = MapValue(resultType, variant.Value);
            var doLog = allocation.DoLog.HasValue && allocation.DoLog.Value;
            var metadata = new Dictionary<string, string>
            {
                [MetadataAllocationKey] = allocation.Key,
                [FeatureFlagMetadataKeys.ObserveFullEvaluationData] = consent,
            };

            // The serial id and doLog feed both APM span enrichment and the exposure hooks, so emit
            // them whenever either consumer can run, not only when span enrichment is enabled.
            // __dd_split_serial_id is omitted when the split carries none.
            if (_spanEnrichmentEnabled || doLog)
            {
                metadata[MetadataDoLog] = doLog ? "true" : "false";

                if (split.SerialId.HasValue)
                {
                    metadata[MetadataSplitSerialId] = split.SerialId.Value.ToString(CultureInfo.InvariantCulture);
                }
            }

            var exposureFeatures = AddFeatureMetadata(metadata, split.Features);

            var evaluation = new Evaluation(
                flagKey,
                mappedValue,
                reason,
                variant: variant.Key,
                metadata: metadata);

            if (doLog && CreateExposure(flagKey, evaluation, evalTime, context, split, exposureFeatures) is { } exposure)
            {
                if (session is null)
                {
                    evaluation.PendingExposure = exposure;
                }
                else
                {
                    session.RecordExposure(flagKey, exposure);
                }
            }
            else if (!doLog)
            {
                Log.Debug<string, string>("Feature Flags: exposure not recorded for {FlagKey}: allocation {AllocationKey} has exposure logging disabled", flagKey, allocation.Key);
            }

            return evaluation;

            Evaluation ParseError(string error)
            {
                return new Evaluation(
                    flagKey,
                    defaultValue,
                    EvaluationReason.Error,
                    error: error,
                    metadata: new Dictionary<string, string>
                    {
                        ["errorCode"] = "PARSE_ERROR",
                        ["message"] = error,
                        [FeatureFlagMetadataKeys.ObserveFullEvaluationData] = consent
                    });
            }
        }

        private Exposure.Model.ExposureEvent? CreateExposure(
            string flagKey,
            Evaluation evaluation,
            DateTime evalTime,
            EvaluationContext? context,
            Split split,
            Dictionary<string, object>? features)
        {
            var allocationKey = AllocationKey(evaluation);
            var variantKey = evaluation.Variant;

            if (allocationKey == null || variantKey == null)
            {
                Log.Debug<string>("Feature Flags: exposure not recorded for {FlagKey}: allocation key or variant key is missing", flagKey);
                return null;
            }

            return new Exposure.Model.ExposureEvent(
                new DateTimeOffset(evalTime).ToUnixTimeMilliseconds(),
                new Exposure.Model.Allocation(allocationKey),
                new Exposure.Model.Flag(flagKey),
                new Exposure.Model.Variant(variantKey),
                new Exposure.Model.Subject(context?.TargetingKey ?? string.Empty, FlattenContext(context)),
                split.SerialId,
                features);
        }

        private readonly struct ExposureCheckpoint(int exposureCount, int? frameExposureCount)
        {
            public int ExposureCount { get; } = exposureCount;

            public int? FrameExposureCount { get; } = frameExposureCount;
        }

        private readonly struct ExposureCandidate(string flagKey, Exposure.Model.ExposureEvent exposure)
        {
            public string FlagKey { get; } = flagKey;

            public Exposure.Model.ExposureEvent Exposure { get; } = exposure;
        }

        private sealed class DependencyEvaluationSession
        {
            private readonly FeatureFlagsEvaluator _owner;
            private readonly ServerConfiguration _config;
            private readonly EvaluationContext? _context;
            private readonly DateTime _evaluationTime;
            private readonly string _consent;
            private readonly bool _collectDependencyEvaluations;
            private readonly string _rootFlagKey;
            private readonly string? _targetingKey;
            private readonly bool _includeContext;
            private HashSet<string>? _exposureKeys;
            private List<ExposureCandidate>? _exposures;
            private Dictionary<string, MemoizedDependency>? _memo;
            private Dictionary<string, EvaluationSlot>? _evaluationSlots;
            private HashSet<string>? _active;
            private List<DependencyFrame>? _frames;
            private List<EvaluationSlot>? _evaluationOrder;
            private IReadOnlyDictionary<string, object?>? _contextAttributes;
            private ContextOmissionReason _contextOmissions;
            private bool _contextSnapshotted;
            private int? _maxDepth;

            public DependencyEvaluationSession(
                FeatureFlagsEvaluator owner,
                ServerConfiguration config,
                string rootFlagKey,
                EvaluationContext? context,
                DateTime evaluationTime,
                string consent,
                bool collectDependencyEvaluations)
            {
                _owner = owner;
                _config = config;
                _context = context;
                _evaluationTime = evaluationTime;
                _consent = consent;
                _collectDependencyEvaluations = collectDependencyEvaluations;
                _rootFlagKey = rootFlagKey;
                _targetingKey = context?.TargetingKey;
                _includeContext = collectDependencyEvaluations && consent == "true";
            }

            public ExposureCheckpoint Checkpoint()
            {
                var frame = _frames is not { Count: > 0 } ? null : _frames[_frames.Count - 1];
                return new ExposureCheckpoint(_exposures?.Count ?? 0, frame?.ExposureTrace.Count);
            }

            public void Rollback(ExposureCheckpoint checkpoint)
            {
                if (_exposures is not null && _exposureKeys is not null)
                {
                    RemoveTail(_exposures, _exposureKeys, checkpoint.ExposureCount);
                }

                if (checkpoint.FrameExposureCount is { } count && _frames is { Count: > 0 })
                {
                    var trace = _frames[_frames.Count - 1].ExposureTrace;
                    RemoveTail(trace, _frames[_frames.Count - 1].ExposureKeys, count);
                }
            }

            public string Resolve(string flagKey)
            {
                var memo = _memo ??= new Dictionary<string, MemoizedDependency>();
                var active = _active ??= new HashSet<string> { _rootFlagKey };
                var frames = _frames ??= [];
                if (flagKey != _rootFlagKey)
                {
                    ReserveEvaluation(flagKey);
                }

                var depth = frames.Count + 1;
                var maxDepth = _maxDepth ??= _owner.MaxDependencyDepth;

                if (depth > maxDepth)
                {
                    if (!memo.ContainsKey(flagKey) && !active.Contains(flagKey))
                    {
                        EmitDepthError(flagKey);
                    }

                    throw new DependencyGraphException("GENERAL", "maximum flag dependency depth exceeded", flagKey);
                }

                if (active.Contains(flagKey))
                {
                    throw new DependencyGraphException("GENERAL", "flag dependency cycle detected", flagKey);
                }

                if (memo.TryGetValue(flagKey, out var memoized))
                {
                    if (depth + memoized.DescendantDepth > maxDepth)
                    {
                        throw new DependencyGraphException("GENERAL", "maximum flag dependency depth exceeded", flagKey);
                    }

                    RecordReachedDepth(depth + memoized.DescendantDepth);
                    RecordExposureTrace(memoized.ExposureTrace);
                    return memoized.Variant;
                }

                Flag? flag = null;
                var lookup = _owner._config?.Flags?.Find(flagKey, out flag) ?? FlagLookupResult.NotFound;
                if (lookup == FlagLookupResult.NotFound)
                {
                    var missing = Error(flagKey, DefaultValue(ValueType.String), "FLAG_NOT_FOUND", "prerequisite flag is missing", _consent);
                    EmitEvaluation(missing);
                    throw new DependencyGraphException("FLAG_NOT_FOUND", "prerequisite flag is missing", flagKey);
                }

                if (lookup == FlagLookupResult.Invalid || flag?.VariationType is null)
                {
                    var invalid = Error(flagKey, null, "PARSE_ERROR", "prerequisite flag is invalid", _consent);
                    EmitEvaluation(invalid);
                    throw new DependencyGraphException("PARSE_ERROR", "prerequisite flag is invalid", flagKey);
                }

                var type = flag.VariationType.Value;
                var defaultValue = DefaultValue(type);
                active.Add(flagKey);
                var frame = new DependencyFrame(depth);
                frames.Add(frame);
                MemoizedDependency? resolved = null;
                try
                {
                    var evaluation = _owner.EvaluateFlag(flagKey, type, defaultValue, _context, this, _evaluationTime, _consent);
                    if (evaluation.Reason == EvaluationReason.Error)
                    {
                        throw new DependencyGraphException(ErrorCode(evaluation) ?? "GENERAL", evaluation.Error ?? "prerequisite flag evaluation failed", flagKey);
                    }

                    if (evaluation.Variant is not { } variant)
                    {
                        throw new DependencyGraphException("GENERAL", "prerequisite flag evaluation returned no variant", flagKey);
                    }

                    var trace = new List<ExposureCandidate>(frame.ExposureTrace);
                    var result = new MemoizedDependency(variant, frame.MaxReachedDepth - depth, trace);
                    memo.Add(flagKey, result);
                    resolved = result;
                    EmitEvaluation(evaluation);
                    return evaluation.Variant;
                }
                catch (DependencyGraphException ex)
                {
                    var errorCode = ex.OriginFlagKey is not null && ex.OriginFlagKey != flagKey ? "GENERAL" : ex.ErrorCode;
                    var failed = Error(flagKey, defaultValue, errorCode, ex.Message, _consent);
                    EmitEvaluation(failed);
                    throw;
                }
                catch (MissingTargetingKeyException)
                {
                    const string message = "prerequisite flag evaluation requires a targeting key";
                    EmitEvaluation(Error(flagKey, defaultValue, "TARGETING_KEY_MISSING", message, _consent));
                    throw new DependencyGraphException("TARGETING_KEY_MISSING", message, flagKey);
                }
                catch (FormatException ex)
                {
                    EmitEvaluation(Error(flagKey, defaultValue, "PARSE_ERROR", ex.Message, _consent));
                    throw new DependencyGraphException("PARSE_ERROR", ex.Message, flagKey);
                }
                catch (Exception)
                {
                    const string message = "prerequisite flag evaluation failed";
                    EmitEvaluation(Error(flagKey, defaultValue, "GENERAL", message, _consent));
                    throw new DependencyGraphException("GENERAL", message, flagKey);
                }
                finally
                {
                    frames.RemoveAt(frames.Count - 1);
                    active.Remove(flagKey);
                    if (resolved is not null)
                    {
                        RecordReachedDepth(depth + resolved.DescendantDepth);
                        RecordExposureTrace(resolved.ExposureTrace);
                    }
                }
            }

            public void RecordExposure(string flagKey, Exposure.Model.ExposureEvent exposure)
            {
                var candidate = new ExposureCandidate(flagKey, exposure);
                _exposures ??= [];
                _exposureKeys ??= [];
                AppendUnique(_exposures, _exposureKeys, candidate);
                if (_frames is { Count: > 0 })
                {
                    var frame = _frames[_frames.Count - 1];
                    AppendUnique(frame.ExposureTrace, frame.ExposureKeys, candidate);
                }
            }

            public EvaluationTrace Complete(Evaluation details)
            {
                var exposures = Array.Empty<Exposure.Model.ExposureEvent>();
                if (details.Reason != EvaluationReason.Error && _exposures is { Count: > 0 })
                {
                    exposures = new Exposure.Model.ExposureEvent[_exposures.Count];
                    for (var i = 0; i < exposures.Length; i++)
                    {
                        exposures[i] = _exposures[i].Exposure;
                    }
                }

                return new EvaluationTrace(details, MaterializeEvaluations(), exposures, reachedDependencies: _memo is not null);
            }

            private static object DefaultValue(ValueType type) => type switch
            {
                ValueType.Boolean => false,
                ValueType.Integer => 0,
                ValueType.Numeric => 0d,
                ValueType.String => string.Empty,
                ValueType.Json => new Dictionary<string, object?>(),
                _ => string.Empty,
            };

            private static void AppendUnique(List<ExposureCandidate> target, HashSet<string> keys, ExposureCandidate candidate)
            {
                if (keys.Add(candidate.FlagKey))
                {
                    target.Add(candidate);
                }
            }

            private static void RemoveTail(List<ExposureCandidate> target, HashSet<string> keys, int count)
            {
                for (var i = target.Count - 1; i >= count; i--)
                {
                    keys.Remove(target[i].FlagKey);
                }

                target.RemoveRange(count, target.Count - count);
            }

            private void RecordExposureTrace(List<ExposureCandidate> trace)
            {
                foreach (var candidate in trace)
                {
                    _exposures ??= [];
                    _exposureKeys ??= [];
                    AppendUnique(_exposures, _exposureKeys, candidate);
                    if (_frames is { Count: > 0 })
                    {
                        var frame = _frames[_frames.Count - 1];
                        AppendUnique(frame.ExposureTrace, frame.ExposureKeys, candidate);
                    }
                }
            }

            private void RecordReachedDepth(int depth)
            {
                if (_frames is { Count: > 0 })
                {
                    var frame = _frames[_frames.Count - 1];
                    frame.MaxReachedDepth = Math.Max(frame.MaxReachedDepth, depth);
                }
            }

            private void EmitEvaluation(Evaluation evaluation)
            {
                if (!_collectDependencyEvaluations)
                {
                    return;
                }

                var slot = ReserveEvaluation(evaluation.FlagKey);
                if (slot is null)
                {
                    return;
                }

                if (!_contextSnapshotted)
                {
                    _contextAttributes = DependencyEvaluation.SnapshotContext(_context, _includeContext, out _contextOmissions);
                    _contextSnapshotted = true;
                }

                slot.Complete(DependencyEvaluation.Snapshot(evaluation, _evaluationTime, _targetingKey, _contextAttributes, _contextOmissions));
            }

            private EvaluationSlot? ReserveEvaluation(string flagKey)
            {
                if (!_collectDependencyEvaluations)
                {
                    return null;
                }

                _evaluationSlots ??= new Dictionary<string, EvaluationSlot>();
                if (_evaluationSlots.TryGetValue(flagKey, out var existing))
                {
                    return existing;
                }

                var slot = new EvaluationSlot();
                _evaluationOrder ??= [];
                _evaluationSlots.Add(flagKey, slot);
                _evaluationOrder.Add(slot);
                return slot;
            }

            private DependencyEvaluation[] MaterializeEvaluations()
            {
                if (_evaluationOrder is not { Count: > 0 })
                {
                    return [];
                }

                var completed = new List<DependencyEvaluation>(_evaluationOrder.Count);
                foreach (var slot in _evaluationOrder)
                {
                    if (slot.Evaluation is { } evaluation)
                    {
                        completed.Add(evaluation);
                    }
                }

                return completed.ToArray();
            }

            private void EmitDepthError(string flagKey)
            {
                EmitDependencyError(flagKey, "GENERAL", "maximum flag dependency depth exceeded");
            }

            private void EmitDependencyError(string flagKey, string code, string message)
            {
                Flag? flag = null;
                var lookup = _config.Flags?.Find(flagKey, out flag) ?? FlagLookupResult.NotFound;
                var defaultValue = lookup == FlagLookupResult.Found && flag?.VariationType is { } type ? DefaultValue(type) : null;
                EmitEvaluation(Error(flagKey, defaultValue, code, message, _consent));
            }

            private sealed class DependencyFrame(int maxReachedDepth)
            {
                public int MaxReachedDepth { get; set; } = maxReachedDepth;

                public List<ExposureCandidate> ExposureTrace { get; } = [];

                public HashSet<string> ExposureKeys { get; } = [];
            }

            private sealed class MemoizedDependency(string variant, int descendantDepth, List<ExposureCandidate> exposureTrace)
            {
                public string Variant { get; } = variant;

                public int DescendantDepth { get; } = descendantDepth;

                public List<ExposureCandidate> ExposureTrace { get; } = exposureTrace;
            }

            private sealed class EvaluationSlot
            {
                public DependencyEvaluation? Evaluation { get; private set; }

                public void Complete(DependencyEvaluation evaluation) => Evaluation = evaluation;
            }
        }

        private sealed class MissingTargetingKeyException : Exception
        {
        }

        private sealed class DependencyGraphException(string errorCode, string message, string? originFlagKey = null) : Exception(message)
        {
            public string ErrorCode { get; } = errorCode;

            public string? OriginFlagKey { get; } = originFlagKey;
        }
    }
}
