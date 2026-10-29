// <copyright file="AzureFunctionsTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Tagging
{
    public class AzureFunctionsTagsTests
    {
        private const string ShortName = "HttpTrigger1";
        private const string FullName = "Samples.AzureFunctions.HttpTrigger1.Run";
        private const string BindingSource = "Microsoft.Azure.WebJobs.Host.Executors.BindingSource";
        private const string TriggerType = "Http";

        [Theory]
        [InlineData(false, "aas.")]
        [InlineData(true, "aca.")]
        public void Tags_UseEnvironmentSpecificPrefix(bool isContainerApps, string prefix)
        {
            var tags = CreateTags(isContainerApps);

            tags.GetTag(prefix + "function.name").Should().Be(ShortName);
            tags.GetTag(prefix + "function.method").Should().Be(FullName);
            tags.GetTag(prefix + "function.binding").Should().Be(BindingSource);
            tags.GetTag(prefix + "function.trigger").Should().Be(TriggerType);
        }

        [Theory]
        [InlineData(false, "aca.")]
        [InlineData(true, "aas.")]
        public void Tags_DoNotUseTheOtherPrefix(bool isContainerApps, string unexpectedPrefix)
        {
            var tags = CreateTags(isContainerApps);

            tags.GetTag(unexpectedPrefix + "function.name").Should().BeNull();
            tags.GetTag(unexpectedPrefix + "function.method").Should().BeNull();
            tags.GetTag(unexpectedPrefix + "function.binding").Should().BeNull();
            tags.GetTag(unexpectedPrefix + "function.trigger").Should().BeNull();
        }

        [Theory]
        [CombinatorialData]
        public void Facade_ReadsBackWhatWasWritten(bool isContainerApps)
        {
            var tags = CreateTags(isContainerApps);

            tags.ShortName.Should().Be(ShortName);
            tags.FullName.Should().Be(FullName);
            tags.BindingSource.Should().Be(BindingSource);
            tags.TriggerType.Should().Be(TriggerType);
        }

        [Theory]
        [InlineData(false, "aas.")]
        [InlineData(true, "aca.")]
        public void TriggerType_DefaultsToUnknown(bool isContainerApps, string prefix)
        {
            var tags = new AzureFunctionsTags(isContainerApps);

            tags.TriggerType.Should().Be("Unknown");
            tags.GetTag(prefix + "function.trigger").Should().Be("Unknown");
        }

        [Theory]
        [InlineData(false, "aas.")]
        [InlineData(true, "aca.")]
        public void SetRootSpanTags_OnAzureFunctionsTags_UsesPrefixedKeys(bool isContainerApps, string prefix)
        {
            var tags = new AzureFunctionsTags(isContainerApps);

            AzureFunctionsTags.SetRootSpanTags(tags, isContainerApps, ShortName, FullName, BindingSource, TriggerType);

            tags.GetTag(prefix + "function.name").Should().Be(ShortName);
            tags.GetTag(prefix + "function.method").Should().Be(FullName);
            tags.GetTag(prefix + "function.binding").Should().Be(BindingSource);
            tags.GetTag(prefix + "function.trigger").Should().Be(TriggerType);
        }

        [Theory]
        [InlineData(false, "aas.")]
        [InlineData(true, "aca.")]
        public void SetRootSpanTags_OnAzureFunctionsTags_PrefersTheInstanceOverTheFlag(bool isContainerApps, string prefix)
        {
            // the typed path writes through the facade, so the instance's own environment picks
            // the keys. Passing the opposite flag must not produce a mixed-prefix span.
            var tags = new AzureFunctionsTags(isContainerApps);

            AzureFunctionsTags.SetRootSpanTags(tags, !isContainerApps, ShortName, FullName, BindingSource, TriggerType);

            tags.GetTag(prefix + "function.name").Should().Be(ShortName);
            tags.GetTag(prefix + "function.method").Should().Be(FullName);
            tags.GetTag(prefix + "function.binding").Should().Be(BindingSource);
            tags.GetTag(prefix + "function.trigger").Should().Be(TriggerType);

            var unexpectedPrefix = isContainerApps ? "aas." : "aca.";
            tags.GetTag(unexpectedPrefix + "function.name").Should().BeNull();
            tags.GetTag(unexpectedPrefix + "function.method").Should().BeNull();
            tags.GetTag(unexpectedPrefix + "function.binding").Should().BeNull();
            tags.GetTag(unexpectedPrefix + "function.trigger").Should().BeNull();
        }

        [Theory]
        [InlineData(false, "aas.")]
        [InlineData(true, "aca.")]
        public void SetRootSpanTags_OnForeignTags_UsesPrefixedKeys(bool isContainerApps, string prefix)
        {
            // the root span is frequently tagged with something other than AzureFunctionsTags
            // (e.g. AspNetCoreTags), which exercises the non-typed branch
            var tags = new TagsList();

            AzureFunctionsTags.SetRootSpanTags(tags, isContainerApps, ShortName, FullName, BindingSource, TriggerType);

            tags.GetTag(prefix + "function.name").Should().Be(ShortName);
            tags.GetTag(prefix + "function.method").Should().Be(FullName);
            tags.GetTag(prefix + "function.binding").Should().Be(BindingSource);
            tags.GetTag(prefix + "function.trigger").Should().Be(TriggerType);
        }

        [Theory]
        [InlineData(false, "aca.")]
        [InlineData(true, "aas.")]
        public void SetRootSpanTags_OnForeignTags_DoesNotUseTheOtherPrefix(bool isContainerApps, string unexpectedPrefix)
        {
            var tags = new TagsList();

            AzureFunctionsTags.SetRootSpanTags(tags, isContainerApps, ShortName, FullName, BindingSource, TriggerType);

            tags.GetTag(unexpectedPrefix + "function.name").Should().BeNull();
            tags.GetTag(unexpectedPrefix + "function.method").Should().BeNull();
            tags.GetTag(unexpectedPrefix + "function.binding").Should().BeNull();
            tags.GetTag(unexpectedPrefix + "function.trigger").Should().BeNull();
        }

        [Theory]
        [CombinatorialData]
        public void OnlyTheActivePrefixReachesTheSerializer(bool isContainerApps)
        {
            var tags = CreateTags(isContainerApps);
            var expectedPrefix = isContainerApps ? "aca." : "aas.";
            var unexpectedPrefix = isContainerApps ? "aas." : "aca.";

            // EnumerateTags is the path that feeds serialization (ToString only covers debug
            // output), so assert the wire-visible tags here.
            var enumerated = GetTagsSnapshot(tags);

            enumerated.Should().Contain(
                new KeyValuePair<string, string>(expectedPrefix + "function.name", ShortName));
            enumerated.Should().Contain(
                new KeyValuePair<string, string>(expectedPrefix + "function.method", FullName));
            enumerated.Should().Contain(
                new KeyValuePair<string, string>(expectedPrefix + "function.binding", BindingSource));
            enumerated.Should().Contain(
                new KeyValuePair<string, string>(expectedPrefix + "function.trigger", TriggerType));

            enumerated.Should().NotContain(kvp => kvp.Key.StartsWith(unexpectedPrefix, StringComparison.Ordinal));
        }

        private static List<KeyValuePair<string, string>> GetTagsSnapshot(TagsList tags)
        {
            var result = new List<KeyValuePair<string, string>>();
            var processor = new TagCollectorProcessor(result);
            tags.EnumerateTags(ref processor, openTelemetrySemanticsEnabled: false);
            return result;
        }

        private static AzureFunctionsTags CreateTags(bool isContainerApps)
            => new(isContainerApps)
            {
                ShortName = ShortName,
                FullName = FullName,
                BindingSource = BindingSource,
                TriggerType = TriggerType,
            };

        private readonly struct TagCollectorProcessor : IItemProcessor<string>, IItemProcessor<int>
        {
            private readonly List<KeyValuePair<string, string>> _items;

            public TagCollectorProcessor(List<KeyValuePair<string, string>> items)
            {
                _items = items;
            }

            public void Process(TagItem<string> item)
            {
                _items.Add(new(item.Key, item.Value));
            }

            public void Process(TagItem<int> item)
            {
                _items.Add(new(item.Key, item.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }
}
