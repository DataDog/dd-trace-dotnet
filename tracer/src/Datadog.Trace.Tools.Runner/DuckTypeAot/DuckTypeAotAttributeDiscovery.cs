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
    /// <summary>
    /// Provides helper operations for duck type aot attribute discovery.
    /// </summary>
    internal static class DuckTypeAotAttributeDiscovery
    {
        /// <summary>
        /// Defines the duck type attribute full name constant.
        /// </summary>
        private const string DuckTypeAttributeFullName = "Datadog.Trace.DuckTyping.DuckTypeAttribute";

        /// <summary>
        /// Defines the duck copy attribute full name constant.
        /// </summary>
        private const string DuckCopyAttributeFullName = "Datadog.Trace.DuckTyping.DuckCopyAttribute";

        /// <summary>
        /// Executes discover.
        /// </summary>
        /// <param name="proxyAssemblyPaths">The proxy assembly paths value.</param>
        /// <returns>The result produced by this operation.</returns>
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
                        // Branch: take this path when (type.IsGlobalModuleType) evaluates to true.
                        if (type.IsGlobalModuleType)
                        {
                            continue;
                        }

                        foreach (var attribute in type.CustomAttributes)
                        {
                            var attributeFullName = attribute.AttributeType?.FullName;
                            // Branch: take this path when (!string.Equals(attributeFullName, DuckTypeAttributeFullName, StringComparison.Ordinal) && evaluates to true.
                            if (!string.Equals(attributeFullName, DuckTypeAttributeFullName, StringComparison.Ordinal) &&
                                !string.Equals(attributeFullName, DuckCopyAttributeFullName, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            // Branch: take this path when (!TryReadTargetData(attribute, out var targetTypeName, out var targetAssemblyName)) evaluates to true.
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
                    // Branch: handles exceptions that match Exception ex.
                    errors.Add($"Unable to read proxy assembly '{proxyAssemblyPath}': {ex.Message}");
                }
            }

            return new DuckTypeAotAttributeDiscoveryResult(mappings.Values, warnings, errors);
        }

        /// <summary>
        /// Attempts to try read target data.
        /// </summary>
        /// <param name="attribute">The attribute value.</param>
        /// <param name="targetTypeName">The target type name value.</param>
        /// <param name="targetAssemblyName">The target assembly name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
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

            // Branch: take this path when (string.IsNullOrWhiteSpace(targetType) || string.IsNullOrWhiteSpace(targetAssembly)) evaluates to true.
            if (string.IsNullOrWhiteSpace(targetType) || string.IsNullOrWhiteSpace(targetAssembly))
            {
                return false;
            }

            targetTypeName = targetType;
            targetAssemblyName = targetAssembly;
            return true;
        }

        /// <summary>
        /// Attempts to try read string value.
        /// </summary>
        /// <param name="attribute">The attribute value.</param>
        /// <param name="constructorArgumentIndex">The constructor argument index value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static string? TryReadStringValue(CustomAttribute attribute, int constructorArgumentIndex)
        {
            // Branch: take this path when (attribute.ConstructorArguments.Count <= constructorArgumentIndex) evaluates to true.
            if (attribute.ConstructorArguments.Count <= constructorArgumentIndex)
            {
                return null;
            }

            return ToStringValue(attribute.ConstructorArguments[constructorArgumentIndex].Value);
        }

        /// <summary>
        /// Attempts to try read named string value.
        /// </summary>
        /// <param name="attribute">The attribute value.</param>
        /// <param name="propertyName">The property name value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static string? TryReadNamedStringValue(CustomAttribute attribute, string propertyName)
        {
            foreach (var namedArgument in attribute.NamedArguments)
            {
                // Branch: take this path when (!string.Equals(namedArgument.Name, propertyName, StringComparison.Ordinal)) evaluates to true.
                if (!string.Equals(namedArgument.Name, propertyName, StringComparison.Ordinal))
                {
                    continue;
                }

                return ToStringValue(namedArgument.Argument.Value);
            }

            return null;
        }

        /// <summary>
        /// Executes to string value.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <returns>The result produced by this operation.</returns>
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

    /// <summary>
    /// Represents duck type aot attribute discovery result.
    /// </summary>
    internal sealed class DuckTypeAotAttributeDiscoveryResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotAttributeDiscoveryResult"/> class.
        /// </summary>
        /// <param name="mappings">The mappings value.</param>
        /// <param name="warnings">The warnings value.</param>
        /// <param name="errors">The errors value.</param>
        public DuckTypeAotAttributeDiscoveryResult(IEnumerable<DuckTypeAotMapping> mappings, IReadOnlyList<string> warnings, IReadOnlyList<string> errors)
        {
            Mappings = new List<DuckTypeAotMapping>(mappings);
            Warnings = warnings;
            Errors = errors;
        }

        /// <summary>
        /// Gets mappings.
        /// </summary>
        /// <value>The mappings value.</value>
        public IReadOnlyList<DuckTypeAotMapping> Mappings { get; }

        /// <summary>
        /// Gets warnings.
        /// </summary>
        /// <value>The warnings value.</value>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Gets errors.
        /// </summary>
        /// <value>The errors value.</value>
        public IReadOnlyList<string> Errors { get; }
    }
}
