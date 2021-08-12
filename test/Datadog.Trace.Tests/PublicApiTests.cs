// <copyright file="PublicApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET452
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using FluentAssertions;
using PublicApiGenerator;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class PublicApiTests
    {
        private static readonly ApiGeneratorOptions ApiGeneratorOptions = new ApiGeneratorOptions
        {
            ExcludeAttributes = new[] { typeof(InternalsVisibleToAttribute).FullName, },
        };

        [Fact]
        public void PublicApiHasNotChanged()
        {
            var assembly = typeof(Tracer).Assembly;
            var browsableTypes = assembly.GetTypes().Where(type => !HasHideInIntellisenseAttributes(type)).ToArray();
            var publicApi = browsableTypes.GeneratePublicApi(ApiGeneratorOptions);

            // we will have a slightly different public API for net4x vs netcore
            var attribute = (TargetFrameworkAttribute)assembly.GetCustomAttribute(typeof(TargetFrameworkAttribute));

            // remove the differences between the lines
            publicApi = publicApi.Replace(
                $@"[assembly: System.Runtime.Versioning.TargetFramework(""{attribute.FrameworkName}"", FrameworkDisplayName=""{attribute.FrameworkDisplayName}"")]",
                string.Empty);

            var targetFramework = attribute.FrameworkName.Contains(".NETFramework") ? "net4" : "netcoreapp";

            var expected = GetExpected(targetFramework, publicApi);

            publicApi.Should().Be(expected, "Public API should match the verified API. Update verified snapshot when the public API changes as appropriate");
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

        private static string GetExpected(string targetFramework, string publicApi, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null)
        {
            // poor-man's VerifyTests.Verify, because Verify has incompatible dependencies with ASP.NET Core
            var snapshotDirectory = Path.Combine(Directory.GetParent(filePath).FullName, "Snapshots");
            var receivedPath = Path.Combine(snapshotDirectory, $"PublicApiTests.{methodName}_{targetFramework}.received.txt");
            var verifiedPath = Path.Combine(snapshotDirectory, $"PublicApiTests.{methodName}_{targetFramework}.verified.txt");

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
#endif
