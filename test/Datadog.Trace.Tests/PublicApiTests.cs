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
            ExcludeAttributes = new[] { "System.Runtime.CompilerServices.InternalsVisibleToAttribute" },
        };

        [Fact]
        public void PublicApiHasNotChanged()
        {
            var assembly = typeof(Tracer).Assembly;
            var publicApi = assembly.GeneratePublicApi(ApiGeneratorOptions);

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

        private static string GetExpected(string targetFramework, string publicApi, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null)
        {
            // poor-man's VerifyTests.Verify, because Verify has incompatible dependencies with ASP.NET Core
            var snapshotDirectory = Path.Combine(Directory.GetParent(filePath).FullName, "Snapshots");
            var receivedPath = Path.Combine(snapshotDirectory, $"PublicApiTests.{methodName}_{targetFramework}.received.txt");
            var verifiedPath = Path.Combine(snapshotDirectory, $"PublicApiTests.{methodName}_{targetFramework}.verified.txt");

            File.WriteAllText(receivedPath, publicApi);
            return File.Exists(verifiedPath) ? File.ReadAllText(verifiedPath) : string.Empty;
        }
    }
}
#endif
