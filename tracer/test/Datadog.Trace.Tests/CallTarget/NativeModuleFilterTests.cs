// <copyright file="NativeModuleFilterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget
{
    public class NativeModuleFilterTests
    {
        private static readonly string NativeSourceDirectory = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "src", "Datadog.Tracer.Native");

        [Fact]
        public void IntegrationTargetAssembliesAreNotSkipped()
        {
            var constants = File.ReadAllText(Path.Combine(NativeSourceDirectory, "dd_profiler_constants.h"));
            var skipPrefixes = GetStringArray(constants, "skip_assembly_prefixes");
            var includeAssemblies = GetStringArray(constants, "include_assemblies");
            var skipAssemblies = GetStringArray(constants, "skip_assemblies");

            var definitions = File.ReadAllText(Path.Combine(NativeSourceDirectory, "Generated", "generated_calltargets.g.cpp"));
            var targetAssemblies = Regex.Matches(definitions, @"\{\(WCHAR\*\)WStr\(""(?<assembly>[^""]+)""\),\(WCHAR\*\)WStr\(""[^""]+""\),\(WCHAR\*\)WStr\(""[^""]+""\),sig\d+,[^}]*CallTargetKind::")
                                         .Cast<Match>()
                                         .Select(m => m.Groups["assembly"].Value)
                                         .Distinct()
                                         .ToList();

            targetAssemblies.Should().NotBeEmpty();

            // netstandard defines no types (it only forwards them), so skipping it never hides a target. Derived definitions on it match types in the modules that reference it.
            var skippedTargets = targetAssemblies
                                .Where(name => name != "netstandard")
                                .Where(name => skipAssemblies.Contains(name) ||
                                               (skipPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) && !includeAssemblies.Contains(name)))
                                .ToList();

            skippedTargets.Should().BeEmpty("the native tracer never instruments modules skipped by dd_profiler_constants.h; add these assemblies to include_assemblies");
        }

        private static string[] GetStringArray(string source, string name)
        {
            var array = Regex.Match(source, $@"\b{name}\[\]\s*\{{(?<body>[^}}]*)\}}");
            array.Success.Should().BeTrue($"dd_profiler_constants.h should define {name}");

            return Regex.Matches(array.Groups["body"].Value, @"WStr\(""(?<value>[^""]*)""\)")
                        .Cast<Match>()
                        .Select(m => m.Groups["value"].Value)
                        .ToArray();
        }
    }
}
