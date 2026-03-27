// <copyright file="CallTargetAotDefinitionDiscovery.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mono.Cecil;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Discovers direct <c>InstrumentMethodAttribute</c> integrations from tracer metadata without loading integration dependencies.
/// </summary>
internal static class CallTargetAotDefinitionDiscovery
{
    private const string InstrumentMethodAttributeFullName = "Datadog.Trace.ClrProfiler.InstrumentMethodAttribute";
    private const string CallTargetKindFullName = "Datadog.Trace.ClrProfiler.CallTargetKind";

    /// <summary>
    /// Discovers and expands concrete CallTarget definitions from the supplied tracer assembly path.
    /// </summary>
    /// <param name="tracerAssemblyPath">The tracer assembly path.</param>
    /// <returns>The expanded concrete definitions.</returns>
    internal static List<CallTargetAotDefinition> Discover(string tracerAssemblyPath)
    {
        using var tracerAssembly = AssemblyDefinition.ReadAssembly(tracerAssemblyPath, new ReaderParameters { ReadSymbols = false });
        var definitions = new List<CallTargetAotDefinition>();

        foreach (var integrationType in tracerAssembly.MainModule.Types)
        {
            foreach (var attribute in integrationType.CustomAttributes.Where(static candidate => string.Equals(candidate.AttributeType.FullName, InstrumentMethodAttributeFullName, StringComparison.Ordinal)))
            {
                var assemblyNames = GetStringArray(attribute, "AssemblyNames", "AssemblyName");
                var typeNames = GetStringArray(attribute, "TypeNames", "TypeName");
                var methodName = GetString(attribute, "MethodName");
                var returnTypeName = GetString(attribute, "ReturnTypeName");
                var parameterTypeNames = GetStringArray(attribute, "ParameterTypeNames");
                var minimumVersion = ParseMinimumVersion(GetString(attribute, "MinimumVersion"));
                var maximumVersion = ParseMaximumVersion(GetString(attribute, "MaximumVersion"));
                var callTargetKind = GetCallTargetKind(attribute);

                foreach (var assemblyName in assemblyNames)
                {
                    foreach (var typeName in typeNames)
                    {
                        definitions.Add(new CallTargetAotDefinition(
                            assemblyName,
                            typeName,
                            methodName,
                            returnTypeName,
                            parameterTypeNames,
                            integrationType.FullName,
                            minimumVersion,
                            maximumVersion,
                            callTargetKind));
                    }
                }
            }
        }

        return definitions;
    }

    /// <summary>
    /// Reads a string-valued property from a metadata attribute.
    /// </summary>
    /// <param name="attribute">The attribute to inspect.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <returns>The string value, if present.</returns>
    private static string GetString(CustomAttribute attribute, string propertyName)
    {
        foreach (var property in attribute.Properties)
        {
            if (string.Equals(property.Name, propertyName, StringComparison.Ordinal))
            {
                return property.Argument.Value as string ?? string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Reads either an array-valued property or its single-value fallback property from a metadata attribute.
    /// </summary>
    /// <param name="attribute">The attribute to inspect.</param>
    /// <param name="arrayPropertyName">The primary array-valued property name.</param>
    /// <param name="singleValueFallbackPropertyName">The optional single-value property name used by many integrations.</param>
    /// <returns>The normalized string values.</returns>
    private static IReadOnlyList<string> GetStringArray(CustomAttribute attribute, string arrayPropertyName, string? singleValueFallbackPropertyName = null)
    {
        foreach (var property in attribute.Properties)
        {
            if (string.Equals(property.Name, arrayPropertyName, StringComparison.Ordinal))
            {
                if (property.Argument.Value is CustomAttributeArgument[] values)
                {
                    return values
                          .Select(static value => value.Value as string)
                          .Where(static value => !string.IsNullOrWhiteSpace(value))
                          .Cast<string>()
                          .ToList();
                }

                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(singleValueFallbackPropertyName))
        {
            var singleValue = GetString(attribute, singleValueFallbackPropertyName!);
            if (!string.IsNullOrWhiteSpace(singleValue))
            {
                return [singleValue];
            }
        }

        return [];
    }

    /// <summary>
    /// Reads the serialized calltarget kind from a metadata attribute.
    /// </summary>
    /// <param name="attribute">The attribute to inspect.</param>
    /// <returns>The parsed calltarget kind, or the default kind when the property is omitted.</returns>
    private static Datadog.Trace.ClrProfiler.CallTargetKind GetCallTargetKind(CustomAttribute attribute)
    {
        foreach (var property in attribute.Properties)
        {
            if (string.Equals(property.Name, "CallTargetIntegrationKind", StringComparison.Ordinal) &&
                string.Equals(property.Argument.Type.FullName, CallTargetKindFullName, StringComparison.Ordinal))
            {
                var value = Convert.ToInt32(property.Argument.Value, CultureInfo.InvariantCulture);
                return (Datadog.Trace.ClrProfiler.CallTargetKind)value;
            }
        }

        return Datadog.Trace.ClrProfiler.CallTargetKind.Default;
    }

    /// <summary>
    /// Parses a minimum version string or returns the default inclusive floor.
    /// </summary>
    /// <param name="value">The configured minimum version string.</param>
    /// <returns>The parsed version floor.</returns>
    private static Version ParseMinimumVersion(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? new Version(0, 0, 0, 0) : ParseVersion(value!, wildcardValue: 0);
    }

    /// <summary>
    /// Parses a maximum version string or returns a broad inclusive ceiling.
    /// </summary>
    /// <param name="value">The configured maximum version string.</param>
    /// <returns>The parsed version ceiling.</returns>
    private static Version ParseMaximumVersion(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? new Version(65535, 65535, 65535, 65535) : ParseVersion(value!, wildcardValue: 65535);
    }

    /// <summary>
    /// Parses version strings that may omit the build or revision components.
    /// </summary>
    /// <param name="value">The version string to normalize.</param>
    /// <returns>The normalized version.</returns>
    private static Version ParseVersion(string value, int wildcardValue)
    {
        var segments = value.Split('.');
        var normalizedSegments = new int[4];
        for (var index = 0; index < normalizedSegments.Length; index++)
        {
            if (index < segments.Length)
            {
                normalizedSegments[index] = ParseVersionSegment(segments[index], wildcardValue);
            }
            else
            {
                normalizedSegments[index] = wildcardValue == 0 ? 0 : 65535;
            }
        }

        return new Version(normalizedSegments[0], normalizedSegments[1], normalizedSegments[2], normalizedSegments[3]);
    }

    /// <summary>
    /// Parses a single version segment, allowing wildcard segments used by integration metadata.
    /// </summary>
    /// <param name="value">The serialized segment value.</param>
    /// <param name="wildcardValue">The value to use when the segment contains a wildcard.</param>
    /// <returns>The normalized numeric segment value.</returns>
    private static int ParseVersionSegment(string value, int wildcardValue)
    {
        return string.Equals(value, "*", StringComparison.Ordinal)
                   ? wildcardValue
                   : int.Parse(value, CultureInfo.InvariantCulture);
    }
}
