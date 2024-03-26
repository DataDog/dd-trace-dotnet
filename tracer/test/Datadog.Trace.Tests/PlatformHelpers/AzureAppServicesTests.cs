// <copyright file="AzureAppServicesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.PlatformHelpers
{
    public class AzureAppServicesTests
    {
        internal static readonly string DeploymentId = "AzureExampleSiteName";

        private const string AppServiceKind = "app";
        private const string AppServiceType = "app";
        private const string FunctionKind = "functionapp";
        private const string FunctionType = "function";

        private static readonly string SubscriptionId = "8c500027-5f00-400e-8f00-60000000000f";
        private static readonly string PlanResourceGroup = "apm-dotnet";
        private static readonly string SiteResourceGroup = "apm-dotnet-site-resource-group";
        private static readonly string ExpectedResourceId =
            $"/subscriptions/{SubscriptionId}/resourcegroups/{SiteResourceGroup}/providers/microsoft.web/sites/{DeploymentId}".ToLowerInvariant();

        private static readonly string FunctionsVersion = "~3";
        private static readonly string FunctionsRuntime = "dotnet";

        [Fact]
        public void AzureContext_AzureAppService_Default()
        {
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(SubscriptionId, DeploymentId, PlanResourceGroup, SiteResourceGroup);
            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            Assert.Equal(expected: AzureContext.AzureAppService, actual: metadata.AzureContext);
            Assert.Equal(expected: AppServiceKind, actual: metadata.SiteKind);
            Assert.Equal(expected: AppServiceType, actual: metadata.SiteType);
        }

        [Fact]
        public void AzureContext_AzureFunction_WhenFunctionVariablesPresent()
        {
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(
                SubscriptionId,
                DeploymentId,
                PlanResourceGroup,
                SiteResourceGroup,
                functionsVersion: FunctionsVersion,
                functionsRuntime: FunctionsRuntime);

            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            Assert.Equal(expected: AzureContext.AzureFunctions, actual: metadata.AzureContext);
            Assert.Equal(expected: FunctionKind, actual: metadata.SiteKind);
            Assert.Equal(expected: FunctionType, actual: metadata.SiteType);
        }

        [Fact]
        public void ResourceId_Created_WhenAllRequirementsExist()
        {
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(SubscriptionId, DeploymentId, PlanResourceGroup, SiteResourceGroup);
            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            var resourceId = metadata.ResourceId;
            Assert.Equal(expected: ExpectedResourceId, actual: resourceId);
        }

        [Fact]
        public void IsRelevant_True_WhenVariableSetTrue()
        {
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(null, null, null, null);
            var settings = new TracerSettings(vars);
            Assert.True(settings.IsRunningInAzureAppService);
        }

        [Fact]
        public void OperatingSystem_Set()
        {
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(null, null, null, null);
            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            Assert.Equal(expected: "windows", actual: metadata.OperatingSystem);
        }

        [Fact]
        public void InstanceId_Set()
        {
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(null, null, null, null);
            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            Assert.Equal(expected: "instance_id", actual: metadata.InstanceId);
        }

        [Fact]
        public void InstanceName_Set()
        {
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(null, null, null, null);
            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            Assert.Equal(expected: "instance_name", actual: metadata.InstanceName);
        }

        [Fact]
        public void Runtime_Set()
        {
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(null, null, null, null);
            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            Assert.True(metadata.Runtime?.Length > 0);
        }

        [Fact]
        public void IsRelevant_False_WhenVariableDoesNotExist()
        {
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(null, null, null, null, addContextKey: false);
            var metadata = new TracerSettings(vars);
            Assert.False(metadata.IsRunningInAzureAppService);
        }

        [Theory]
        [InlineData(null, "deploymentId", "siteResourceGroup")]
        [InlineData("123", null, "siteResourceGroup")]
        [InlineData("123", "deploymentId", null)]
        [InlineData(null, null, "siteResourceGroup")]
        [InlineData(null, "deploymentId", null)]
        [InlineData("123", null, null)]
        [InlineData(null, null, null)]
        public void ResourceId_IsNull_WhenAnyRequirementsMissing(string subscriptionId, string deploymentId, string siteResourceGroup)
        {
            // plan resource group actually doesn't matter for the resource id we build
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(subscriptionId, deploymentId, "some-resource-group", siteResourceGroup);
            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            Assert.Null(metadata.ResourceId);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("0", false)]
        [InlineData("false", false)]
        [InlineData("f", false)]
        [InlineData("F", false)]
        [InlineData("1", true)]
        [InlineData("t", true)]
        [InlineData("true", true)]
        [InlineData("T", true)]
        public void DebugModeEnabled_Tests(string ddTraceDebug, bool expectation)
        {
            // plan resource group actually doesn't matter for the resource id we build
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues("subscription", "deploymentId", "some-resource-group", "siteResourceGroup", ddTraceDebug: ddTraceDebug);
            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            Assert.Equal(actual: metadata.DebugModeEnabled, expected: expectation);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("0", false)]
        [InlineData("false", false)]
        [InlineData("f", false)]
        [InlineData("F", false)]
        [InlineData("1", true)]
        [InlineData("t", true)]
        [InlineData("true", true)]
        [InlineData("T", true)]
        public void CustomMetricsEnabled_Tests(string customMetrics, bool expectation)
        {
            // plan resource group actually doesn't matter for the resource id we build
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues("subscription", "deploymentId", "some-resource-group", "siteResourceGroup", enableCustomMetrics: customMetrics);
            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            Assert.Equal(actual: metadata.NeedsDogStatsD, expected: expectation);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("0", false)]
        [InlineData("false", false)]
        [InlineData("f", false)]
        [InlineData("F", false)]
        [InlineData("1", true)]
        [InlineData("t", true)]
        [InlineData("true", true)]
        [InlineData("T", true)]
        public void CustomTracingEnabled_Tests(string customTracing, bool expectation)
        {
            // plan resource group actually doesn't matter for the resource id we build
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues("subscription", "deploymentId", "some-resource-group", "siteResourceGroup", enableCustomTracing: customTracing);
            var metadata = new ImmutableAzureAppServiceSettings(vars, NullConfigurationTelemetry.Instance);
            Assert.Equal(actual: metadata.CustomTracingEnabled, expected: expectation);
        }

        [Fact]
        public async Task DoNotTagSpans()
        {
            // AAS Tags are handled at serialization now. So no tags should be set on spans
            var vars = AzureAppServiceHelper.GetRequiredAasConfigurationValues(SubscriptionId, DeploymentId, PlanResourceGroup, SiteResourceGroup);
            var settings = new TracerSettings(vars);
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var spans = new List<ISpan>();
            var iterations = 5;
            var remaining = iterations;

            while (remaining-- > 0)
            {
                using (var rootScope = tracer.StartActive("root"))
                {
                    spans.Add(rootScope.Span);

                    using (var nestedScope = tracer.StartActive("nest-a"))
                    {
                        spans.Add(nestedScope.Span);
                    }

                    using (var nestedScope = tracer.StartActive("nest-b"))
                    {
                        spans.Add(nestedScope.Span);

                        using (var doublyNestedScope = tracer.StartActive("nest-b-1"))
                        {
                            spans.Add(doublyNestedScope.Span);
                        }
                    }
                }
            }

            Assert.NotEmpty(spans);

            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesSiteName) != null);
            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesSiteKind) != null);
            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesSiteType) != null);
            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesResourceGroup) != null);
            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesSubscriptionId) != null);
            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesResourceId) != null);
            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesInstanceId) != null);
            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesInstanceName) != null);
            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesOperatingSystem) != null);
            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesRuntime) != null);
            spans.Should().NotContain(s => s.GetTag(Tags.AzureAppServicesExtensionVersion) != null);
        }
    }
}
