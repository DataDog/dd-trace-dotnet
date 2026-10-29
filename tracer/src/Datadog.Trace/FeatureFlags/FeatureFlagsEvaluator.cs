// <copyright file="FeatureFlagsEvaluator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsEvaluator));

        private const int DefaultMaxDependencyDepth = 10;

        private readonly ReportExposureDelegate? _onExposureEvent;
        private readonly ReportDependencyEvaluationDelegate? _onDependencyEvaluation;
        private readonly ServerConfiguration? _config;
        private readonly bool _spanEnrichmentEnabled;

        public FeatureFlagsEvaluator(
            ReportExposureDelegate? onExposureEvent,
            ServerConfiguration? config,
            bool spanEnrichmentEnabled = false,
            ReportDependencyEvaluationDelegate? onDependencyEvaluation = null)
        {
            _onExposureEvent = onExposureEvent;
            _onDependencyEvaluation = onDependencyEvaluation;
            _config = config;
            _spanEnrichmentEnabled = spanEnrichmentEnabled;
            if (_config is null)
            {
                Log.Debug("Creating Evaluator without config");
            }
            else
            {
                Log.Debug<int>("Creating Evaluator with {Flags} flags", _config.Flags?.Count ?? 0);
            }
        }

        private delegate bool NumberEquality(double a, double b);

        /// <summary>
        /// Gets the configuration this evaluator resolves against, which diagnostics use to identify it.
        /// </summary>
        internal ServerConfiguration? Configuration => _config;

        public Evaluation Evaluate(string flagKey, ValueType resultType, object? defaultValue, EvaluationContext? context)
        {
            // Capture before evaluating: even an exception after a configuration update keeps this consent.
            var consent = _config?.ObserveFullEvaluationData == true ? "true" : "false";
            try
            {
                var config = _config;
                if (config == null)
                {
                    return new Evaluation(
                        flagKey,
                        defaultValue,
                        EvaluationReason.Error,
                        error: "PROVIDER_NOT_READY",
                        metadata: new Dictionary<string, string>
                        {
                            ["errorCode"] = "PROVIDER_NOT_READY",
                            [FeatureFlagMetadataKeys.ObserveFullEvaluationData] = consent
                        });
                }

                var now = DateTime.UtcNow;
                var session = new DependencyEvaluationSession(this, config, flagKey, context, now, consent);
                var evaluation = EvaluateFlag(flagKey, resultType, defaultValue, context, session, now, consent);
                if (evaluation.Reason != EvaluationReason.Error)
                {
                    session.CommitExposures();
                }

                return evaluation;
            }
            catch (DependencyGraphException ex)
            {
                return Error(flagKey, defaultValue, ex.ErrorCode, ex.Message, consent);
            }
            catch (FormatException ex)
            {
                return Error(flagKey, defaultValue, "PARSE_ERROR", ex.Message, consent);
            }
            catch (MissingTargetingKeyException)
            {
                return Error(flagKey, defaultValue, "TARGETING_KEY_MISSING", null, consent);
            }
            catch (Exception ex)
            {
                return Error(flagKey, defaultValue, "GENERAL", ex.Message, consent, ex.Message);
            }
        }

