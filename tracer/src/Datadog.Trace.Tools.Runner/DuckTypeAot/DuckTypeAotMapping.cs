// <copyright file="DuckTypeAotMapping.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1204 // Static elements should appear before instance elements

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Defines named constants for duck type aot mapping mode.
    /// </summary>
    internal enum DuckTypeAotMappingMode
    {
        /// <summary>
        /// Represents forward.
        /// </summary>
        Forward,

        /// <summary>
        /// Represents reverse.
        /// </summary>
        Reverse
    }

    /// <summary>
    /// Defines named constants for duck type aot mapping source.
    /// </summary>
    internal enum DuckTypeAotMappingSource
    {
        /// <summary>
        /// Represents attribute.
        /// </summary>
        Attribute,

        /// <summary>
        /// Represents map file.
        /// </summary>
        MapFile
    }

    /// <summary>
    /// Represents duck type aot mapping.
    /// </summary>
    internal sealed class DuckTypeAotMapping
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotMapping"/> class.
        /// </summary>
        /// <param name="proxyTypeName">The proxy type name value.</param>
        /// <param name="proxyAssemblyName">The proxy assembly name value.</param>
        /// <param name="targetTypeName">The target type name value.</param>
        /// <param name="targetAssemblyName">The target assembly name value.</param>
        /// <param name="mode">The mode value.</param>
        /// <param name="source">The source value.</param>
        /// <param name="scenarioId">The scenario id value.</param>
        public DuckTypeAotMapping(
            string proxyTypeName,
            string proxyAssemblyName,
            string targetTypeName,
            string targetAssemblyName,
            DuckTypeAotMappingMode mode,
            DuckTypeAotMappingSource source,
            string? scenarioId = null)
        {
            ProxyTypeName = proxyTypeName;
            ProxyAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(proxyAssemblyName);
            TargetTypeName = targetTypeName;
            TargetAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(targetAssemblyName);
            Mode = mode;
            Source = source;
            ScenarioId = NormalizeScenarioId(scenarioId);
        }

        /// <summary>
        /// Gets proxy type name.
        /// </summary>
        /// <value>The proxy type name value.</value>
        public string ProxyTypeName { get; }

        /// <summary>
        /// Gets proxy assembly name.
        /// </summary>
        /// <value>The proxy assembly name value.</value>
        public string ProxyAssemblyName { get; }

        /// <summary>
        /// Gets target type name.
        /// </summary>
        /// <value>The target type name value.</value>
        public string TargetTypeName { get; }

        /// <summary>
        /// Gets target assembly name.
        /// </summary>
        /// <value>The target assembly name value.</value>
        public string TargetAssemblyName { get; }

        /// <summary>
        /// Gets mode.
        /// </summary>
        /// <value>The mode value.</value>
        public DuckTypeAotMappingMode Mode { get; }

        /// <summary>
        /// Gets source.
        /// </summary>
        /// <value>The source value.</value>
        public DuckTypeAotMappingSource Source { get; }

        /// <summary>
        /// Gets scenario id.
        /// </summary>
        /// <value>The scenario id value.</value>
        public string? ScenarioId { get; }

        public string Key =>
            string.Concat(
                Mode.ToString(),
                "|",
                ProxyAssemblyName.ToUpperInvariant(),
                "|",
                ProxyTypeName,
                "|",
                TargetAssemblyName.ToUpperInvariant(),
                "|",
                TargetTypeName);

        /// <summary>
        /// Executes with scenario id.
        /// </summary>
        /// <param name="scenarioId">The scenario id value.</param>
        /// <returns>The result produced by this operation.</returns>
        public DuckTypeAotMapping WithScenarioId(string scenarioId)
        {
            return new DuckTypeAotMapping(
                ProxyTypeName,
                ProxyAssemblyName,
                TargetTypeName,
                TargetAssemblyName,
                Mode,
                Source,
                scenarioId);
        }

        /// <summary>
        /// Normalizes normalize scenario id.
        /// </summary>
        /// <param name="scenarioId">The scenario id value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static string? NormalizeScenarioId(string? scenarioId)
        {
            var trimmedScenarioId = scenarioId?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedScenarioId))
            {
                return null;
            }

            return trimmedScenarioId;
        }
    }

    /// <summary>
    /// Provides helper operations for duck type aot name helpers.
    /// </summary>
    internal static class DuckTypeAotNameHelpers
    {
        /// <summary>
        /// Normalizes normalize assembly name.
        /// </summary>
        /// <param name="assemblyName">The assembly name value.</param>
        /// <returns>The resulting string value.</returns>
        internal static string NormalizeAssemblyName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return string.Empty;
            }

            var commaIndex = assemblyName.IndexOf(',');
            return commaIndex >= 0 ? assemblyName.Substring(0, commaIndex).Trim() : assemblyName.Trim();
        }

        /// <summary>
        /// Parses a potentially assembly-qualified type name into type and assembly components.
        /// </summary>
        /// <param name="value">The raw type reference value.</param>
        /// <returns>The parsed type name and optional assembly name.</returns>
        internal static (string TypeName, string? AssemblyName) ParseTypeAndAssembly(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (string.Empty, null);
            }

            var commaIndex = FindTopLevelComma(value);
            if (commaIndex < 0)
            {
                return (value.Trim(), null);
            }

            return (value.Substring(0, commaIndex).Trim(), NormalizeAssemblyName(value.Substring(commaIndex + 1)));
        }

        /// <summary>
        /// Determines whether is generic type name.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        internal static bool IsGenericTypeName(string typeName)
        {
            return !string.IsNullOrWhiteSpace(typeName) && typeName.IndexOf('`') >= 0;
        }

        /// <summary>
        /// Determines whether is open generic type name.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        internal static bool IsOpenGenericTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            if (typeName.IndexOf('!') >= 0)
            {
                return true;
            }

            if (typeName.IndexOf('`') < 0)
            {
                return false;
            }

            var genericArgumentsStart = typeName.IndexOf("[[", StringComparison.Ordinal);
            if (genericArgumentsStart < 0)
            {
                return true;
            }

            var declaredArity = CountDeclaredGenericArity(typeName, genericArgumentsStart);
            if (declaredArity <= 0)
            {
                return false;
            }

            var providedArguments = CountTopLevelGenericArguments(typeName, genericArgumentsStart);
            return providedArguments < declaredArity;
        }

        /// <summary>
        /// Determines whether is closed generic type name.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        internal static bool IsClosedGenericTypeName(string typeName)
        {
            return IsGenericTypeName(typeName) && !IsOpenGenericTypeName(typeName);
        }

        /// <summary>
        /// Executes find top level comma.
        /// </summary>
        /// <param name="value">The value value.</param>
        /// <returns>The computed numeric value.</returns>
        private static int FindTopLevelComma(string value)
        {
            var bracketDepth = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '[')
                {
                    bracketDepth++;
                }
                else if (c == ']')
                {
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                }
                else if (c == ',' && bracketDepth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Executes count declared generic arity.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <param name="genericArgumentsStart">The generic arguments start value.</param>
        /// <returns>The computed numeric value.</returns>
        private static int CountDeclaredGenericArity(string typeName, int genericArgumentsStart)
        {
            var arity = 0;
            for (var i = 0; i < genericArgumentsStart; i++)
            {
                if (typeName[i] != '`')
                {
                    continue;
                }

                var digitsStart = i + 1;
                if (digitsStart >= genericArgumentsStart || !char.IsDigit(typeName[digitsStart]))
                {
                    continue;
                }

                var digitsEnd = digitsStart;
                while (digitsEnd < genericArgumentsStart && char.IsDigit(typeName[digitsEnd]))
                {
                    digitsEnd++;
                }

                if (int.TryParse(typeName.Substring(digitsStart, digitsEnd - digitsStart), out var parsedArity))
                {
                    arity += parsedArity;
                }

                i = digitsEnd - 1;
            }

            return arity;
        }

        /// <summary>
        /// Executes count top level generic arguments.
        /// </summary>
        /// <param name="typeName">The type name value.</param>
        /// <param name="genericArgumentsStart">The generic arguments start value.</param>
        /// <returns>The computed numeric value.</returns>
        private static int CountTopLevelGenericArguments(string typeName, int genericArgumentsStart)
        {
            var bracketDepth = 0;
            var argumentCount = 0;
            var hasStartedRootArgumentList = false;

            for (var i = genericArgumentsStart; i < typeName.Length; i++)
            {
                var current = typeName[i];
                if (current == '[')
                {
                    bracketDepth++;
                    if (bracketDepth == 2 && !hasStartedRootArgumentList)
                    {
                        hasStartedRootArgumentList = true;
                        argumentCount = 1;
                    }

                    continue;
                }

                if (current == ']')
                {
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    if (hasStartedRootArgumentList && bracketDepth == 0)
                    {
                        break;
                    }

                    continue;
                }

                if (current == ',' && hasStartedRootArgumentList && bracketDepth == 1)
                {
                    argumentCount++;
                }
            }

            return argumentCount;
        }
    }
}
