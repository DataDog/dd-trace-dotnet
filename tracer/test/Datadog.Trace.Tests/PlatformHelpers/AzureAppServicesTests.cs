// <copyright file="AzureAppServicesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Trace.Tests.PlatformHelpers
{
    [Collection(nameof(AzureAppServicesTestCollection))]
    [AzureAppServicesRestorer]
    public class AzureAppServicesTests
    {
        internal static readonly string DeploymentId = "AzureExampleSiteName";

        private const string AppServiceKind = "app";
        private const string AppServiceType = "app";
        private const string FunctionKind = "functionapp";
        private const string FunctionType = "function";

        private static readonly List<string> EnvVars = new List<string>()
        {
            AzureAppServices.AzureAppServicesContextKey,
            AzureAppServices.WebsiteOwnerNameKey,
            AzureAppServices.ResourceGroupKey,
            AzureAppServices.SiteNameKey
        };

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
            var vars = GetMockVariables(SubscriptionId, DeploymentId, PlanResourceGroup, SiteResourceGroup);
            var metadata = new AzureAppServices(vars);
            Assert.Equal(expected: AzureContext.AzureAppService, actual: metadata.AzureContext);
            Assert.Equal(expected: AppServiceKind, actual: metadata.SiteKind);
            Assert.Equal(expected: AppServiceType, actual: metadata.SiteType);
        }

        [Fact]
        public void AzureContext_AzureFunction_WhenFunctionVariablesPresent()
        {
            var vars = GetMockVariables(
                SubscriptionId,
                DeploymentId,
                PlanResourceGroup,
                SiteResourceGroup,
                functionsVersion: FunctionsVersion,
                functionsRuntime: FunctionsRuntime);

            var metadata = new AzureAppServices(vars);
            Assert.Equal(expected: AzureContext.AzureFunctions, actual: metadata.AzureContext);
            Assert.Equal(expected: FunctionKind, actual: metadata.SiteKind);
            Assert.Equal(expected: FunctionType, actual: metadata.SiteType);
        }

        [Fact]
        public void ResourceId_Created_WhenAllRequirementsExist()
        {
            var vars = GetMockVariables(SubscriptionId, DeploymentId, PlanResourceGroup, SiteResourceGroup);
            var metadata = new AzureAppServices(vars);
            var resourceId = metadata.ResourceId;
            Assert.Equal(expected: ExpectedResourceId, actual: resourceId);
        }

        [Fact]
        public void IsRelevant_True_WhenVariableSetTrue()
        {
            var vars = GetMockVariables(null, null, null, null);
            var metadata = new AzureAppServices(vars);
            Assert.True(metadata.IsRelevant);
        }

        [Fact]
        public void OperatingSystem_Set()
        {
            var vars = GetMockVariables(null, null, null, null);
            var metadata = new AzureAppServices(vars);
            Assert.Equal(expected: "windows", actual: metadata.OperatingSystem);
        }

        [Fact]
        public void InstanceId_Set()
        {
            var vars = GetMockVariables(null, null, null, null);
            var metadata = new AzureAppServices(vars);
            Assert.Equal(expected: "instance_id", actual: metadata.InstanceId);
        }

        [Fact]
        public void InstanceName_Set()
        {
            var vars = GetMockVariables(null, null, null, null);
            var metadata = new AzureAppServices(vars);
            Assert.Equal(expected: "instance_name", actual: metadata.InstanceName);
        }

        [Fact]
        public void Runtime_Set()
        {
            var vars = GetMockVariables(null, null, null, null);
            var metadata = new AzureAppServices(vars);
            Assert.True(metadata.Runtime?.Length > 0);
        }

        [Fact]
        public void IsRelevant_False_WhenVariableDoesNotExist()
        {
            var vars = GetMockVariables(null, null, null, null);
            vars.Remove(AzureAppServices.AzureAppServicesContextKey);
            var metadata = new AzureAppServices(vars);
            Assert.False(metadata.IsRelevant);
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
            var vars = GetMockVariables(subscriptionId, deploymentId, "some-resource-group", siteResourceGroup);
            var metadata = new AzureAppServices(vars);
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
            var vars = GetMockVariables("subscription", "deploymentId", "some-resource-group", "siteResourceGroup", ddTraceDebug: ddTraceDebug);
            var metadata = new AzureAppServices(vars);
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
            var vars = GetMockVariables("subscription", "deploymentId", "some-resource-group", "siteResourceGroup", enableCustomMetrics: customMetrics);
            var metadata = new AzureAppServices(vars);
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
            var vars = GetMockVariables("subscription", "deploymentId", "some-resource-group", "siteResourceGroup", enableCustomTracing: customTracing);
            var metadata = new AzureAppServices(vars);
            Assert.Equal(actual: metadata.CustomTracingEnabled, expected: expectation);
        }

        [Fact]
        public void PopulatesOnlyRootSpans()
        {
            var vars = GetMockVariables(SubscriptionId, DeploymentId, PlanResourceGroup, SiteResourceGroup);
            AzureAppServices.Metadata = new AzureAppServices(vars);
            var tracer = TracerHelper.Create();
            var rootSpans = new List<ISpan>();
            var nonRootSpans = new List<ISpan>();
            var iterations = 5;
            var remaining = iterations;

            while (remaining-- > 0)
            {
                using (var rootScope = tracer.StartActive("root"))
                {
                    rootSpans.Add(rootScope.Span);

                    using (var nestedScope = tracer.StartActive("nest-a"))
                    {
                        nonRootSpans.Add(nestedScope.Span);
                    }

                    using (var nestedScope = tracer.StartActive("nest-b"))
                    {
                        nonRootSpans.Add(nestedScope.Span);

                        using (var doublyNestedScope = tracer.StartActive("nest-b-1"))
                        {
                            nonRootSpans.Add(doublyNestedScope.Span);
                        }
                    }
                }
            }

            Assert.Equal(expected: iterations, actual: rootSpans.Count);
            Assert.NotEmpty(nonRootSpans);

            var rootSpansMissingExpectedTag =
                rootSpans.Where(s => s.GetTag(Tags.AzureAppServicesResourceId) != ExpectedResourceId).ToList();

            var nonRootSpansWithTag =
                nonRootSpans.Where(s => s.GetTag(Tags.AzureAppServicesResourceId) == ExpectedResourceId);

            var newLine = Environment.NewLine;
            var detailedMessage =
                string.Join(
                    Environment.NewLine,
                    rootSpansMissingExpectedTag.Select(
                        r => $"Expected {ExpectedResourceId} {newLine}but received {r.GetTag(Tags.AzureAppServicesResourceId) ?? "NULL"} {newLine}{newLine}({r}) {newLine}"));

            var envVarValues = string.Join(", ", EnvVars.Select(e => $"{e}: {Environment.GetEnvironmentVariable(e)}"));

            Assert.True(!rootSpansMissingExpectedTag.Any(), $"All root spans should have the resource id: {newLine}{envVarValues}{newLine}{detailedMessage}");
            Assert.True(!nonRootSpansWithTag.Any(), "No non root spans should have the resource id.");
        }

        private IDictionary GetMockVariables(
            string subscriptionId,
            string deploymentId,
            string planResourceGroup,
            string siteResourceGroup,
            string ddTraceDebug = null,
            string functionsVersion = null,
            string functionsRuntime = null,
            string enableCustomTracing = null,
            string enableCustomMetrics = null)
        {
            var vars = Environment.GetEnvironmentVariables();

            if (vars.Contains(AzureAppServices.InstanceNameKey))
            {
                // This is the COMPUTERNAME key which we'll remove for consistent testing
                vars.Remove(AzureAppServices.InstanceNameKey);
            }

            if (vars.Contains(ConfigurationKeys.DebugEnabled))
            {
                vars.Remove(ConfigurationKeys.DebugEnabled);
            }

            if (!vars.Contains(ConfigurationKeys.ApiKey))
            {
                // This is a needed configuration for the AAS extension
                vars.Add(ConfigurationKeys.ApiKey, "1");
            }

            vars.Add(AzureAppServices.AzureAppServicesContextKey, "1");
            vars.Add(AzureAppServices.WebsiteOwnerNameKey, $"{subscriptionId}+{planResourceGroup}-EastUSwebspace");
            vars.Add(AzureAppServices.ResourceGroupKey, siteResourceGroup);
            vars.Add(AzureAppServices.SiteNameKey, deploymentId);
            vars.Add(AzureAppServices.OperatingSystemKey, "windows");
            vars.Add(AzureAppServices.InstanceIdKey, "instance_id");
            vars.Add(AzureAppServices.InstanceNameKey, "instance_name");
            vars.Add(ConfigurationKeys.DebugEnabled, ddTraceDebug);

            if (functionsVersion != null)
            {
                vars.Add(AzureAppServices.FunctionsExtensionVersionKey, functionsVersion);
            }

            if (functionsRuntime != null)
            {
                vars.Add(AzureAppServices.FunctionsWorkerRuntimeKey, functionsRuntime);
            }

            vars.Add(AzureAppServices.AasEnableCustomTracing, enableCustomTracing ?? "false");
            vars.Add(AzureAppServices.AasEnableCustomMetrics, enableCustomMetrics ?? "false");

            return vars;
        }
    }
}