#pragma warning disable SA1204 // Static helpers follow this recursive instance method for evaluator readability
        private Evaluation EvaluateFlag(
            string flagKey,
            ValueType resultType,
            object? defaultValue,
            EvaluationContext? context,
            DependencyEvaluationSession session,
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

                var checkpoint = session.Checkpoint();
                var hadRules = allocation.Rules is { Count: > 0 };
                if (hadRules && !EvaluateRules(allocation.Rules!, context, session))
                {
                    session.Rollback(checkpoint);
                    continue;
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
                        var hadShards = split.Shards is { Count: > 0 };
                        if (hadShards)
                        {
                            foreach (var shard in split.Shards!)
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

                session.Rollback(checkpoint);
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

        private static bool EvaluateRules(List<Rule> rules, EvaluationContext? context, DependencyEvaluationSession session)
        {
            foreach (var rule in rules)
            {
                if (rule.Conditions is null || rule.Conditions.Count == 0)
                {
                    continue;
                }

                var checkpoint = session.Checkpoint();
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

                session.Rollback(checkpoint);
            }

            return false;
        }

        private static bool EvaluateCondition(ConditionConfiguration condition, EvaluationContext? context, DependencyEvaluationSession session)
        {
            if (condition.Operator is null)
            {
                throw new FormatException("Condition operator can not be null");
            }

            var hasAttribute = condition.AttributePresent;
            var hasFlagEvaluation = condition.FlagEvaluationPresent;
            if (hasAttribute == hasFlagEvaluation)
            {
                throw new FormatException("Condition must contain exactly one of attribute or flagEvaluation");
            }

            if (hasFlagEvaluation)
            {
                if (condition.Operator is not ConditionOperator.ONE_OF and not ConditionOperator.NOT_ONE_OF)
                {
                    throw new FormatException("Flag evaluation conditions only support ONE_OF and NOT_ONE_OF");
                }

                var descriptor = condition.FlagEvaluation;
                if (descriptor is null
                 || !descriptor.KeyPresent
                 || descriptor.Key is not string { Length: > 0 } key
                 || descriptor.AdditionalProperties is { Count: > 0 })
                {
                    throw new FormatException("Flag evaluation must contain only a non-empty string key");
                }

                var variant = session.Resolve(key);
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

        private static int GetMaxDependencyDepth(ServerConfiguration config)
        {
            if (!config.EvaluatorParamsPresent)
            {
                return DefaultMaxDependencyDepth;
            }

            var evaluatorParams = config.EvaluatorParams;
            if (evaluatorParams is null || !evaluatorParams.MaxDependencyDepthPresent)
            {
                if (evaluatorParams is not null)
                {
                    return DefaultMaxDependencyDepth;
                }

                throw new FormatException("evaluatorParams must be an object");
            }

            var configured = evaluatorParams.MaxDependencyDepth;

            if (configured is null or bool or string || configured is not IConvertible)
            {
                throw new FormatException("maxDependencyDepth must be an integer from 0 through 255");
            }

            try
            {
                var value = Convert.ToDouble(configured, CultureInfo.InvariantCulture);
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 255 || Math.Truncate(value) != value)
                {
                    throw new FormatException("maxDependencyDepth must be an integer from 0 through 255");
                }

                return (int)value;
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FormatException("maxDependencyDepth must be an integer from 0 through 255", ex);
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
            if (context is not null)
            {
                var seen = new HashSet<object>();
                var keys = context.Attributes.Keys;
                foreach (var key in keys)
                {
                    var stack = new Stack<FlattenEntry>();
                    stack.Push(new FlattenEntry(key, context.GetAttribute(key), 1));

                    while (stack.Count > 0)
                    {
                        var entry = stack.Pop();
                        var value = entry.Value;

                        // For now only return first level entries
                        if (entry.Level > 1)
                        {
                            continue;
                        }

                        if (value == null || seen.Add(value))
                        {
                            if (value == null)
                            {
                                result[entry.Key] = null;
                            }
                            else if (value is IList list)
                            {
                                for (var i = 0; i < list.Count; i++)
                                {
                                    stack.Push(new FlattenEntry($"{entry.Key}[{i}]", list[i], entry.Level + 1));
                                }
                            }
                            else if (value is IDictionary dict)
                            {
                                foreach (var pairKey in dict.Keys)
                                {
                                    stack.Push(new FlattenEntry($"{entry.Key}.{pairKey}", dict[pairKey!], entry.Level + 1));
                                }
                            }
                            else
                            {
                                // Do not overwrite existing keys
                                if (!result.ContainsKey(entry.Key))
                                {
                                    result[entry.Key] = value;
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

#pragma warning restore SA1204
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
            DependencyEvaluationSession session)
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

            // do_log and the split serial id are only consumed by APM span enrichment, so emit them
            // (and the serial-id ToString) only when that feature is enabled. doLog itself is still
            // needed below to drive exposure dispatch.
            if (_spanEnrichmentEnabled)
            {
                metadata[MetadataDoLog] = doLog ? "true" : "false";

                if (split.SerialId.HasValue)
                {
                    metadata[MetadataSplitSerialId] = split.SerialId.Value.ToString(CultureInfo.InvariantCulture);
                }
            }

            var evaluation = new Evaluation(
                flagKey,
                mappedValue,
                reason,
                variant: variant.Key,
                metadata: metadata);

            if (doLog)
            {
                var exposure = CreateExposure(flagKey, evaluation, evalTime, context, split.SerialId);
                if (exposure.HasValue)
                {
                    session.RecordExposure(flagKey, exposure.Value);
                }
            }
            else
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
            long? serialId)
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
                serialId);
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

        private readonly struct FlattenEntry(string key, object? value, int level)
        {
            public string Key { get; } = key;

            public object? Value { get; } = value;

            public int Level { get; } = level;
        }

        private sealed class DependencyEvaluationSession
        {
            private readonly FeatureFlagsEvaluator _owner;
            private readonly ServerConfiguration _config;
            private readonly EvaluationContext? _context;
            private readonly DateTime _evaluationTime;
            private readonly string _consent;
            private readonly Dictionary<string, MemoizedDependency> _memo = new();
            private readonly HashSet<string> _active;
            private readonly List<DependencyFrame> _frames = [];
            private readonly List<ExposureCandidate> _exposures = [];
            private int? _maxDepth;

            public DependencyEvaluationSession(
                FeatureFlagsEvaluator owner,
                ServerConfiguration config,
                string rootFlagKey,
                EvaluationContext? context,
                DateTime evaluationTime,
                string consent)
            {
                _owner = owner;
                _config = config;
                _context = context;
                _evaluationTime = evaluationTime;
                _consent = consent;
                _active = new HashSet<string> { rootFlagKey };
            }

            public ExposureCheckpoint Checkpoint()
            {
                var frame = _frames.Count == 0 ? null : _frames[_frames.Count - 1];
                return new ExposureCheckpoint(_exposures.Count, frame?.ExposureTrace.Count);
            }

            public void Rollback(ExposureCheckpoint checkpoint)
            {
                _exposures.RemoveRange(checkpoint.ExposureCount, _exposures.Count - checkpoint.ExposureCount);
                if (checkpoint.FrameExposureCount is { } count && _frames.Count > 0)
                {
                    var trace = _frames[_frames.Count - 1].ExposureTrace;
                    trace.RemoveRange(count, trace.Count - count);
                }
            }

            public string Resolve(string flagKey)
            {
                var depth = _frames.Count + 1;
                var maxDepth = _maxDepth ??= GetMaxDependencyDepth(_config);
                if (depth > maxDepth)
                {
                    if (!_memo.ContainsKey(flagKey) && !_active.Contains(flagKey))
                    {
                        EmitDepthError(flagKey);
                    }

                    throw new DependencyGraphException("GENERAL", "maximum flag dependency depth exceeded");
                }

                if (_active.Contains(flagKey))
                {
                    throw new DependencyGraphException("GENERAL", "flag dependency cycle detected");
                }

                if (_memo.TryGetValue(flagKey, out var memoized))
                {
                    if (depth + memoized.DescendantDepth > maxDepth)
                    {
                        throw new DependencyGraphException("GENERAL", "maximum flag dependency depth exceeded");
                    }

                    RecordReachedDepth(depth + memoized.DescendantDepth);
                    RecordExposureTrace(memoized.ExposureTrace);
                    return memoized.Evaluation.Variant!;
                }

                Flag? flag = null;
                var lookup = _owner._config?.Flags?.Find(flagKey, out flag) ?? FlagLookupResult.NotFound;
                if (lookup == FlagLookupResult.NotFound)
                {
                    var missing = Error(flagKey, DefaultValue(ValueType.String), "FLAG_NOT_FOUND", "prerequisite flag is missing", _consent);
                    EmitEvaluation(missing);
                    throw new DependencyGraphException("FLAG_NOT_FOUND", "prerequisite flag is missing");
                }

                if (lookup == FlagLookupResult.Invalid || flag?.VariationType is null)
                {
                    var invalid = Error(flagKey, null, "PARSE_ERROR", "prerequisite flag is invalid", _consent);
                    EmitEvaluation(invalid);
                    throw new DependencyGraphException("PARSE_ERROR", "prerequisite flag is invalid");
                }

                var type = flag.VariationType.Value;
                var defaultValue = DefaultValue(type);
                _active.Add(flagKey);
                var frame = new DependencyFrame(depth);
                _frames.Add(frame);
                MemoizedDependency? resolved = null;
                try
                {
                    var evaluation = _owner.EvaluateFlag(flagKey, type, defaultValue, _context, this, _evaluationTime, _consent);
                    if (evaluation.Reason == EvaluationReason.Error)
                    {
                        throw new DependencyGraphException(ErrorCode(evaluation) ?? "GENERAL", evaluation.Error ?? "prerequisite flag evaluation failed");
                    }

                    if (evaluation.Variant is null)
                    {
                        throw new DependencyGraphException("GENERAL", "prerequisite flag evaluation returned no variant");
                    }

                    var trace = new List<ExposureCandidate>(frame.ExposureTrace);
                    var result = new MemoizedDependency(evaluation, frame.MaxReachedDepth - depth, trace);
                    _memo.Add(flagKey, result);
                    resolved = result;
                    EmitEvaluation(evaluation);
                    return evaluation.Variant;
                }
                catch (DependencyGraphException ex)
                {
                    var failed = Error(flagKey, defaultValue, ex.ErrorCode, ex.Message, _consent);
                    EmitEvaluation(failed);
                    throw;
                }
                catch (MissingTargetingKeyException)
                {
                    const string message = "prerequisite flag evaluation requires a targeting key";
                    EmitEvaluation(Error(flagKey, defaultValue, "TARGETING_KEY_MISSING", message, _consent));
                    throw new DependencyGraphException("TARGETING_KEY_MISSING", message);
                }
                catch (FormatException ex)
                {
                    EmitEvaluation(Error(flagKey, defaultValue, "PARSE_ERROR", ex.Message, _consent));
                    throw new DependencyGraphException("PARSE_ERROR", ex.Message);
                }
                catch (Exception)
                {
                    const string message = "prerequisite flag evaluation failed";
                    EmitEvaluation(Error(flagKey, defaultValue, "GENERAL", message, _consent));
                    throw new DependencyGraphException("GENERAL", message);
                }
                finally
                {
                    _frames.RemoveAt(_frames.Count - 1);
                    _active.Remove(flagKey);
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
                AppendUnique(_exposures, candidate);
                if (_frames.Count > 0)
                {
                    AppendUnique(_frames[_frames.Count - 1].ExposureTrace, candidate);
                }
            }

            public void CommitExposures()
            {
                foreach (var candidate in _exposures)
                {
                    var exposure = candidate.Exposure;
                    try
                    {
                        _owner._onExposureEvent?.Invoke(in exposure);
                    }
                    catch
                    {
                        // Exposure delivery is best effort and must not affect flag evaluation.
                    }
                }
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

            private static void AppendUnique(List<ExposureCandidate> target, ExposureCandidate candidate)
            {
                if (!target.Any(existing => existing.FlagKey == candidate.FlagKey))
                {
                    target.Add(candidate);
                }
            }

            private void RecordExposureTrace(List<ExposureCandidate> trace)
            {
                foreach (var candidate in trace)
                {
                    AppendUnique(_exposures, candidate);
                    if (_frames.Count > 0)
                    {
                        AppendUnique(_frames[_frames.Count - 1].ExposureTrace, candidate);
                    }
                }
            }

            private void RecordReachedDepth(int depth)
            {
                if (_frames.Count > 0)
                {
                    var frame = _frames[_frames.Count - 1];
                    frame.MaxReachedDepth = Math.Max(frame.MaxReachedDepth, depth);
                }
            }

            private void EmitEvaluation(Evaluation evaluation)
            {
                try
                {
                    _owner._onDependencyEvaluation?.Invoke(evaluation, _evaluationTime, _context);
                }
                catch
                {
                    // Evaluation telemetry is best effort and must not affect flag evaluation.
                }
            }

            private void EmitDepthError(string flagKey)
            {
                Flag? flag = null;
                var lookup = _config.Flags?.Find(flagKey, out flag) ?? FlagLookupResult.NotFound;
                var defaultValue = lookup == FlagLookupResult.Found && flag?.VariationType is { } type ? DefaultValue(type) : null;
                EmitEvaluation(Error(flagKey, defaultValue, "GENERAL", "maximum flag dependency depth exceeded", _consent));
            }

            private sealed class DependencyFrame(int maxReachedDepth)
            {
                public int MaxReachedDepth { get; set; } = maxReachedDepth;

                public List<ExposureCandidate> ExposureTrace { get; } = [];
            }

            private sealed class MemoizedDependency(Evaluation evaluation, int descendantDepth, List<ExposureCandidate> exposureTrace)
            {
                public Evaluation Evaluation { get; } = evaluation;

                public int DescendantDepth { get; } = descendantDepth;

                public List<ExposureCandidate> ExposureTrace { get; } = exposureTrace;
            }
        }

        private sealed class MissingTargetingKeyException : Exception
        {
        }

        private sealed class DependencyGraphException(string errorCode, string message) : Exception(message)
        {
            public string ErrorCode { get; } = errorCode;
        }

#pragma warning disable SA1201 // Elements should appear in the correct order
        internal delegate void ReportExposureDelegate(in Exposure.Model.ExposureEvent ev);

        internal delegate void ReportDependencyEvaluationDelegate(Evaluation evaluation, DateTime evaluationTime, EvaluationContext? context);
#pragma warning restore SA1201 // Elements should appear in the correct order
    }
}
