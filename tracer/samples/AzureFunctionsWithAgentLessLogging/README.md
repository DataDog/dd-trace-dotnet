This sample allows you to run an azure function locally, with logs correlation and agentless logging enabled for ILogger.
Prerequisites:
- The datadog tracer must be setup locally (In AAS, it will be available through the extension).
- [Azure functions tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=v4%2Cmacos%2Ccsharp%2Cportal%2Cbash) must be setup

In FunctionWithLogging, run the following commmand in powershell:
- ./run.ps1 -AppDirectory PATH_TO_THE_DATADOG_LIBRARY -ApiKey YOUR_API_KEY

`PATH_TO_THE_DATADOG_LIBRARY` being the folder above the `datadog` folder.