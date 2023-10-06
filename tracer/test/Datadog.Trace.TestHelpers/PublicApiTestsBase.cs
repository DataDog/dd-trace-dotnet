// <copyright file="PublicApiTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text;
using Datadog.Trace.Ci.Coverage.Attributes;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions;
using FluentAssertions;
using PublicApiGenerator;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests
{
    public abstract class PublicApiTestsBase
    {
        private readonly Assembly _assembly;
        private readonly ITestOutputHelper _output;
        private readonly string _snapshotDirectory;

        public PublicApiTestsBase(Assembly assembly, ITestOutputHelper output)
        {
            _assembly = assembly;
            _output = output;
            var projectName = this.GetType().Assembly.GetName().Name!;
            _snapshotDirectory = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "test", projectName, "Snapshots");
        }

        [Fact]
        public void PublicApiHasNotChanged()
        {
            var browsableTypes = _assembly.GetTypes().Where(type => !HasHideInIntellisenseAttributes(type)).ToArray();
            var options = new ApiGeneratorOptions
            {
                ExcludeAttributes = new[]
                {
                    typeof(InternalsVisibleToAttribute).FullName,
                    typeof(AvoidCoverageAttribute).FullName,
                    "System.Runtime.CompilerServices.IsByRefLike",
                },

                // Specify IncludeTypes in options to ensure the results are identical between running this single test
                // and running multiple tests at once
                IncludeTypes = browsableTypes,
            };

            var publicApi = _assembly.GeneratePublicApi(options);

            // we will have a slightly different public API for net4x vs netcore
            var attribute = (TargetFrameworkAttribute)_assembly.GetCustomAttribute(typeof(TargetFrameworkAttribute));

            // remove the differences between the lines
            publicApi = publicApi.Replace(
                $@"[assembly: System.Runtime.Versioning.TargetFramework(""{attribute.FrameworkName}"", FrameworkDisplayName=""{attribute.FrameworkDisplayName}"")]",
                string.Empty);

            var expected = GetExpected(publicApi);

            publicApi.Should().Be(expected, outputDiffOnly: true, "Public API should match the verified API. Update verified snapshot when the public API changes as appropriate");
        }

        [Fact]
        public void AssemblyReferencesHaveNotChanged()
        {
            StringBuilder sb = new();
            foreach (var referencedAssembly in _assembly.GetReferencedAssemblies().OrderBy(asm => asm.FullName))
            {
                // Exclusions
                // Datadog.Trace: This dependency is fine and the version will change over time
                // netstandard: This dependency is fine and there's a discrepancy between local builds and CI builds containing/not containing a reference to it
                if (!referencedAssembly.Name.StartsWith("Datadog.Trace")
                    && !referencedAssembly.Name.Equals("netstandard"))
                {
                    sb.AppendLine(referencedAssembly.FullName);
                }
            }

            // we will have a different list of referenced assemblies for net4x vs netcore vs netstandard
            var referencedAssemblyOutput = sb.ToString();
            string frameworkName = EnvironmentTools.GetTracerTargetFrameworkDirectory();
            var expected = GetExpected(referencedAssemblyOutput, frameworkName);

            referencedAssemblyOutput.Should().Be(expected, outputDiffOnly: true, "Assembly references should match the verified list of assembly references. Update the verified snapshot when the assembly references change");
        }

        [Theory]
        [InlineData(typeof(Hidden), true)]
        [InlineData(typeof(NotHidden1), false)]
        [InlineData(typeof(NotHidden2), false)]
        [InlineData(typeof(NotHidden3), false)]
        [InlineData(typeof(NotHidden4), false)]
        [InlineData(typeof(NotHidden5), false)]
        [InlineData(typeof(NotHidden6), false)]
        public void CalculatesHiddenCorrectly(Type type, bool isHidden)
        {
            var actual = HasHideInIntellisenseAttributes(type);
            actual.Should().Be(isHidden);
        }

        private static bool HasHideInIntellisenseAttributes(Type type)
        {
            var browsable = type.GetCustomAttribute<BrowsableAttribute>();
            if (browsable is null || browsable.Browsable)
            {
                return false;
            }

            var editorBrowsable = type.GetCustomAttribute<EditorBrowsableAttribute>();
            if (editorBrowsable is null || editorBrowsable.State != EditorBrowsableState.Never)
            {
                return false;
            }

            return true;
        }

        private string GetExpected(string publicApi, string targetFramework = null, [CallerMemberName] string methodName = null)
        {
            // poor-man's VerifyTests.Verify, because Verify has incompatible dependencies with ASP.NET Core
            var assemblyName = _assembly.GetName().Name;
            _output?.WriteLine($"Building directory for assembly {assemblyName} and snapshot directory {_snapshotDirectory}");

            var intermediatePath = targetFramework == null ? methodName : $"{methodName}.{targetFramework}";
            var receivedPath = Path.Combine(_snapshotDirectory, $"PublicApiTests.{assemblyName}.{intermediatePath}.received.txt");
            var verifiedPath = Path.Combine(_snapshotDirectory, $"PublicApiTests.{assemblyName}.{intermediatePath}.verified.txt");

            File.WriteAllText(receivedPath, publicApi);
            return File.Exists(verifiedPath) ? File.ReadAllText(verifiedPath) : string.Empty;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public class Hidden
        {
        }

        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public class NotHidden1
        {
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public class NotHidden2
        {
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        public class NotHidden3
        {
        }

        [Browsable(false)]
        public class NotHidden4
        {
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public class NotHidden5
        {
        }

        public class NotHidden6
        {
        }
    }
}
