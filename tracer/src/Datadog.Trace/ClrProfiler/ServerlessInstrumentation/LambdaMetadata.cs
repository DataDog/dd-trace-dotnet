// <copyright file="LambdaMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation;

internal class LambdaMetadata
{
    private const string ExtensionFullPath = "/opt/extensions/datadog-agent";
    internal const string ExtensionPathEnvVar = "_DD_EXTENSION_PATH";
    internal const string FunctionNameEnvVar = "AWS_LAMBDA_FUNCTION_NAME";
    internal const string HandlerEnvVar = "_HANDLER";

    private LambdaMetadata(bool isRunningInLambda, string functionName, string handlerName, string serviceName)
    {
        IsRunningInLambda = isRunningInLambda;
        FunctionName = functionName;
        HandlerName = handlerName;
        ServiceName = serviceName;
    }

    public bool IsRunningInLambda { get; }

    public string FunctionName { get; }

    public string HandlerName { get; }

    public string ServiceName { get; }

    /// <summary>
    /// Gets the paths we don't want to trace when running in Lambda
    /// </summary>
    internal string DefaultHttpClientExclusions => "/2018-06-01/RUNTIME/INVOCATION/";

    public static LambdaMetadata Create(string extensionPath = ExtensionFullPath)
    {
        var functionName = EnvironmentHelpers.GetEnvironmentVariable(FunctionNameEnvVar);

        var isRunningInLambda = !string.IsNullOrEmpty(functionName)
                             && File.Exists(
                                    EnvironmentHelpers.GetEnvironmentVariable(ExtensionPathEnvVar)
                                 ?? extensionPath);

        if (!isRunningInLambda)
        {
            // the other values are irrelevant, so don't bother setting them
            return new LambdaMetadata(isRunningInLambda: false, functionName, handlerName: null, serviceName: null);
        }

        var handlerName = EnvironmentHelpers.GetEnvironmentVariable(HandlerEnvVar);
        var serviceName = handlerName?.IndexOf("::", StringComparison.Ordinal) switch
        {
            null => null, // not provided
            0 => null, // invalid handler name (no assembly)
            -1 => handlerName, // top level function style
            { } i => handlerName.Substring(0, i), // three part function style
        };

        return new LambdaMetadata(isRunningInLambda: true, functionName, handlerName, serviceName);
    }
}
