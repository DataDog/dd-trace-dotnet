// <copyright file="IntegrationCategories.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// The instrumentation categories of the integrations of <c>Datadog.Trace</c>, as its source generator gives them to the
/// CallTarget definitions of the native tracer: the <c>InstrumentationCategory</c> of the <c>[InstrumentMethod]</c>
/// attributes (of the integration they decorate, or of their <c>CallTargetType</c>) and of the ADO.NET signatures, Tracing
/// when they don't set it. An integration of several definitions gets the categories of all of them.
/// </summary>
internal static class IntegrationCategories
{
    internal const uint Tracing = 1;

    private const string InstrumentMethodAttribute = "Datadog.Trace.ClrProfiler.InstrumentMethodAttribute";
    private const string AdoNetTargetSignatureAttribute = "Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute/AdoNetTargetSignatureAttribute";

    private static readonly ConditionalWeakTable<ModuleDef, Dictionary<string, uint>> Cache = new();

    public static uint Get(ModuleDef datadogTrace, TypeDef integration)
        => Cache.GetValue(datadogTrace, Read).TryGetValue(integration.FullName, out var categories) ? categories : Tracing;

    private static Dictionary<string, uint> Read(ModuleDef module)
    {
        var categories = new Dictionary<string, uint>(StringComparer.Ordinal);
        if (module.Assembly is { } assembly)
        {
            Add(assembly.CustomAttributes, owner: null);
        }

        foreach (var type in module.GetTypes())
        {
            Add(type.CustomAttributes, type);
        }

        return categories;

        void Add(CustomAttributeCollection attributes, TypeDef? owner)
        {
            foreach (var attribute in attributes)
            {
                var attributeType = attribute.TypeFullName;
                if (attributeType != InstrumentMethodAttribute && attributeType != AdoNetTargetSignatureAttribute)
                {
                    continue;
                }

                var integration = attribute.GetProperty("CallTargetType")?.Argument.Value is TypeSig callTargetType
                                      ? callTargetType.FullName
                                      : attributeType == InstrumentMethodAttribute ? owner?.FullName : null;
                if (integration is null)
                {
                    continue;
                }

                var category = attribute.GetProperty("InstrumentationCategory") is { } property ? property.Argument.Value as uint? ?? 0 : Tracing;
                categories[integration] = categories.TryGetValue(integration, out var known) ? known | category : category;
            }
        }
    }
}
#endif
