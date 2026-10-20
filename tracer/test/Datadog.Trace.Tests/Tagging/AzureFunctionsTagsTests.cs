// <copyright file="AzureFunctionsTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
        [InlineData(false)]
        [InlineData(true)]
        public void Properties_RoundTripThroughTheTagDictionary(bool isContainerApps)
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
        [InlineData(false)]
        [InlineData(true)]
        public void Tags_AreEnumerated(bool isContainerApps)
        {
            var tags = CreateTags(isContainerApps);
            var prefix = isContainerApps ? "aca." : "aas.";

            // the four function tags no longer have [Tag] attributes, so prove they still
            // reach the serializer via the base tag dictionary
            tags.ToString().Should().Contain(prefix + "function.name");
        }

        private static AzureFunctionsTags CreateTags(bool isContainerApps)
            => new(isContainerApps)
            {
                ShortName = ShortName,
                FullName = FullName,
                BindingSource = BindingSource,
                TriggerType = TriggerType,
            };
    }
}
