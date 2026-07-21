// <copyright file="IFunctionBindingsFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

namespace Microsoft.Azure.Functions.Worker.Context.Features
{
    internal interface IFunctionBindingsFeature
    {
    }
}

#pragma warning disable SA1403 // File may only contain a single namespace
namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.Functions
{
#pragma warning restore SA1403
#pragma warning disable SA1402 // File may only contain a single type

    // This duck types with tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/IFunctionContext.cs
    internal class MockFunctionContext : IFunctionContext
    {
        public FunctionDefinitionStruct FunctionDefinition { get; set; }

        public IEnumerable<KeyValuePair<Type, object?>>? Features { get; set; }

        public IDictionary<object, object?>? Items { get; }
    }

    // This duck types with tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/FunctionBindingsFeatureStruct.cs
    internal class MockBindingsFeature
    {
        public IDictionary<string, object?>? TriggerMetadata { get; set; }

        public IDictionary<string, object?>? InputData { get; set; }
    }
#pragma warning restore SA1402
}

#endif
