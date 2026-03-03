// <copyright file="DuckTypeAotAttributeDiscovery.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;

#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Discovers AOT mapping contracts declared via type-level duck-typing attributes.
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
        /// Defines the duck reverse attribute full name constant.
        /// </summary>
        private const string DuckReverseAttributeFullName = "Datadog.Trace.DuckTyping.DuckReverseAttribute";

        /// <summary>
        /// Defines the duck attribute namespace prefix constant.
        /// </summary>
        private const string DuckAttributeNamespacePrefix = "Datadog.Trace.DuckTyping.";

        /// <summary>
        /// Defines the duck attribute name prefix constant.
        /// </summary>
        private const string DuckAttributeNamePrefix = "Duck";

        /// <summary>
        /// Defines the attribute suffix constant.
        /// </summary>
        private const string AttributeSuffix = "Attribute";

        /// <summary>
        /// Discovers mappings from the provided proxy assemblies.
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
                        if (type.IsGlobalModuleType)
                        {
                            continue;
                        }

                        var hasTypeLevelMappingAttribute = false;
                        foreach (var attribute in type.CustomAttributes)
                        {
                            var attributeFullName = attribute.AttributeType?.FullName;
                            if (!TryResolveMappingMode(attributeFullName, out var mappingMode))
                            {
                                continue;
                            }

                            hasTypeLevelMappingAttribute = true;
                            if (!TryReadTargetData(attribute, out var targetTypeName, out var targetAssemblyName))
                            {
                                warnings.Add(
                                    $"Skipping type-level mapping attribute '{attributeFullName}' in '{proxyAssemblyPath}' for proxy type '{type.ReflectionFullName}' because target type/assembly values are missing or invalid.");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(type.ReflectionFullName))
                            {
                                warnings.Add($"Skipping type-level mapping attribute '{attributeFullName}' in '{proxyAssemblyPath}' because the proxy type full name is empty.");
                                continue;
                            }

                            var mapping = new DuckTypeAotMapping(
                                type.ReflectionFullName,
                                proxyAssemblyName,
                                targetTypeName,
                                targetAssemblyName,
                                mappingMode,
                                DuckTypeAotMappingSource.Attribute);

                            mappings[mapping.Key] = mapping;
                        }

                        if (!hasTypeLevelMappingAttribute && HasDuckMemberAttributeUsage(type))
                        {
                            warnings.Add(
                                $"Type '{type.ReflectionFullName}' in '{proxyAssemblyPath}' uses duck member attributes but does not declare a type-level mapping attribute. " +
                                "Add [DuckType], [DuckCopy], or [DuckReverse] metadata.");
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
        /// Attempts to resolve a mapping mode from an attribute full name.
        /// </summary>
        /// <param name="attributeFullName">The attribute full name value.</param>
        /// <param name="mode">The resolved mode value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryResolveMappingMode(string? attributeFullName, out DuckTypeAotMappingMode mode)
        {
            if (string.Equals(attributeFullName, DuckTypeAttributeFullName, StringComparison.Ordinal) ||
                string.Equals(attributeFullName, DuckCopyAttributeFullName, StringComparison.Ordinal))
            {
                mode = DuckTypeAotMappingMode.Forward;
                return true;
            }

            if (string.Equals(attributeFullName, DuckReverseAttributeFullName, StringComparison.Ordinal))
            {
                mode = DuckTypeAotMappingMode.Reverse;
                return true;
            }

            mode = DuckTypeAotMappingMode.Forward;
            return false;
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

        /// <summary>
        /// Determines whether the type uses duck-typing member-level attributes.
        /// </summary>
        /// <param name="type">The type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool HasDuckMemberAttributeUsage(TypeDef type)
        {
            if (ContainsDuckMemberAttribute(type.Fields.SelectMany(field => field.CustomAttributes)))
            {
                return true;
            }

            foreach (var method in type.Methods)
            {
                if (ContainsDuckMemberAttribute(method.CustomAttributes))
                {
                    return true;
                }
            }

            if (ContainsDuckMemberAttribute(type.Properties.SelectMany(property => property.CustomAttributes)))
            {
                return true;
            }

            return ContainsDuckMemberAttribute(type.Events.SelectMany(eventDef => eventDef.CustomAttributes));
        }

        /// <summary>
        /// Determines whether the provided custom attributes include member-level duck attributes.
        /// </summary>
        /// <param name="attributes">The attributes value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool ContainsDuckMemberAttribute(IEnumerable<CustomAttribute> attributes)
        {
            foreach (var attribute in attributes)
            {
                var fullName = attribute.AttributeType?.FullName ?? string.Empty;
                if (fullName.Length == 0)
                {
                    continue;
                }

                if (!fullName.StartsWith(DuckAttributeNamespacePrefix, StringComparison.Ordinal) ||
                    !fullName.EndsWith(AttributeSuffix, StringComparison.Ordinal) ||
                    fullName.IndexOf(DuckAttributeNamePrefix, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                if (string.Equals(fullName, DuckTypeAttributeFullName, StringComparison.Ordinal) ||
                    string.Equals(fullName, DuckCopyAttributeFullName, StringComparison.Ordinal) ||
                    string.Equals(fullName, DuckReverseAttributeFullName, StringComparison.Ordinal))
                {
                    continue;
                }

                return true;
            }

            return false;
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
