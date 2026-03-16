// <copyright file="ErrorCodes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.AutoInstrumentation.Generator.Cli.Output;

internal static class ErrorCodes
{
    public const string FileNotFound = "FILE_NOT_FOUND";
    public const string TypeNotFound = "TYPE_NOT_FOUND";
    public const string MethodNotFound = "METHOD_NOT_FOUND";
    public const string AmbiguousOverload = "AMBIGUOUS_OVERLOAD";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string InvalidConfig = "INVALID_CONFIG";
    public const string UnknownKey = "UNKNOWN_KEY";
    public const string GenerationError = "GENERATION_ERROR";
    public const string BadAssembly = "BAD_ASSEMBLY";
}
