using System;
using Datadog.Trace.PlatformHelpers;
using Xunit;

namespace Datadog.Trace.Tests.PlatformHelpers
{
    public class AzureAppServicesMetadataTests
    {
        [Fact]
        public void ResourceId_Created_WhenAllRequirementsExist()
        {
            var subscriptionId = "8c56d827-5f07-45ce-8f2b-6c5001db5c6f";
            var planResourceGroup = "apm-dotnet";
            var deploymentId = "AzureExampleSiteName";
            var siteResourceGroup = "apm-dotnet--site-resource-group";

            SetVariables(subscriptionId, deploymentId, planResourceGroup, siteResourceGroup);

            var resourceId = AzureAppServicesMetadata.GetResourceIdInternal();
            var expectedResourceId = $"/subscriptions/{subscriptionId}/resourcegroups/{siteResourceGroup}/providers/microsoft.web/sites/{deploymentId}".ToLowerInvariant();

            Assert.Equal(expected: expectedResourceId, actual: resourceId);

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
