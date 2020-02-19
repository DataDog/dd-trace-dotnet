using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.PlatformHelpers;
using Xunit;

namespace Datadog.Trace.Tests.PlatformHelpers
{
    public class AzureAppServicesMetadataTests
    {
        private static string _subscriptionId = "8c56d827-5f07-45ce-8f2b-6c5001db5c6f";
        private static string _planResourceGroup = "apm-dotnet";
        private static string _deploymentId = "AzureExampleSiteName";
        private static string _siteResourceGroup = "apm-dotnet--site-resource-group";
        private static string _expectedResourceId =
            $"/subscriptions/{_subscriptionId}/resourcegroups/{_siteResourceGroup}/providers/microsoft.web/sites/{_deploymentId}".ToLowerInvariant();

        [Fact]
        public void ResourceId_Created_WhenAllRequirementsExist()
        {
            SetVariables(_subscriptionId, _deploymentId, _planResourceGroup, _siteResourceGroup);

            var resourceId = AzureAppServicesMetadata.GetResourceIdInternal();

            Assert.Equal(expected: _expectedResourceId, actual: resourceId);

            ClearVariables();
        }

        [Fact]
        public void IsRelevant_True_WhenVariableExists()
        {
            SetVariables(null, null, null, null);

            Assert.True(AzureAppServicesMetadata.IsRelevantInternal());

            ClearVariables();
        }

        [Fact]
        public void IsRelevant_False_WhenVariableDoesNotExist()
        {
            ClearVariables();
            Assert.False(AzureAppServicesMetadata.IsRelevantInternal());
        }

        [Fact]
        public void PopulatesOnlyRootSpans()
        {
            SetVariables(_subscriptionId, _deploymentId, _planResourceGroup, _siteResourceGroup);

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
                rootSpans.Where(s => s.GetTag(Tags.AzureAppServicesResourceId) != _expectedResourceId);

            var nonRootSpansWithTag =
                nonRootSpans.Where(s => s.GetTag(Tags.AzureAppServicesResourceId) == _expectedResourceId);

            Assert.True(!rootSpansMissingExpectedTag.Any(), "All root spans should have the resource id.");
            Assert.True(!nonRootSpansWithTag.Any(), "No non root spans should have the resource id.");

            ClearVariables();
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
            SetVariables(subscriptionId, deploymentId, "some-resource-group", siteResourceGroup);
            Assert.Null(AzureAppServicesMetadata.GetResourceIdInternal());
            ClearVariables();
        }

        private void SetVariables(string subscriptionId, string deploymentId, string planResourceGroup, string siteResourceGroup)
        {
            Environment.SetEnvironmentVariable(AzureAppServicesMetadata.AzureAppServicesContextKey, "1");

            Environment.SetEnvironmentVariable(AzureAppServicesMetadata.WebsiteOwnerNameKey, $"{subscriptionId}+{planResourceGroup}-EastUSwebspace");
            Environment.SetEnvironmentVariable(AzureAppServicesMetadata.ResourceGroupKey, siteResourceGroup);
            Environment.SetEnvironmentVariable(AzureAppServicesMetadata.SiteNameKey, deploymentId);
        }

        private void ClearVariables()
        {
            Environment.SetEnvironmentVariable(AzureAppServicesMetadata.AzureAppServicesContextKey, null);
            Environment.SetEnvironmentVariable(AzureAppServicesMetadata.WebsiteOwnerNameKey, null);
            Environment.SetEnvironmentVariable(AzureAppServicesMetadata.ResourceGroupKey, null);
            Environment.SetEnvironmentVariable(AzureAppServicesMetadata.SiteNameKey, null);
        }
    }
}
