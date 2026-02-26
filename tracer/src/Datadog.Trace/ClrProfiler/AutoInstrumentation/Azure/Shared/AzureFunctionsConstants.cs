// <copyright file="AzureFunctionsConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;

internal static class AzureFunctionsConstants
{
    // Used for the operation name of spans created for Azure API Management requests
    public const string AzureApimName = "azure.apim";

    // Used for the operation name of spans created for Azure Functions requests
    public const string AzureFunctionName = "azure_functions.invoke";
}
