using System;
using System.Collections;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.PlatformHelpers
{
    internal static class AzureAppServicesMetadata
    {
        /// <summary>
        /// Configuration key which is used as a flag to tell us whether we are running in the context of Azure App Services.
        /// </summary>
        internal static readonly string AzureAppServicesContextKey = "DD_AZURE_APP_SERVICES";

        /// <summary>
        /// Example: 8c56d827-5f07-45ce-8f2b-6c5001db5c6f+apm-dotnet-EastUSwebspace
        /// Format: {subscriptionId}+{planResourceGroup}-{hostedInRegion}
        /// </summary>
        internal static readonly string WebsiteOwnerNameKey = "WEBSITE_OWNER_NAME";

        /// <summary>
        /// This is the name of the resource group the site instance is assigned to.
        /// </summary>
        internal static readonly string ResourceGroupKey = "WEBSITE_RESOURCE_GROUP";

        /// <summary>
        /// This is the unique name of the website instance within azure app services.
        /// </summary>
        internal static readonly string SiteNameKey = "WEBSITE_DEPLOYMENT_ID";

        private static readonly Lazy<string> ResourceId = new Lazy<string>(GetResourceIdInternal, LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly Lazy<bool> IsRunningInAzureAppServices = new Lazy<bool>(IsRelevantInternal, LazyThreadSafetyMode.ExecutionAndPublication);

        public static string GetResourceId()
        {
            return ResourceId.Value;
        }

        public static bool IsRelevant()
        {
            return IsRunningInAzureAppServices.Value;
        }

        internal static bool IsRelevantInternal()
        {
            return Environment.GetEnvironmentVariable(AzureAppServicesContextKey) == "1";
        }

        internal static string GetResourceIdInternal()
        {
            string resourceId = null;

            try
            {
                var environmentVariables = Environment.GetEnvironmentVariables();

                string subscriptionId = null;

                if (environmentVariables.Contains(WebsiteOwnerNameKey))
                {
                    var websiteOwner = environmentVariables[WebsiteOwnerNameKey].ToString();
                    var plusSplit = websiteOwner.Split('+');
                    if (plusSplit.Length > 0 && !string.IsNullOrWhiteSpace(plusSplit[0]))
                    {
                        subscriptionId = plusSplit[0];
                    }
                }

                var siteName = GetVariableIfExists(environmentVariables, SiteNameKey);
                var siteResourceGroup = GetVariableIfExists(environmentVariables, ResourceGroupKey);
                var success = true;

                if (subscriptionId == null)
                {
                    success = false;
                    DatadogLogging.RegisterStartupLog(log => log.Warning("Could not successfully retrieve the subscription ID from variable: {0}", WebsiteOwnerNameKey));
                }

                if (siteName == null)
                {
                    success = false;
                    DatadogLogging.RegisterStartupLog(log => log.Warning("Could not successfully retrieve the deployment ID from variable: {0}", SiteNameKey));
                }

                if (siteResourceGroup == null)
                {
                    success = false;
                    DatadogLogging.RegisterStartupLog(log => log.Warning("Could not successfully retrieve the resource group name from variable: {0}", ResourceGroupKey));
                }

                if (success)
                {
                    resourceId = $"/subscriptions/{subscriptionId}/resourcegroups/{siteResourceGroup}/providers/microsoft.web/sites/{siteName}".ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                DatadogLogging.RegisterStartupLog(log => log.Error(ex, "Could not successfully setup the resource id for azure app services."));
            }

            return resourceId;
        }

        private static string GetVariableIfExists(IDictionary environmentVariables, string key)
        {
            if (environmentVariables.Contains(key))
            {
                return environmentVariables[key].ToString();
            }

            return null;
        }
    }
}
