// <copyright file="FeatureFlagsHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;

namespace Datadog.Trace.TestHelpers;

internal static class FeatureFlagsHelpers
{
    // ---------------------------------------------------------------------
    // Helpers to build flags (minimal subset)
    // ---------------------------------------------------------------------

    internal static Dictionary<string, Flag> CreateAllFlags()
    {
        var flags = new Dictionary<string, Flag>
        {
            ["simple-string"] = CreateSimpleFlag("simple-string", ValueType.String, "test-value", "on"),
            ["rule-based-flag"] = CreateRuleBasedFlag(),
            ["numeric-rule-flag"] = CreateNumericRuleFlag(),
            ["time-based-flag"] = CreateTimeBasedFlag(),
            ["exposure-flag"] = FeatureFlagsHelpers.CreateExposureFlag(),
        };

        return flags;
    }

    internal static Flag CreateSimpleFlag(string key, ValueType type, object value, string variantKey)
    {
        var variants = new Dictionary<string, Variant>
        {
            [variantKey] = new Variant { Key = variantKey, Value = (string)value },
        };

        var shards = new List<Shard>()
        {
            new Shard() { Salt = ".", TotalShards = 5000, Ranges = new List<ShardRange> { new ShardRange() { Start = 0, End = 5000 } } }
        };

        var splits = new List<Split>
        {
            new Split { Shards = shards, VariationKey = variantKey }
        };

        var allocations = new List<Allocation>
        {
            new Allocation { Key = "alloc1", Splits = splits, DoLog = false }
        };

        return new Flag { Key = key, Enabled = true, VariationType = type, Variations = variants, Allocations = allocations };
    }

    internal static Flag CreateRuleBasedFlag()
    {
        var variants = new Dictionary<string, Variant>
        {
            ["premium"] = new Variant { Key = "premium", Value = "premium" },
            ["basic"] = new Variant { Key = "basic", Value = "basic" },
        };

        var premiumConditions = new List<ConditionConfiguration>
        {
            new ConditionConfiguration { Operator = ConditionOperator.MATCHES, Attribute = "email", Value = "@company\\.com$" },
        };

        var premiumRules = new List<Rule> { new Rule(premiumConditions) };
        var premiumSplits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = "premium" } };
        var premiumAlloc = new Allocation { Key = "premium-alloc", Rules = premiumRules, Splits = premiumSplits, DoLog = false };

        var basicSplits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = "basic" } };
        var basicAlloc = new Allocation { Key = "basic-alloc", Splits = basicSplits, DoLog = false };

        return new Flag { Key = "rule-based-flag", Enabled = true, VariationType = ValueType.String, Variations = variants, Allocations = new List<Allocation> { premiumAlloc, basicAlloc } };
    }

    internal static Flag CreateNumericRuleFlag()
    {
        var variants = new Dictionary<string, Variant>
        {
            ["vip"] = new Variant("vip", "vip"),
            ["regular"] = new Variant("regular", "regular")
        };

        var vipConditions = new List<ConditionConfiguration>
        {
            new ConditionConfiguration { Operator = ConditionOperator.GTE, Attribute = "score", Value = 800 },
        };

        var vipRules = new List<Rule> { new Rule(vipConditions) };
        var vipSplits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = "vip" } };
        var vipAlloc = new Allocation { Key = "vip-alloc", Rules = vipRules, Splits = vipSplits, DoLog = false };

        var regularSplits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = "regular" } };
        var regularAlloc = new Allocation { Key = "regular-alloc", Splits = regularSplits, DoLog = false };

        return new Flag { Key = "numeric-rule-flag", Enabled = true, VariationType = ValueType.String, Variations = variants, Allocations = new List<Allocation> { vipAlloc, regularAlloc } };
    }

    internal static Flag CreateTimeBasedFlag()
    {
        var variants = new Dictionary<string, Variant>
        {
            ["time-limited"] = new Variant("time-limited", "time-limited")
        };

        var splits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = "time-limited" } };
        var alloc = new Allocation { Key = "time-alloc", StartAt = "2022-01-01T00:00:00.000Z", EndAt = "2022-12-31T23:59:59.000Z", Splits = splits, DoLog = false };

        return new Flag { Key = "time-based-flag", Enabled = true, VariationType = ValueType.String, Variations = variants, Allocations = new List<Allocation> { alloc } };
    }

    internal static Flag CreateExposureFlag()
    {
        var variants = new Dictionary<string, Variant>
        {
            ["tracked"] = new Variant("tracked", "tracked-value")
        };

        var splits = new List<Split> { new Split { VariationKey = "tracked", Shards = new List<Shard>() } };
        var alloc = new Allocation { Key = "exposure-alloc", Splits = splits, DoLog = true };

        return new Flag { Key = "exposure-flag", Enabled = true, VariationType = ValueType.String, Variations = variants, Allocations = new List<Allocation> { alloc } };
    }
}
