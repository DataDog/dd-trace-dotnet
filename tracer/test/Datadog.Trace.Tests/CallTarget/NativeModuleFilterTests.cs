// <copyright file="NativeModuleFilterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget
{
    public class NativeModuleFilterTests
    {
        private static readonly string NativeSourceDirectory = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "src", "Datadog.Tracer.Native");

        private static readonly Regex CallTargetDefinition = new(@"\{\(WCHAR\*\)WStr\(""(?<assembly>[^""]+)""\),\(WCHAR\*\)WStr\(""(?<type>[^""]+)""\),\(WCHAR\*\)WStr\(""[^""]+""\),sig\d+,[^}]*CallTargetKind::(?<kind>\w+)");

        [Fact]
        public void IntegrationTargetAssembliesAreNotSkipped()
        {
            var isSkipped = GetSkippedModuleFilter();
            var targetAssemblies = GetCallTargetDefinitions()
                                  .Select(m => m.Groups["assembly"].Value)
                                  .Distinct()
                                  .ToList();

            targetAssemblies.Should().NotBeEmpty();

            // netstandard defines no types (it only forwards them), so skipping it never hides a target. Derived definitions on it match types in the modules that reference it.
            var skippedTargets = targetAssemblies
                                .Where(name => name != "netstandard")
                                .Where(isSkipped)
                                .ToList();

            skippedTargets.Should().BeEmpty("the native tracer never instruments modules skipped by dd_profiler_constants.h; add these assemblies to include_assemblies");
        }

        [Fact]
        public void SkippedAssembliesAreExcludedByIast()
        {
            var constants = File.ReadAllText(Path.Combine(NativeSourceDirectory, "dd_profiler_constants.h"));
            var iastExcludeFilters = GetStringArray(File.ReadAllText(Path.Combine(NativeSourceDirectory, "iast", "dataflow.cpp")), "_assemblyExcludeFilters");
            var iastExcludePrefixes = iastExcludeFilters
                                     .Where(filter => filter.EndsWith("*"))
                                     .Select(filter => filter.Substring(0, filter.Length - 1))
                                     .ToArray();

            // Known gaps: IAST doesn't exclude these skipped assemblies.
            string[] knownExceptions = ["Anonymously Hosted DynamicMethods Assembly", "ISymWrapper"];

            var notExcluded = GetStringArray(constants, "skip_assemblies")
                             .Where(name => !knownExceptions.Contains(name) && !iastExcludeFilters.Contains(name) && !MatchesIastPrefix(name))
                             .Concat(GetStringArray(constants, "skip_assembly_prefixes").Where(prefix => !MatchesIastPrefix(prefix)))
                             .ToList();

            notExcluded.Should().BeEmpty("IAST can rewrite modules the native tracer skips, but skipped modules don't get the Datadog.Trace reference from GetAssemblyReferences; exclude them in iast/dataflow.cpp or don't skip them");

            bool MatchesIastPrefix(string name) => iastExcludePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
        }

        [Fact]
        public void SkippedFrameworkModulesDontDeriveFromOrImplementTargets()
        {
            var isSkipped = GetSkippedModuleFilter();
            var targetTypes = new HashSet<string>(
                GetCallTargetDefinitions()
                   .Where(m => m.Groups["kind"].Value != "Default")
                   .Select(m => $"{m.Groups["assembly"].Value}|{m.Groups["type"].Value}"));
            var targetAssemblies = new HashSet<string>(targetTypes.Select(key => key.Substring(0, key.IndexOf('|'))));

            GetTypesMatchingTargets(typeof(SqlCommand).Assembly.Location, targetTypes, targetAssemblies)
               .Should().Contain(match => match.Contains(typeof(SqlCommand).FullName));

            var skippedModules = GetFrameworkDirectories()
                                .SelectMany(directory => Directory.GetFiles(directory, "*.dll"))
                                .Where(file => isSkipped(Path.GetFileNameWithoutExtension(file)))
                                .ToList();

            skippedModules.Should().NotBeEmpty();

            var matches = skippedModules.SelectMany(file => GetTypesMatchingTargets(file, targetTypes, targetAssemblies));

            string.Join(Environment.NewLine, matches).Should().BeEmpty("the native tracer never instruments types in modules skipped by dd_profiler_constants.h; don't skip these modules");
        }

        private static IEnumerable<Match> GetCallTargetDefinitions()
            => CallTargetDefinition.Matches(File.ReadAllText(Path.Combine(NativeSourceDirectory, "Generated", "generated_calltargets.g.cpp"))).Cast<Match>();

        private static Func<string, bool> GetSkippedModuleFilter()
        {
            var constants = File.ReadAllText(Path.Combine(NativeSourceDirectory, "dd_profiler_constants.h"));
            var skipPrefixes = GetStringArray(constants, "skip_assembly_prefixes");
            var includeAssemblies = GetStringArray(constants, "include_assemblies");
            var skipAssemblies = GetStringArray(constants, "skip_assemblies");

            return name => skipAssemblies.Contains(name) ||
                           (skipPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) && !includeAssemblies.Contains(name));
        }

        private static IEnumerable<string> GetFrameworkDirectories()
        {
            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
#if NETFRAMEWORK
            return new[] { runtimeDirectory, Path.Combine(runtimeDirectory, "WPF") }.Where(Directory.Exists);
#else
            // Shared frameworks are installed as <dotnet>/shared/<framework>/<version>.
            var versionPrefix = string.Join(".", Path.GetFileName(runtimeDirectory).Split('.').Take(2)) + ".";
            return Directory.GetDirectories(Path.GetDirectoryName(Path.GetDirectoryName(runtimeDirectory)))
                            .SelectMany(framework => Directory.GetDirectories(framework, versionPrefix + "*"));
#endif
        }

        // NativeModuleFilterValidator in tracer/build/_build applies the same rule to the assemblies in NuGet packages.
        private static List<string> GetTypesMatchingTargets(string path, HashSet<string> targetTypes, HashSet<string> targetAssemblies)
        {
            ModuleDefMD module;
            try
            {
                module = ModuleDefMD.Load(path);
            }
            catch (BadImageFormatException)
            {
                // Native binaries ship next to the managed assemblies.
                return [];
            }

            using (module)
            {
                // The ReJIT preprocessor only matches base types and interfaces in the target assembly itself or in modules that reference it.
                var moduleName = module.Assembly?.Name.String;
                if (moduleName is null || (!targetAssemblies.Contains(moduleName) && !module.GetAssemblyRefs().Any(reference => targetAssemblies.Contains(reference.Name.String))))
                {
                    return [];
                }

                return module.GetTypes()
                             .SelectMany(type => type.Interfaces
                                                     .Select(implementation => implementation.Interface)
                                                     .Concat([type.BaseType])
                                                     .Select(parent => GetTypeKey(parent, moduleName))
                                                     .Where(key => key is not null && targetTypes.Contains(key))
                                                     .Select(key => $"{moduleName}: {type.FullName} -> {key}"))
                             .ToList();
            }
        }

        private static string GetTypeKey(ITypeDefOrRef type, string moduleName)
            => type switch
            {
                TypeRef { ResolutionScope: AssemblyRef assembly } typeRef => $"{assembly.Name}|{typeRef.FullName}",
                TypeDef typeDef => $"{moduleName}|{typeDef.FullName}",
                TypeSpec { TypeSig: GenericInstSig genericInstance } => GetTypeKey(genericInstance.GenericType.TypeDefOrRef, moduleName),
                _ => null
            };

        private static string[] GetStringArray(string source, string name)
        {
            var array = Regex.Match(source, $@"\b{name}(\[\])?\s*=?\s*\{{(?<body>[^}}]*)\}}");
            array.Success.Should().BeTrue($"{name} should be defined");

            return Regex.Matches(array.Groups["body"].Value, @"WStr\(""(?<value>[^""]*)""\)")
                        .Cast<Match>()
                        .Select(m => m.Groups["value"].Value)
                        .ToArray();
        }
    }
}
