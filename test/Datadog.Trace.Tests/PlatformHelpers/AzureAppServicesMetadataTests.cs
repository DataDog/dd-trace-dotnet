using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.PlatformHelpers;
using Xunit;

namespace Datadog.Trace.Tests.PlatformHelpers
{
    public class AzureAppServicesMetadataTests
    {
        private static readonly List<string> EnvVars = new List<string>()
        {
            AzureAppServices.AzureAppServicesContextKey,
            AzureAppServices.WebsiteOwnerNameKey,
            AzureAppServices.ResourceGroupKey,
            AzureAppServices.SiteNameKey
        };

        private static readonly string SubscriptionId = "8c500027-5f00-400e-8f00-60000000000f";
        private static readonly string PlanResourceGroup = "apm-dotnet";
        private static readonly string DeploymentId = "AzureExampleSiteName";
        private static readonly string SiteResourceGroup = "apm-dotnet-site-resource-group";
        private static readonly string ExpectedResourceId =
            $"/subscriptions/{SubscriptionId}/resourcegroups/{SiteResourceGroup}/providers/microsoft.web/sites/{DeploymentId}".ToLowerInvariant();

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
            Assert.False(metadata.IsRelevant);
        }

        [Fact]
        public void PopulatesOnlyRootSpans()
        {
            SetFromMock(GetMockVariables(SubscriptionId, DeploymentId, PlanResourceGroup, SiteResourceGroup));

            var tracer = new Tracer();

            var rootSpans = new List<Span>();
            var nonRootSpans = new List<Span>();
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

            ClearVariables();
        }

        private IDictionary GetMockVariables(string subscriptionId, string deploymentId, string planResourceGroup, string siteResourceGroup)
        {
            var vars = Environment.GetEnvironmentVariables();
            vars.Add(AzureAppServices.AzureAppServicesContextKey, "1");
            vars.Add(AzureAppServices.WebsiteOwnerNameKey, $"{subscriptionId}+{planResourceGroup}-EastUSwebspace");
            vars.Add(AzureAppServices.ResourceGroupKey, siteResourceGroup);
            vars.Add(AzureAppServices.SiteNameKey, deploymentId);
            return vars;
        }

        private void SetFromMock(IDictionary variables)
        {
            foreach (var envVar in EnvVars)
            {
                Environment.SetEnvironmentVariable(envVar, variables[envVar]?.ToString());
            }
        }

        private void ClearVariables()
        {
            foreach (var envVar in EnvVars)
            {
                Environment.SetEnvironmentVariable(envVar, null);
            }
        }
    }
}
