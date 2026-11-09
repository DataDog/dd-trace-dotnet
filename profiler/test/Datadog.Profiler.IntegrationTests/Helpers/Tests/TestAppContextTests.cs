// <copyright file="TestAppContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    [Trait("Category", "TestInfrastructure")]
    public class TestAppContextTests
    {
        public static IEnumerable<object[]> KnownMonikers()
        {
            yield return new object[] { "net48", TestFramework.Net48 };
            yield return new object[] { "netcoreapp3.1", TestFramework.NetCoreApp31 };
            yield return new object[] { "net5.0", TestFramework.Net5 };
            yield return new object[] { "net6.0", TestFramework.Net6 };
            yield return new object[] { "net7.0", TestFramework.Net7 };
            yield return new object[] { "net8.0", TestFramework.Net8 };
            yield return new object[] { "net9.0", TestFramework.Net9 };
            yield return new object[] { "net10.0", TestFramework.Net10 };
            yield return new object[] { "net11.0", TestFramework.Net11 };
        }

        [Theory]
        [MemberData(nameof(KnownMonikers))]
        public void ParsesTargetFrameworkMoniker(string moniker, TestFramework expected)
        {
            TestFrameworkInfo.TryParse(moniker, out var parsed).Should().BeTrue();
            parsed.Should().Be(expected);
            TestFrameworkInfo.ToMoniker(parsed).Should().Be(moniker);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("net5")]
        [InlineData("net462")]
        [InlineData("net45")]
        [InlineData("none")]
        public void RejectsUnknownMoniker(string moniker)
        {
            TestFrameworkInfo.TryParse(moniker, out var parsed).Should().BeFalse();
            parsed.Should().Be(TestFramework.None);
        }

        [Fact]
        public void VersionOrderExcludesNet48AndFollowsVersion()
        {
            TestFrameworkInfo.VersionOrder.Select(TestFrameworkInfo.ToMoniker).Should().Equal(
                "netcoreapp3.1",
                "net5.0",
                "net6.0",
                "net7.0",
                "net8.0",
                "net9.0",
                "net10.0",
                "net11.0");

            ((int)TestFramework.None).Should().Be(0);
            ((int)TestFramework.Net48).Should().BeLessThan((int)TestFramework.NetCoreApp31);
            for (var i = 1; i < TestFrameworkInfo.VersionOrder.Length; i++)
            {
                ((int)TestFrameworkInfo.VersionOrder[i]).Should().BeGreaterThan((int)TestFrameworkInfo.VersionOrder[i - 1]);
            }
        }

        [Fact]
        public void MinimumFrameworkSelectsThatVersionAndNewerExceptNet48()
        {
            var selected = Monikers(Plan(
                exact: null,
                minimum: TestFramework.Net6,
                maximum: TestFramework.None,
                excluded: null,
                built: new[] { "net48", "netcoreapp3.1", "net5.0", "net6.0", "net8.0", "net10.0", "net11.0" },
                windows: true,
                arm64: false));

            selected.Should().Equal("net6.0", "net8.0", "net10.0", "net11.0");
        }

        [Fact]
        public void MaximumFrameworkClosesTheRange()
        {
            var selected = Monikers(Plan(
                exact: null,
                minimum: TestFramework.Net6,
                maximum: TestFramework.Net8,
                excluded: null,
                built: new[] { "net6.0", "net7.0", "net8.0", "net9.0" },
                windows: false,
                arm64: false));

            selected.Should().Equal("net6.0", "net7.0", "net8.0");
        }

        [Fact]
        public void ExclusionsRemoveFrameworksFromARange()
        {
            var selected = Monikers(Plan(
                exact: null,
                minimum: TestFramework.Net6,
                maximum: TestFramework.None,
                excluded: new[] { TestFramework.Net7, TestFramework.Net9 },
                built: new[] { "net6.0", "net7.0", "net8.0", "net9.0", "net10.0" },
                windows: false,
                arm64: false));

            selected.Should().Equal("net6.0", "net8.0", "net10.0");
        }

        [Fact]
        public void ExplicitListKeepsNet48AndAppliesExclusions()
        {
            var selected = Monikers(Plan(
                exact: new[] { TestFramework.Net48, TestFramework.Net8, TestFramework.Net10 },
                minimum: TestFramework.None,
                maximum: TestFramework.None,
                excluded: new[] { TestFramework.Net10 },
                built: new[] { "net48", "net8.0", "net10.0" },
                windows: true,
                arm64: false));

            selected.Should().Equal("net48", "net8.0");
        }

        [Fact]
        public void RangeNeverAddsNet48UnlessItIsListed()
        {
            var withoutExplicit = Monikers(Plan(
                exact: null,
                minimum: TestFramework.NetCoreApp31,
                maximum: TestFramework.None,
                excluded: null,
                built: new[] { "net48", "netcoreapp3.1", "net6.0" },
                windows: true,
                arm64: false));
            withoutExplicit.Should().Equal("netcoreapp3.1", "net6.0");

            var withExplicit = Monikers(Plan(
                exact: new[] { TestFramework.Net48, TestFramework.Net6, TestFramework.Net10 },
                minimum: TestFramework.Net8,
                maximum: TestFramework.None,
                excluded: null,
                built: new[] { "net48", "net6.0", "net8.0", "net10.0" },
                windows: true,
                arm64: false));
            withExplicit.Should().Equal("net48", "net8.0", "net10.0");
        }

        [Fact]
        public void DefaultSelectionIsEveryBuiltNonNet48FrameworkForThePlatform()
        {
            var windows = Monikers(Plan(
                exact: null,
                minimum: TestFramework.None,
                maximum: TestFramework.None,
                excluded: null,
                built: new[] { "net48", "netcoreapp3.1", "net6.0", "net8.0" },
                windows: true,
                arm64: false));
            windows.Should().Equal("netcoreapp3.1", "net6.0", "net8.0");

            var arm64 = Monikers(Plan(
                exact: null,
                minimum: TestFramework.None,
                maximum: TestFramework.None,
                excluded: null,
                built: new[] { "net6.0", "net8.0", "net11.0" },
                windows: false,
                arm64: true));
            arm64.Should().Equal("net6.0", "net8.0", "net11.0");
            arm64.Should().NotContain("netcoreapp3.1");
        }

        [Fact]
        public void Net48AsARangeBoundIsAnError()
        {
            var planned = Plan(
                exact: null,
                minimum: TestFramework.Net48,
                maximum: TestFramework.None,
                excluded: null,
                built: new[] { "net48", "net8.0" },
                windows: true,
                arm64: false);

            planned.Should().ContainSingle();
            planned[0].Kind.Should().Be(TestAppCasePlanner.Kind.Error);
            planned[0].Message.Should().Contain("net48");
        }

        [Fact]
        public void KnownFrameworkMissingOnThisPlatformIsSkipped()
        {
            var planned = Plan(
                exact: new[] { TestFramework.Net48, TestFramework.NetCoreApp31, TestFramework.Net8 },
                minimum: TestFramework.None,
                maximum: TestFramework.None,
                excluded: null,
                built: new[] { "net8.0" },
                windows: false,
                arm64: true);

            planned.Select(item => item.Kind + ":" + TestFrameworkInfo.ToMoniker(item.Framework)).Should().Equal(
                "Skip:net48",
                "Skip:netcoreapp3.1",
                "Run:net8.0");
            planned[0].Message.Should().Be("Test case skipped: net48 is not built for this OS/architecture");
            planned[1].Message.Should().Be("Test case skipped: netcoreapp3.1 is not built for this OS/architecture");
        }

        [Fact]
        public void UnknownTfmFolderIsADiscoveryError()
        {
            var planned = Plan(
                exact: null,
                minimum: TestFramework.Net8,
                maximum: TestFramework.None,
                excluded: null,
                built: new[] { "net8.0", "publish" },
                windows: false,
                arm64: false);

            planned.Should().Contain(item => item.Kind == TestAppCasePlanner.Kind.Error && item.Message.Contains("publish"));
            Monikers(planned).Should().Equal("net8.0");
        }

        [Fact]
        public void UnknownApplicationIsADiscoveryError()
        {
            var planned = TestAppCasePlanner.Plan(
                (TestApplication)999,
                exact: null,
                minimum: TestFramework.None,
                maximum: TestFramework.None,
                excluded: null,
                builtFolderNames: new[] { "net8.0" },
                windows: true,
                arm64: false);

            planned.Should().ContainSingle();
            planned[0].Kind.Should().Be(TestAppCasePlanner.Kind.Error);
            planned[0].Message.Should().Contain("Unknown test application");
        }

        [Fact]
        public void RoundTripsThroughXunitSerialization()
        {
            var original = new TestAppContext(TestApplicationCatalog.Get(TestApplication.WebsiteAspNet), TestFramework.Net48);
            var copy = RoundTrip(original);

            copy.Application.Application.Should().Be(TestApplication.WebsiteAspNet);
            copy.Application.FolderName.Should().Be("Website-AspNet");
            copy.Application.AssemblyName.Should().Be("Datadog.Demos.Website-AspNet");
            copy.Framework.Should().Be(TestFramework.Net48);
            copy.ToString().Should().Be("Website-AspNet net48");
            copy.IsNetFramework.Should().BeTrue();
        }

        [Fact]
        public void ToStringUsesFolderNameAndMoniker()
        {
            var context = new TestAppContext(TestApplicationCatalog.Get(TestApplication.WebsiteAspNetCore01), TestFramework.Net6);
            context.ToString().Should().Be("Samples.Website-AspNetCore01 net6.0");
        }

        [Theory]
        [InlineData(TestFramework.Net48, true)]
        [InlineData(TestFramework.Net8, false)]
        [InlineData(TestFramework.NetCoreApp31, false)]
        public void IsNetFrameworkIsOnlyNet48(TestFramework framework, bool expected)
        {
            var context = new TestAppContext(TestApplicationCatalog.Get(TestApplication.Computer01), framework);
            context.IsNetFramework.Should().Be(expected);
        }

        [Theory]
        [InlineData(TestFramework.Net10, TestFramework.Net8, true)]
        [InlineData(TestFramework.Net8, TestFramework.Net8, true)]
        [InlineData(TestFramework.Net6, TestFramework.Net8, false)]
        [InlineData(TestFramework.Net48, TestFramework.Net8, false)]
        [InlineData(TestFramework.Net8, TestFramework.Net48, false)]
        [InlineData(TestFramework.Net48, TestFramework.Net48, true)]
        [InlineData(TestFramework.NetCoreApp31, TestFramework.Net6, false)]
        public void IsAtLeastComparesVersionAndKeepsNet48Separate(TestFramework framework, TestFramework minimum, bool expected)
        {
            var context = new TestAppContext(TestApplicationCatalog.Get(TestApplication.Computer01), framework);
            context.IsAtLeast(minimum).Should().Be(expected);
        }

        [Fact]
        public void CatalogCoversEveryDemoProjectAndItsTargetFrameworks()
        {
            var root = FindRepositoryRoot();
            var demos = Path.Combine(root, "profiler", "src", "Demos");
            var projects = Directory.GetDirectories(demos)
                .Select(path => new { Folder = Path.GetFileName(path), Project = Directory.GetFiles(path, "*.csproj").SingleOrDefault() })
                .Where(item => item.Project != null)
                .ToList();

            projects.Should().NotBeEmpty();
            foreach (var project in projects)
            {
                TestApplicationCatalog.TryGetByFolder(project.Folder, out var descriptor).Should().BeTrue($"folder {project.Folder} is a demo app");
                var assemblyName = Regex.Match(File.ReadAllText(project.Project), @"<AssemblyName>([^<]+)</AssemblyName>");
                assemblyName.Success.Should().BeTrue();
                descriptor.AssemblyName.Should().Be(assemblyName.Groups[1].Value);
            }

            var monikers = new HashSet<string>();
            foreach (var props in Directory.GetFiles(demos, "*.props", SearchOption.AllDirectories)
                .Concat(projects.Select(project => project.Project)))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(props), @"<TargetFrameworks\b[^>]*>([^<]+)</TargetFrameworks>"))
                {
                    foreach (var tfm in match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        monikers.Add(tfm);
                    }
                }
            }

            monikers.Should().BeEquivalentTo(
                "net48",
                "netcoreapp3.1",
                "net5.0",
                "net6.0",
                "net7.0",
                "net8.0",
                "net9.0",
                "net10.0",
                "net11.0");
            foreach (var moniker in monikers)
            {
                TestFrameworkInfo.TryParse(moniker, out _).Should().BeTrue();
            }
        }

        [Fact]
        public void CatalogCoversEveryStringTestAppFact()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var attributes = typeof(TestAppFact).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(flags))
                .SelectMany(method => method.GetCustomAttributes<TestAppFact>(inherit: false))
                .Where(attribute => !attribute.IsTyped)
                .ToList();

            attributes.Should().NotBeEmpty();
            foreach (var attribute in attributes)
            {
                TestApplicationCatalog.TryGetByFolder(attribute.AppName, out var descriptor).Should().BeTrue(attribute.AppName);
                descriptor.AssemblyName.Should().Be(attribute.AppAssembly);
            }
        }

        [Fact]
        public void EveryApplicationEnumValueHasADescriptor()
        {
            foreach (TestApplication application in Enum.GetValues(typeof(TestApplication)))
            {
                TestApplicationCatalog.TryGet(application, out var descriptor).Should().BeTrue();
                descriptor.Application.Should().Be(application);
                descriptor.FolderName.Should().NotBeNullOrWhiteSpace();
                descriptor.AssemblyName.Should().NotBeNullOrWhiteSpace();
            }
        }

        private static TestAppContext RoundTrip(TestAppContext original)
        {
            // SerializationHelper is the serializer XunitTestCase uses for IXunitSerializable arguments.
            // The type is internal to xUnit, so the test calls it through reflection.
            var helper = typeof(XunitTestCase).Assembly.GetType("Xunit.Sdk.SerializationHelper", throwOnError: true);
            var serialize = helper.GetMethod("Serialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var deserialize = helper.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var serialized = (string)serialize.Invoke(null, new object[] { original });
            return (TestAppContext)deserialize.MakeGenericMethod(typeof(TestAppContext)).Invoke(null, new object[] { serialized });
        }

        private static IReadOnlyList<TestAppCasePlanner.Case> Plan(
            TestFramework[] exact,
            TestFramework minimum,
            TestFramework maximum,
            TestFramework[] excluded,
            string[] built,
            bool windows,
            bool arm64)
        {
            return TestAppCasePlanner.Plan(
                TestApplication.Computer01,
                exact,
                minimum,
                maximum,
                excluded,
                built,
                windows,
                arm64);
        }

        private static IEnumerable<string> Monikers(IReadOnlyList<TestAppCasePlanner.Case> planned)
        {
            return planned.Where(item => item.Kind == TestAppCasePlanner.Kind.Run).Select(item => TestFrameworkInfo.ToMoniker(item.Framework));
        }

        private static string FindRepositoryRoot()
        {
            var starts = new[] { AppContext.BaseDirectory, Environment.CurrentDirectory };
            foreach (var start in starts)
            {
                var current = new DirectoryInfo(start);
                while (current != null)
                {
                    var marker = Path.Combine(current.FullName, "profiler", "src", "Demos", "Directory.Build.props");
                    if (File.Exists(marker))
                    {
                        return current.FullName;
                    }

                    current = current.Parent;
                }
            }

            throw new InvalidOperationException("Could not find the repository root from the test output directory.");
        }
    }
}
