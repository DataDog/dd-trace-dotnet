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
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;

namespace Datadog.Trace.FeatureFlags
{
    internal class FeatureFlagsEvaluator
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsEvaluator));

        private static readonly string[] DateFormats =
        {
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            "yyyy-MM-dd'T'HH:mm:ss'Z'"
        };

        private static readonly HashSet<Type> SupportedResolutionTypes =
            new()
            {
                typeof(string),
                typeof(bool),
                typeof(int),
                typeof(double),
                // typeof(Value)
            };

        private readonly Action<Exposure.ExposureEvent>? _onExposureEvent;
        private readonly ServerConfiguration? _config;
        private readonly long _timeoutMs;

        public FeatureFlagsEvaluator(Action<Exposure.ExposureEvent>? onExposureEvent, ServerConfiguration? config, long timeoutMs = 1000)
        {
            _onExposureEvent = onExposureEvent;
            _config = config;
            _timeoutMs = timeoutMs;
        }

        private delegate bool NumberEquality(double a, double b);

        public Evaluation<T> Evaluate<T>(string key, T defaultValue, EvaluationContext context)
        {
            try
            {
                var config = _config;
                if (config == null)
                {
                    return new Evaluation<T>(
                        defaultValue,
                        EvaluationReason.ERROR,
                        error: "PROVIDER_NOT_READY",
                        metadata: new Dictionary<string, string>
                        {
                            ["errorCode"] = "PROVIDER_NOT_READY"
                        });
                }

                if (context == null)
                {
                    return new Evaluation<T>(
                        defaultValue,
                        EvaluationReason.ERROR,
                        error: "INVALID_CONTEXT",
                        metadata: new Dictionary<string, string>
                        {
                            ["errorCode"] = "INVALID_CONTEXT"
                        });
                }

                if (string.IsNullOrEmpty(context.TargetingKey))
                {
                    return new Evaluation<T>(
                        defaultValue,
                        EvaluationReason.ERROR,
                        error: "TARGETING_KEY_MISSING",
                        metadata: new Dictionary<string, string>
                        {
                            ["errorCode"] = "TARGETING_KEY_MISSING"
                        });
                }

                if (config.Flags is null || !config.Flags.TryGetValue(key, out var flag) || flag is null)
                {
                    return new Evaluation<T>(
                        defaultValue,
                        EvaluationReason.ERROR,
                        error: "FLAG_NOT_FOUND",
                        metadata: new Dictionary<string, string>
                        {
                            ["errorCode"] = "FLAG_NOT_FOUND"
                        });
                }

                if (!flag.Enabled is true)
                {
                    return new Evaluation<T>(
                        defaultValue,
                        EvaluationReason.DISABLED);
                }

                if (flag.Allocations is { Count: 0 })
                {
                    return new Evaluation<T>(
                        defaultValue,
                        EvaluationReason.ERROR,
                        error: "Missing allocations",
                        metadata: new Dictionary<string, string>
                        {
                            ["errorCode"] = "GENERAL",
                            ["message"] = $"Missing allocations for flag {flag.Key}"
                        });
                }

                var now = DateTime.UtcNow;
                var targetingKey = context.TargetingKey;

                foreach (var allocation in flag.Allocations!)
                {
                    if (!IsAllocationActive(allocation, now))
                    {
                        continue;
                    }

                    if (allocation.Rules is { Count: > 0 })
                    {
                        if (!EvaluateRules(allocation.Rules!, context))
                        {
                            continue;
                        }
                    }

                    if (allocation.Splits is { Count: > 0 })
                    {
                        foreach (var split in allocation.Splits)
                        {
                            if (split.Shards is { Count: 0 })
                            {
                                return ResolveVariant(key, defaultValue, flag, split.VariationKey ?? string.Empty, allocation, context);
                            }

                            var allShardsMatch = true;
                            foreach (var shard in split.Shards!)
                            {
                                if (!MatchesShard(shard, targetingKey))
                                {
                                    allShardsMatch = false;
                                    break;
                                }
                            }

                            if (allShardsMatch)
                            {
                                return ResolveVariant(key, defaultValue, flag, split.VariationKey ?? string.Empty, allocation, context);
                            }
                        }
                    }
                }

                // No allocation / split matched – use default
                return new Evaluation<T>(
                    defaultValue,
                    EvaluationReason.DEFAULT);
            }
            catch (FormatException ex)
            {
                Log.Debug(ex, "Evaluation failed for key {Key}", key);
                return new Evaluation<T>(
                    defaultValue,
                    EvaluationReason.ERROR,
                    error: "TYPE_MISMATCH",
                    metadata: new Dictionary<string, string>
                    {
                        ["errorCode"] = "TYPE_MISMATCH"
                    });
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Evaluation failed for key {Key}", key);
                return new Evaluation<T>(
                    defaultValue,
                    EvaluationReason.ERROR,
                    error: ex.Message,
                    metadata: new Dictionary<string, string>
                    {
                        ["errorCode"] = "GENERAL",
                        ["message"] = ex.Message
                    });
            }
        }

        // --------- helpers ----------

        private static bool IsAllocationActive(Allocation allocation, DateTime now)
        {
            var startDate = ParseDate(allocation.StartAt);
            if (startDate.HasValue && now < startDate.Value)
            {
                return false;
            }

            var endDate = ParseDate(allocation.EndAt);
            if (endDate.HasValue && now > endDate.Value)
            {
                return false;
            }

            return true;
        }

        private static bool EvaluateRules(IEnumerable<Rule> rules, EvaluationContext context)
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

        private static bool EvaluateCondition(ConditionConfiguration condition, EvaluationContext context)
        {
            if (condition.Operator is null)
            {
                return false;
            }

            if (condition.Operator == ConditionOperator.IS_NULL)
            {
                var value = ResolveAttribute(condition.Attribute, context);
                var isNull = value == null;
                var expectedNull = condition.Value is bool b ? b : true;
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
                return false;
            }

            try
            {
                var pattern = conditionValue?.ToString() ?? string.Empty;
                var regex = new Regex(pattern);
                return regex.IsMatch(attributeValue?.ToString() ?? string.Empty);
            }
            catch
            {
                return false;
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
                if (ValuesEqual(attributeValue, value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ValuesEqual(object a, object? b)
        {
            if (b is null)
            {
                return false;
            }

            if (Equals(a, b))
            {
                return true;
            }

            if (a is IConvertible || b is IConvertible)
            {
                return CompareNumber(a, b, (first, second) => Math.Abs(first - second) < double.Epsilon);
            }

            return string.Equals(Convert.ToString(a), Convert.ToString(b), StringComparison.Ordinal);
        }

        private static bool CompareNumber(object attributeValue, object? conditionValue, NumberEquality comparator)
        {
            if (conditionValue is null)
            {
                return false;
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

            foreach (var fmt in DateFormats)
            {
                if (DateTime.TryParseExact(
                        dateString,
                        fmt,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var dt))
                {
                    return dt;
                }
            }

            return null;
        }

        private static object? ResolveAttribute(string? name, EvaluationContext context)
        {
            if (name == null)
            {
                return null;
            }

            // Special case "id": if not present, use targeting key
            if (name == "id" && !context.Values.ContainsKey(name))
            {
                return context.TargetingKey;
            }

            var resolved = context.GetValue(name);
            return context.ConvertValue(resolved) ?? resolved;
        }

        internal static T MapValue<T>(object? value)
        {
            if (value is null)
            {
                return default!;
            }

            var target = typeof(T);

            if (!SupportedResolutionTypes.Contains(target))
            {
                throw new ArgumentException($"Type not supported: {target}");
            }

            if (target.IsInstanceOfType(value))
            {
                return (T)value;
            }

            if (target == typeof(string))
            {
                return (T)(object)Convert.ToString(value, CultureInfo.InvariantCulture)!;
            }

            if (target == typeof(bool))
            {
                if (value is IConvertible)
                {
                    if (value is IFormattable && value is not bool)
                    {
                        return (T)(object)(ParseDouble(value) != 0);
                    }

                    return (T)(object)Convert.ToBoolean(value);
                }

                return (T)(object)bool.Parse(value.ToString()!);
            }

            if (target == typeof(int))
            {
                var number = ParseDouble(value);
                return (T)(object)(int)number;
            }

            if (target == typeof(double))
            {
                var number = ParseDouble(value);
                return (T)(object)number;
            }

            return default!;
        }

        private static double ParseDouble(object value)
        {
            if (value is IConvertible)
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            return double.Parse(Convert.ToString(value)!);
        }

        private static string? AllocationKey<T>(Evaluation<T> evaluation)
        {
            if (evaluation.Metadata == null)
            {
                return null;
            }

            return evaluation.Metadata.TryGetValue("allocationKey", out var key) ? key : null;
        }

        internal static IDictionary<string, object?> FlattenContext(EvaluationContext context)
        {
            var keys = context.Values.Keys;
            var result = new Dictionary<string, object?>();
            var seen = new HashSet<object>();

            foreach (var key in keys)
            {
                var stack = new Stack<FlattenEntry>();
                stack.Push(new FlattenEntry(key, context.GetValue(key)));

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
                                stack.Push(new FlattenEntry($"{entry.Key}[{i}]", list[i]));
                            }
                        }
                        else if (value is IDictionary dict)
                        {
                            foreach (var pairKey in dict.Keys)
                            {
                                stack.Push(new FlattenEntry($"{entry.Key}.{pairKey}", dict[pairKey!]));
                            }
                        }
                        else
                        {
                            result[entry.Key] = context.ConvertValue(value) ?? value;
                        }
                    }
                }
            }

            return result;
        }

        private Evaluation<T> ResolveVariant<T>(
            string key,
            T defaultValue,
            Flag flag,
            string variationKey,
            Allocation allocation,
            EvaluationContext context)
        {
            if (flag.Variations is null || !flag.Variations.TryGetValue(variationKey, out var variant) || variant == null)
            {
                return new Evaluation<T>(
                    defaultValue,
                    EvaluationReason.ERROR,
                    error: $"Variant not found for: {variationKey}",
                    metadata: new Dictionary<string, string>
                    {
                        ["errorCode"] = "GENERAL",
                        ["message"] = $"Variant not found for: {variationKey}"
                    });
            }

            var mappedValue = MapValue<T>(variant.Value);

            var metadata = new Dictionary<string, string>
            {
                ["flagKey"] = flag.Key ?? string.Empty,
                ["variationType"] = flag.VariationType?.ToString() ?? string.Empty,
                ["allocationKey"] = allocation.Key ?? string.Empty
            };

            var evaluation = new Evaluation<T>(
                mappedValue,
                EvaluationReason.TARGETING_MATCH,
                variant: variant.Key,
                metadata: metadata);

            var doLog = allocation.DoLog.HasValue && allocation.DoLog.Value;
            if (doLog)
            {
                DispatchExposure(key, evaluation, context);
            }

            return evaluation;
        }

        private void DispatchExposure<T>(
            string flag,
            Evaluation<T> evaluation,
            EvaluationContext context)
        {
            var allocationKey = AllocationKey(evaluation);
            var variantKey = evaluation.Variant;

            if (allocationKey == null || variantKey == null)
            {
                return;
            }

            var evt = new Exposure.ExposureEvent(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new Exposure.Allocation(allocationKey),
                new Exposure.Flag(flag),
                new Exposure.Variant(variantKey),
                new Exposure.Subject(context.TargetingKey, FlattenContext(context)));

            _onExposureEvent?.Invoke(evt);
        }

        private sealed class FlattenEntry(string key, object? value)
        {
            public string Key { get; } = key;

            public object? Value { get; } = value;
        }
    }
}
