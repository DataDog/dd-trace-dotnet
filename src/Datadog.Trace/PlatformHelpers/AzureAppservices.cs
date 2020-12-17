using System;
using System.Collections;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.PlatformHelpers
{
    /// <summary>
    /// Helper class for gathering metadata about the execution context in Azure App Services.
    /// References:
    /// https://docs.microsoft.com/en-us/azure/app-service/environment/intro
    /// https://docs.microsoft.com/en-us/azure/azure-functions/functions-app-settings
    /// https://github.com/projectkudu/kudu/wiki/Azure-runtime-environment
    /// </summary>
    internal class AzureAppServices
    {
        /// <summary>
        /// Configuration key which is used as a flag to tell us whether we are running in the context of Azure App Services.
        /// This is set within the applicationHost.xdt file of the Azure Site Extension.
        /// </summary>
        internal const string AzureAppServicesContextKey = "DD_AZURE_APP_SERVICES";

        /// <summary>
        /// Example: 8c500027-5f00-400e-8f00-60000000000f+apm-dotnet-EastUSwebspace
        /// Format: {subscriptionId}+{planResourceGroup}-{hostedInRegion}
        /// </summary>
        internal const string WebsiteOwnerNameKey = "WEBSITE_OWNER_NAME";

        /// <summary>
        /// This is the name of the resource group the site instance is assigned to.
        /// </summary>
        internal const string ResourceGroupKey = "WEBSITE_RESOURCE_GROUP";

        /// <summary>
        /// This is the unique name of the website instance within Azure App Services.
        /// </summary>
        internal const string SiteNameKey = "WEBSITE_SITE_NAME";

        /// <summary>
        /// The version of the functions runtime to use in this function app.
        /// Reference: https://docs.microsoft.com/en-us/azure/azure-functions/functions-app-settings#functions_extension_version
        /// </summary>
        internal const string FunctionsExtensionVersionKey = "FUNCTIONS_EXTENSION_VERSION";

        /// <summary>
        /// This variable is only present in Azure Functions.
        /// Valid values are dotnet, node, java, powershell, and python.
        /// In this context, we will only ever see dotnet.
        /// Reference: https://docs.microsoft.com/en-us/azure/azure-functions/functions-app-settings#functions_extension_version
        /// </summary>
        internal const string FunctionsWorkerRuntimeKey = "FUNCTIONS_WORKER_RUNTIME";

        /// <summary>
        /// The instance name in Azure where the traced application is running.
        /// </summary>
        internal const string InstanceNameKey = "COMPUTERNAME";

        /// <summary>
        /// The instance ID in Azure where the traced application is running.
        /// </summary>
        internal const string InstanceIdKey = "WEBSITE_INSTANCE_ID";

        /// <summary>
        /// The operating system in Azure where the traced application is running.
        /// </summary>
        internal const string OperatingSystemKey = "WEBSITE_OS";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(AzureAppServices));

        static AzureAppServices()
        {
            Metadata = new AzureAppServices(EnvironmentHelpers.GetEnvironmentVariables());
        }

        public AzureAppServices(IDictionary environmentVariables)
        {
            IsRelevant = GetVariableIfExists(AzureAppServicesContextKey, environmentVariables)?.ToBoolean() ?? false;
            if (IsRelevant)
            {
                // Azure App Services Basis
                SubscriptionId = GetSubscriptionId(environmentVariables);
                ResourceGroup = GetVariableIfExists(ResourceGroupKey, environmentVariables);
                SiteName = GetVariableIfExists(SiteNameKey, environmentVariables);
                ResourceId = CompileResourceId();

                InstanceId = GetVariableIfExists(InstanceIdKey, environmentVariables);
                InstanceName = GetVariableIfExists(InstanceNameKey, environmentVariables);
                OperatingSystem = GetVariableIfExists(OperatingSystemKey, environmentVariables);

                // Functions
                FunctionsWorkerRuntime =
                    GetVariableIfExists(
                        FunctionsWorkerRuntimeKey,
                        environmentVariables,
                        i => AzureContext = AzureContext.AzureFunction);
                FunctionsExtensionVersion =
                    GetVariableIfExists(
                        FunctionsExtensionVersionKey,
                        environmentVariables,
                        i => AzureContext = AzureContext.AzureFunction);

                switch (AzureContext)
                {
                    case AzureContext.AzureFunction:
                        SiteKind = "functionapp";
                        SiteType = "function";
                        break;
                    case AzureContext.AzureAppService:
                        SiteKind = "app";
                        SiteType = "app";
                        break;
                }

                try
                {
                    var frameworkDescription = FrameworkDescription.Create();
                    Runtime = frameworkDescription.Name;
                }
                catch (Exception ex)
                {
                    Log.SafeLogError(ex, "Unable to determine runtime for Azure.");
                }
            }
        }

        public static AzureAppServices Metadata { get; set; }

        public bool IsRelevant { get; }

        public string SiteType { get; }

        public string SiteKind { get; }

        public string SubscriptionId { get; }

        public string ResourceGroup { get; }

        public string SiteName { get; }

        public string ResourceId { get; }

        public AzureContext AzureContext { get; private set; } = AzureContext.AzureAppService;

        public string FunctionsExtensionVersion { get; }

        public string FunctionsWorkerRuntime { get; }

        public string InstanceName { get; }

        public string InstanceId { get; }

        public string OperatingSystem { get; }

        public string Runtime { get; }

        private string CompileResourceId()
        {
            string resourceId = null;

            try
            {
                var success = true;
                if (SubscriptionId == null)
                {
                    success = false;
                    Log.Warning("Could not successfully retrieve the subscription ID from variable: {0}", WebsiteOwnerNameKey);
                }

                if (SiteName == null)
                {
                    success = false;
                    Log.Warning("Could not successfully retrieve the deployment ID from variable: {0}", SiteNameKey);
                }

                if (ResourceGroup == null)
                {
                    success = false;
                    Log.Warning("Could not successfully retrieve the resource group name from variable: {0}", ResourceGroupKey);
                }

                if (success)
                {
                    resourceId = $"/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroup}/providers/microsoft.web/sites/{SiteName}".ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Log.SafeLogError(ex, "Could not successfully setup the resource ID for Azure App Services.");
            }

            return resourceId;
        }

        private string GetSubscriptionId(IDictionary environmentVariables)
        {
            try
            {
                var websiteOwner = GetVariableIfExists(WebsiteOwnerNameKey, environmentVariables);
                if (!string.IsNullOrWhiteSpace(websiteOwner))
                {
                    var plusSplit = websiteOwner.Split('+');
                    if (plusSplit.Length > 0 && !string.IsNullOrWhiteSpace(plusSplit[0]))
                    {
                        return plusSplit[0];
                    }
                }
            }
            catch (Exception ex)
            {
                Log.SafeLogError(ex, "Could not successfully retrieve the subscription ID for Azure App Services.");
            }

            return null;
        }

        private string GetVariableIfExists(
            string key,
            IDictionary environmentVariables,
            Action<string> optionalExistsAction = null)
        {
            var value = environmentVariables.GetValueOrDefault<string>(key);

            if (value != null)
            {
                optionalExistsAction?.Invoke(value);
            }

            return value;
        }
    }
}
