// <copyright file="DuckTypeAotAttributeDiscovery.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using dnlib.DotNet;

#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal static class DuckTypeAotAttributeDiscovery
    {
        private const string DuckTypeAttributeFullName = "Datadog.Trace.DuckTyping.DuckTypeAttribute";
        private const string DuckCopyAttributeFullName = "Datadog.Trace.DuckTyping.DuckCopyAttribute";

        internal static DuckTypeAotAttributeDiscoveryResult Discover(IReadOnlyList<string> proxyAssemblyPaths)
        {
            var mappings = new Dictionary<string, DuckTypeAotMapping>(StringComparer.Ordinal);
            var errors = new List<string>();
            var warnings = new List<string>();

            foreach (var proxyAssemblyPath in proxyAssemblyPaths)
            {
                try
                {
                    using var module = ModuleDefMD.Load(proxyAssemblyPath);
                    var proxyAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(module.Assembly?.Name.String ?? Path.GetFileNameWithoutExtension(proxyAssemblyPath) ?? string.Empty);

                    foreach (var type in module.GetTypes())
                    {
                        if (type.IsGlobalModuleType)
                        {
                            continue;
                        }

                        foreach (var attribute in type.CustomAttributes)
                        {
                            var attributeFullName = attribute.AttributeType?.FullName;
                            if (!string.Equals(attributeFullName, DuckTypeAttributeFullName, StringComparison.Ordinal) &&
                                !string.Equals(attributeFullName, DuckCopyAttributeFullName, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            if (!TryReadTargetData(attribute, out var targetTypeName, out var targetAssemblyName))
                            {
                                warnings.Add($"Skipping attribute mapping in '{proxyAssemblyPath}' for proxy type '{type.ReflectionFullName}' because target type/assembly values are missing.");
                                continue;
                            }

                            var mapping = new DuckTypeAotMapping(
                                type.ReflectionFullName,
                                proxyAssemblyName,
                                targetTypeName,
                                targetAssemblyName,
                                DuckTypeAotMappingMode.Forward,
                                DuckTypeAotMappingSource.Attribute);

                            mappings[mapping.Key] = mapping;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Unable to read proxy assembly '{proxyAssemblyPath}': {ex.Message}");
                }
            }

            return new DuckTypeAotAttributeDiscoveryResult(mappings.Values, warnings, errors);
        }

        private static bool TryReadTargetData(CustomAttribute attribute, out string targetTypeName, out string targetAssemblyName)
        {
            targetTypeName = string.Empty;
            targetAssemblyName = string.Empty;

            var constructorTargetType = TryReadStringValue(attribute, constructorArgumentIndex: 0);
            var constructorTargetAssembly = TryReadStringValue(attribute, constructorArgumentIndex: 1);

            var namedTargetType = TryReadNamedStringValue(attribute, "TargetType");
            var namedTargetAssembly = TryReadNamedStringValue(attribute, "TargetAssembly");

            var (targetType, typeAssemblyFromQualifiedName) = DuckTypeAotNameHelpers.ParseTypeAndAssembly(namedTargetType ?? constructorTargetType ?? string.Empty);
            var targetAssembly = DuckTypeAotNameHelpers.NormalizeAssemblyName(namedTargetAssembly ?? constructorTargetAssembly ?? typeAssemblyFromQualifiedName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(targetType) || string.IsNullOrWhiteSpace(targetAssembly))
            {
                return false;
            }

            targetTypeName = targetType;
            targetAssemblyName = targetAssembly;
            return true;
        }

        private static string? TryReadStringValue(CustomAttribute attribute, int constructorArgumentIndex)
        {
            if (attribute.ConstructorArguments.Count <= constructorArgumentIndex)
            {
                return null;
            }

            return ToStringValue(attribute.ConstructorArguments[constructorArgumentIndex].Value);
        }

        private static string? TryReadNamedStringValue(CustomAttribute attribute, string propertyName)
        {
            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (!string.Equals(namedArgument.Name, propertyName, StringComparison.Ordinal))
                {
                    continue;
                }

                return ToStringValue(namedArgument.Argument.Value);
            }

            return null;
        }

        private static string? ToStringValue(object? value)
        {
            return value switch
            {
                UTF8String utf8 => utf8.String,
                string str => str,
                null => null,
                _ => value.ToString()
            };
        }
    }

    internal sealed class DuckTypeAotAttributeDiscoveryResult
    {
        public DuckTypeAotAttributeDiscoveryResult(IEnumerable<DuckTypeAotMapping> mappings, IReadOnlyList<string> warnings, IReadOnlyList<string> errors)
        {
            Mappings = new List<DuckTypeAotMapping>(mappings);
            Warnings = warnings;
            Errors = errors;
        }

        public IReadOnlyList<DuckTypeAotMapping> Mappings { get; }

        public IReadOnlyList<string> Warnings { get; }

        public IReadOnlyList<string> Errors { get; }
    }
}
