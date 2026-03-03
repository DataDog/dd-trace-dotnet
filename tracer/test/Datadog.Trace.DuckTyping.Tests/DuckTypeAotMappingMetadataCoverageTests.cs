// <copyright file="DuckTypeAotMappingMetadataCoverageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests;

public class DuckTypeAotMappingMetadataCoverageTests
{
    private const string CanonicalMapRelativePath = "AotCompatibility/ducktype-aot-bible-mappings.json";
    private const string DuckTypeAttributeFullName = "Datadog.Trace.DuckTyping.DuckTypeAttribute";
    private const string DuckCopyAttributeFullName = "Datadog.Trace.DuckTyping.DuckCopyAttribute";
    private const string DuckReverseAttributeFullName = "Datadog.Trace.DuckTyping.DuckReverseAttribute";

    [Fact]
    public void CanonicalMappingsShouldHaveMatchingTypeLevelAttributes()
    {
        var mapPath = ResolveCanonicalMapPath();
        File.Exists(mapPath).Should().BeTrue($"canonical map file should exist at '{mapPath}'");

        var document = JsonConvert.DeserializeObject<MapDocument>(File.ReadAllText(mapPath));
        document.Should().NotBeNull();
        document!.Mappings.Should().NotBeEmpty();

        var assembly = typeof(DuckTypeAotDifferentialParityTests).Assembly;
        var failures = new List<string>();

        foreach (var mapping in document.Mappings)
        {
            var mode = (mapping.Mode ?? "forward").Trim();
            var proxyType = assembly.GetType(mapping.ProxyType ?? string.Empty, throwOnError: false);
            if (proxyType is null)
            {
                failures.Add($"Proxy type '{mapping.ProxyType}' was not found.");
                continue;
            }

            var expectedAttributeName = string.Equals(mode, "reverse", StringComparison.OrdinalIgnoreCase)
                                            ? DuckReverseAttributeFullName
                                            : (proxyType.IsValueType ? DuckCopyAttributeFullName : DuckTypeAttributeFullName);

            var matchingAttribute = proxyType.CustomAttributes.FirstOrDefault(
                attribute =>
                    string.Equals(attribute.AttributeType.FullName, expectedAttributeName, StringComparison.Ordinal) &&
                    TryReadTargetData(attribute, out var targetType, out var targetAssembly) &&
                    string.Equals(targetType, mapping.TargetType, StringComparison.Ordinal) &&
                    string.Equals(targetAssembly, mapping.TargetAssembly, StringComparison.OrdinalIgnoreCase));

            if (matchingAttribute is null)
            {
                failures.Add(
                    $"Type '{proxyType.FullName}' is missing [{TypeNameFromFullName(expectedAttributeName)}(\"{mapping.TargetType}\", \"{mapping.TargetAssembly}\")]");
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    private static bool TryReadTargetData(CustomAttributeData attribute, out string targetType, out string targetAssembly)
    {
        targetType = string.Empty;
        targetAssembly = string.Empty;

        if (attribute.ConstructorArguments.Count >= 2)
        {
            targetType = attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
            targetAssembly = attribute.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
        }

        foreach (var named in attribute.NamedArguments)
        {
            if (string.Equals(named.MemberName, "TargetType", StringComparison.Ordinal))
            {
                targetType = named.TypedValue.Value?.ToString() ?? targetType;
            }
            else if (string.Equals(named.MemberName, "TargetAssembly", StringComparison.Ordinal))
            {
                targetAssembly = named.TypedValue.Value?.ToString() ?? targetAssembly;
            }
        }

        return !string.IsNullOrWhiteSpace(targetType) && !string.IsNullOrWhiteSpace(targetAssembly);
    }

    private static string TypeNameFromFullName(string fullName)
    {
        var index = fullName.LastIndexOf('.');
        return index < 0 ? fullName : fullName.Substring(index + 1);
    }

    private static string ResolveCanonicalMapPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, CanonicalMapRelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, CanonicalMapRelativePath);
    }

    private sealed class MapDocument
    {
        public string? SchemaVersion { get; set; }

        public List<MapEntry> Mappings { get; set; } = new();
    }

    private sealed class MapEntry
    {
        public string? Mode { get; set; }

        public string? ProxyType { get; set; }

        public string? TargetType { get; set; }

        public string? TargetAssembly { get; set; }
    }
}
