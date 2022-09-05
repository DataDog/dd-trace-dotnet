// <copyright file="IValidationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// GraphQL.Validation.ValidationContext proxy
    /// https://github.com/graphql-dotnet/graphql-dotnet/blob/ff28dccd9f318ceb4bcfb421428fb2324e6270f3/src/GraphQL/Validation/ValidationContext.cs
    /// </summary>
    internal interface IValidationContext
    {
        DocumentV5Struct Document { get; }
    }
}
