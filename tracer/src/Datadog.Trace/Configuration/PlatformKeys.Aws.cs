// <copyright file="PlatformKeys.Aws.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Configuration;

internal static partial class PlatformKeys
{
    internal static class Aws
    {
        public const string LambdaFunctionName = "AWS_LAMBDA_FUNCTION_NAME";
        public const string LambdaHandler = "_HANDLER";
    }
}
