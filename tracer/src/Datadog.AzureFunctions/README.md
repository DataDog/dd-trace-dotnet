# Datadog Instrumentation Library for .NET Azure Functions
## `Datadog.AzureFunctions`

Add this package to your Azure Functions project to enable Datadog APM tracing.
Further configuration is required for your Azure Functions to send instrumentation data to Datadog.

| Hosting Plan                 | OS               | APM Installation Method                                                                                                                            |
|------------------------------|------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| Flex Consumption plan        | Windows or Linux | Use `Datadog.AzureFunctions` and `Datadog.Serverless.Compat` NuGet packages.                                                                       |
| Consumption plan             | Windows or Linux | Use `Datadog.AzureFunctions` and `Datadog.Serverless.Compat` NuGet packages.                                                                       |
| (Elastic) Premium plan       | Windows or Linux | Use `Datadog.AzureFunctions` and `Datadog.Serverless.Compat` NuGet packages.                                                                       |
| Dedicated plan / App Service | Linux            | Use `Datadog.AzureFunctions` and `Datadog.Serverless.Compat` NuGet packages.                                                                       |
| Dedicated plan / App Service | Windows          | Use the [Datadog Azure App Services Site Extension](https://docs.datadoghq.com/serverless/azure_app_services/azure_app_services_windows/?tab=net). |

For more information, see the Datadog documentation at https://docs.datadoghq.com/serverless/azure_functions/

Contact us with questions or feedback at https://www.datadoghq.com/support/
