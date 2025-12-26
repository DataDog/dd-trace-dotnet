// <copyright file="FeatureFlagsEvaluator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.FeatureFlags
{
    internal sealed class FeatureFlagsEvaluator
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsEvaluator));

        internal static readonly string DateFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        private readonly ReportExposureDelegate? _onExposureEvent;
        private readonly ServerConfiguration? _config;

        public FeatureFlagsEvaluator(ReportExposureDelegate? onExposureEvent, ServerConfiguration? config)
        {
            _onExposureEvent = onExposureEvent;
            _config = config;
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

        public Evaluation Evaluate(string flagKey, ValueType resultType, object? defaultValue, EvaluationContext? context)
        {
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
                            ["errorCode"] = "PROVIDER_NOT_READY"
                        });
                }

                if (StringUtil.IsNullOrEmpty(context?.TargetingKey))
                {
                    return new Evaluation(
                        flagKey,
                        defaultValue,
                        EvaluationReason.Error,
                        error: "TARGETING_KEY_MISSING",
                        metadata: new Dictionary<string, string>
                        {
                            ["errorCode"] = "TARGETING_KEY_MISSING"
                        });
                }

                if (config.Flags is null || !config.Flags.TryGetValue(flagKey, out var flag) || flag is null)
                {
                    return new Evaluation(
                        flagKey,
                        defaultValue,
                        EvaluationReason.Error,
                        error: "FLAG_NOT_FOUND",
                        metadata: new Dictionary<string, string>
                        {
                            ["errorCode"] = "FLAG_NOT_FOUND"
                        });
                }

                if (!flag.Enabled is true)
                {
                    return new Evaluation(
                        flagKey,
                        defaultValue,
                        EvaluationReason.Disabled);
                }

                if (flag.VariationType != resultType)
                {
                    return new Evaluation(
                        flagKey,
                        defaultValue,
                        EvaluationReason.Error,
                        error: "TYPE_MISMATCH",
                        metadata: new Dictionary<string, string>
                        {
                            ["errorCode"] = "TYPE_MISMATCH"
                        });
                }

                if (flag.Allocations is null or { Count: 0 })
                {
                    return new Evaluation(
                        flagKey,
                        defaultValue,
                        EvaluationReason.Default);
                }

                var now = DateTime.UtcNow;
                var targetingKey = context?.TargetingKey ?? string.Empty;

                foreach (var allocation in flag.Allocations)
                {
                    if (!IsAllocationActive(allocation, now))
                    {
                        continue;
                    }

                    if (allocation.Rules is { Count: > 0 } allocationRules)
                    {
                        if (!EvaluateRules(allocationRules, context))
                        {
                            continue;
                        }
                    }

                    if (allocation.Splits is { Count: > 0 })
                    {
                        foreach (var split in allocation.Splits)
                        {
                            var allShardsMatch = true;
                            if (split.Shards is { Count: > 0 } splitShards)
                            {
                                foreach (var shard in splitShards)
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
                                return ResolveVariant(flagKey, resultType, defaultValue, flag, split.VariationKey ?? string.Empty, allocation, now, context);
                            }
                        }
                    }
                }

                // No allocation / split matched – use default
                return new Evaluation(
                    flagKey,
                    defaultValue,
                    EvaluationReason.Default);
            }
            catch (FormatException ex)
            {
                return new Evaluation(
                    flagKey,
                    defaultValue,
                    EvaluationReason.Error,
                    error: "PARSE_ERROR",
                    metadata: new Dictionary<string, string>
                    {
                        ["errorCode"] = "PARSE_ERROR",
                        ["message"] = ex.Message
                    });
            }
            catch (Exception ex)
            {
                return new Evaluation(
                    flagKey,
                    defaultValue,
                    EvaluationReason.Error,
                    error: ex.Message,
                    metadata: new Dictionary<string, string>
                    {
                        ["errorCode"] = "GENERAL",
                        ["message"] = ex.Message
                    });
            }
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

        private static bool EvaluateRules(List<Rule> rules, EvaluationContext? context)
        {
            foreach (var rule in rules)
            {
                if (rule.Conditions is null || rule.Conditions.Count == 0)
                {
                    continue;
                }

                var allConditionsMatch = true;
                foreach (var condition in rule.Conditions)
                {
                    if (!EvaluateCondition(condition, context))
                    {
                        allConditionsMatch = false;
                        break;
                    }
                }

                if (allConditionsMatch)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EvaluateCondition(ConditionConfiguration condition, EvaluationContext? context)
        {
            if (condition.Operator is null)
            {
                throw new FormatException("Condition operator can not be null");
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
                    return MatchesRegex(attributeValue, condition.Value);
                case ConditionOperator.NOT_MATCHES:
                    return !MatchesRegex(attributeValue, condition.Value);
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
                default:
                    return false;
            }
        }

        private static bool MatchesRegex(object attributeValue, object? conditionValue)
        {
            if (conditionValue is null)
            {
                throw new FormatException("Condition value can not be null");
            }

            try
            {
                var pattern = conditionValue?.ToString() ?? string.Empty;
                var regex = new Regex(pattern);
                return regex.IsMatch(ToString(attributeValue));
            }
            catch
            {
                return false;
            }

            static string ToString(object attributeValue)
            {
                if (attributeValue is null) { return string.Empty; }
                if (attributeValue is bool boolValue) { return boolValue ? "true" : "false"; }
                return Convert.ToString(attributeValue, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        private static bool IsOneOf(object attributeValue, object? conditionValue)
        {
            if (conditionValue is not IEnumerable enumerable)
            {
                return false;
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

        private static bool MatchesShard(Shard shard, string targetingKey)
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

        private static int GetShard(string salt, string targetingKey, int totalShards)
        {
            var hashKey = $"{salt}-{targetingKey}";
            var md5Hash = GetMd5Hash(hashKey);
            var first8Chars = md5Hash.Substring(0, Math.Min(8, md5Hash.Length));
            var intFromHash = Convert.ToInt64(first8Chars, 16);
            return (int)(intFromHash % totalShards);
        }

        private static string GetMd5Hash(string input)
        {
            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(bytes);
            var sb = new StringBuilder();
            foreach (var b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        private static DateTime? ParseDate(string? dateString)
        {
            if (dateString == null)
            {
                return null;
            }

            if (DateTime.TryParseExact(
                    dateString,
                    DateFormat,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var dt))
            {
                return dt;
            }

            throw new FormatException("Wrong date format");
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
                var number = ParseDouble(value);
                return (int)number;
            }

            if (target == ValueType.Numeric)
            {
                var number = ParseDouble(value);
                return (double)number;
            }

            if (target == ValueType.Json)
            {
                if (value is JObject)
                {
                    return value.ToString();
                }

                var json = JsonConvert.SerializeObject(value);
                return json;
            }

            throw new ArgumentException($"Type not supported: {target}");
        }

        private static double ParseDouble(object value)
        {
            if (value is string txt)
            {
                return txt.ToLower() switch
                {
                    "true" => 1.0,
                    "false" => 0.0,
                    _ => double.Parse(txt, CultureInfo.InvariantCulture)
                };
            }
            else if (value is IConvertible)
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            return double.Parse(Convert.ToString(value)!, CultureInfo.InvariantCulture);
        }

        private static string? AllocationKey(Evaluation evaluation)
        {
            if (evaluation.FlagMetadata == null)
            {
                return null;
            }

            return evaluation.FlagMetadata.TryGetValue("allocationKey", out var key) ? key : null;
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

        private Evaluation ResolveVariant(
            string flagKey,
            ValueType resultType,
            object? defaultValue,
            Flag flag,
            string variationKey,
            Allocation allocation,
            DateTime evalTime,
            EvaluationContext? context)
        {
            if (flag.Variations is null || !flag.Variations.TryGetValue(variationKey, out var variant) || variant == null)
            {
                return new Evaluation(
                    flagKey,
                    defaultValue,
                    EvaluationReason.Error,
                    error: $"Variant not found for: {variationKey}",
                    metadata: new Dictionary<string, string>
                    {
                        ["errorCode"] = "PARSE_ERROR",
                        ["message"] = $"Variant not found for: {variationKey}"
                    });
            }

            var mappedValue = MapValue(resultType, variant.Value);

            var metadata = new Dictionary<string, string>
            {
                ["flagKey"] = flag.Key ?? string.Empty,
                ["variationType"] = flag.VariationType?.ToString() ?? string.Empty,
                ["allocationKey"] = allocation.Key ?? string.Empty
            };

            var evaluation = new Evaluation(
                flagKey,
                mappedValue,
                EvaluationReason.TargetingMatch,
                variant: variant.Key,
                metadata: metadata);

            var doLog = allocation.DoLog.HasValue && allocation.DoLog.Value;
            if (doLog)
            {
                DispatchExposure(flagKey, evaluation, evalTime, context);
            }

            return evaluation;
        }

        private void DispatchExposure(
            string flagKey,
            Evaluation evaluation,
            DateTime evalTime,
            EvaluationContext? context)
        {
            var allocationKey = AllocationKey(evaluation);
            var variantKey = evaluation.Variant;

            if (allocationKey == null || variantKey == null)
            {
                return;
            }

            var evt = new Exposure.Model.ExposureEvent(
                new DateTimeOffset(evalTime).ToUnixTimeMilliseconds(),
                new Exposure.Model.Allocation(allocationKey),
                new Exposure.Model.Flag(flagKey),
                new Exposure.Model.Variant(variantKey),
                new Exposure.Model.Subject(context?.TargetingKey ?? string.Empty, FlattenContext(context)));

            _onExposureEvent?.Invoke(in evt);
        }

        private record struct FlattenEntry(string Key, object? Value, int Level)
        {
        }

#pragma warning disable SA1201 // Elements should appear in the correct order
        internal delegate void ReportExposureDelegate(in Exposure.Model.ExposureEvent ev);
#pragma warning restore SA1201 // Elements should appear in the correct order
    }
}
