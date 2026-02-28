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
    internal enum DuckTypeAotMappingMode
    {
        Forward,
        Reverse
    }

    internal enum DuckTypeAotMappingSource
    {
        Attribute,
        MapFile
    }

    internal sealed class DuckTypeAotMapping
    {
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

        public string ProxyTypeName { get; }

        public string ProxyAssemblyName { get; }

        public string TargetTypeName { get; }

        public string TargetAssemblyName { get; }

        public DuckTypeAotMappingMode Mode { get; }

        public DuckTypeAotMappingSource Source { get; }

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

    internal static class DuckTypeAotNameHelpers
    {
        internal static string NormalizeAssemblyName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return string.Empty;
            }

            var commaIndex = assemblyName.IndexOf(',');
            return commaIndex >= 0 ? assemblyName.Substring(0, commaIndex).Trim() : assemblyName.Trim();
        }

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

        internal static bool IsGenericTypeName(string typeName)
        {
            return !string.IsNullOrWhiteSpace(typeName) && typeName.IndexOf('`') >= 0;
        }

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

        internal static bool IsClosedGenericTypeName(string typeName)
        {
            return IsGenericTypeName(typeName) && !IsOpenGenericTypeName(typeName);
        }

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
