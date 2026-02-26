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
            DuckTypeAotMappingSource source)
        {
            ProxyTypeName = proxyTypeName;
            ProxyAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(proxyAssemblyName);
            TargetTypeName = targetTypeName;
            TargetAssemblyName = DuckTypeAotNameHelpers.NormalizeAssemblyName(targetAssemblyName);
            Mode = mode;
            Source = source;
        }

        public string ProxyTypeName { get; }

        public string ProxyAssemblyName { get; }

        public string TargetTypeName { get; }

        public string TargetAssemblyName { get; }

        public DuckTypeAotMappingMode Mode { get; }

        public DuckTypeAotMappingSource Source { get; }

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

            var commaIndex = value.IndexOf(',');
            if (commaIndex < 0)
            {
                return (value.Trim(), null);
            }

            return (value.Substring(0, commaIndex).Trim(), NormalizeAssemblyName(value.Substring(commaIndex + 1)));
        }
    }
}
