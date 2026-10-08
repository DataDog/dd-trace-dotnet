// <copyright file="SourceLinkInformationExtractorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.Pdb;
using FluentAssertions;
using Xunit;

#nullable enable

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class SourceLinkInformationExtractorTests
    {
        [Theory]
        [InlineData("1.0.0+abc123", "abc123")] // Valid case - exactly 2 parts
        [InlineData("2.1.0-beta+def456", "def456")] // Valid case with prerelease
        public void TryExtractFromAssemblyAttributes_ValidInformationalVersion_ExtractsCommitSha(string informationalVersion, string expectedCommitSha)
        {
            var assembly = CreateTestAssembly(repositoryUrl: "https://github.com/test/repo", informationalVersion: informationalVersion);

            var result = SourceLinkInformationExtractor.TryGetSourceLinkInfo(assembly, out var commitSha, out var repositoryUrl);

            result.Should().BeTrue();
            commitSha.Should().Be(expectedCommitSha);
            repositoryUrl.Should().Be("https://github.com/test/repo");
        }

        [Theory]
        [InlineData("1.0.0")] // No plus signs (parts.Length = 1)
        [InlineData("1.0.0+build+abc123")] // Multiple plus signs (parts.Length = 3)
        [InlineData("1.0.0-beta+build+abc123")] // Three parts with prerelease
        [InlineData("1.0.0+")] // Empty after plus
        [InlineData("1.0.0+build.meta.data+abc123")] // Multiple metadata with dots
        [InlineData("1.0.0+20130313144700+abc123")] // Complex build metadata
        [InlineData("1.0.0-alpha.1+build+abc123")] // With prerelease and multiple plus
        [InlineData("1.0.0-rc.1+exp.sha.5114f85+abc123")] // Complex prerelease with multiple plus
        [InlineData("1.0.0+build+meta+abc123")] // Four parts total
        [InlineData("1.0.0+abc123+def456+ghi789")] // Five parts total
        public void TryExtractFromAssemblyAttributes_InvalidInformationalVersion_FailsToExtractCommitSha(string informationalVersion)
        {
            var assembly = CreateTestAssembly(repositoryUrl: "https://github.com/test/repo", informationalVersion: informationalVersion);

            var result = SourceLinkInformationExtractor.TryGetSourceLinkInfo(assembly, out var commitSha, out var repositoryUrl);

            // Should return false because commit SHA extraction failed (even though repository URL is present)
            result.Should().BeFalse();
            commitSha.Should().BeNull();
            repositoryUrl.Should().BeNull();
        }

        [Fact]
        public void TryExtractFromAssemblyAttributes_MissingRepositoryUrl_ReturnsFalse()
        {
            var assembly = CreateTestAssembly(repositoryUrl: null, informationalVersion: "1.0.0+abc123");

            var result = SourceLinkInformationExtractor.TryGetSourceLinkInfo(assembly, out var commitSha, out var repositoryUrl);

            result.Should().BeFalse();
            commitSha.Should().BeNull();
            repositoryUrl.Should().BeNull();
        }

        [Fact]
        public void TryExtractFromAssemblyAttributes_MissingInformationalVersion_ReturnsFalse()
        {
            var assembly = CreateTestAssembly(repositoryUrl: "https://github.com/test/repo", informationalVersion: null);

            var result = SourceLinkInformationExtractor.TryGetSourceLinkInfo(assembly, out var commitSha, out var repositoryUrl);

            result.Should().BeFalse();
            commitSha.Should().BeNull();
            repositoryUrl.Should().BeNull();
        }

        [Fact]
        public void TryExtractFromAssemblyAttributes_EmptyInformationalVersion_ReturnsFalse()
        {
            var assembly = CreateTestAssembly(repositoryUrl: "https://github.com/test/repo", informationalVersion: string.Empty);

            var result = SourceLinkInformationExtractor.TryGetSourceLinkInfo(assembly, out var commitSha, out var repositoryUrl);

            result.Should().BeFalse();
            commitSha.Should().BeNull();
            repositoryUrl.Should().BeNull();
        }

        [Fact]
        public void BuildTimeSourceLink_IsUsedWithoutAttributesNorPdb()
        {
            // NativeAOT: the build gives the SourceLink document of the application's PDB.
            var assembly = CreateTestAssembly(repositoryUrl: null, informationalVersion: null);
            SourceLinkInformationExtractor.AddBuildTimeSourceLink(
                assembly.GetName().Name!,
                "{\"documents\":{\"/src/*\":\"https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/dd35903c688a74b62d1c6a9e4f41371c65704db8/*\"}}");

            var result = SourceLinkInformationExtractor.TryGetSourceLinkInfo(assembly, out var commitSha, out var repositoryUrl);

            result.Should().BeTrue();
            commitSha.Should().Be("dd35903c688a74b62d1c6a9e4f41371c65704db8");
            repositoryUrl.Should().Be("https://github.com/DataDog/dd-trace-dotnet");
        }

        private static Assembly CreateTestAssembly(string? repositoryUrl, string? informationalVersion)
        {
            var assemblyName = new AssemblyName($"TestAssembly_{System.Guid.NewGuid():N}");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

            if (repositoryUrl != null)
            {
                var repositoryUrlAttribute = new AssemblyMetadataAttribute("RepositoryUrl", repositoryUrl);
                assemblyBuilder.SetCustomAttribute(CreateCustomAttributeBuilder(repositoryUrlAttribute));
            }

            if (informationalVersion is not null)
            {
                var informationalVersionAttribute = new AssemblyInformationalVersionAttribute(informationalVersion);
                assemblyBuilder.SetCustomAttribute(CreateCustomAttributeBuilder(informationalVersionAttribute));
            }

            return assemblyBuilder;
        }

        private static CustomAttributeBuilder CreateCustomAttributeBuilder(Attribute attribute)
        {
            var attributeType = attribute.GetType();
            var constructor = attributeType.GetConstructors()[0];
            var constructorArgs = new object[constructor.GetParameters().Length];

            if (attribute is AssemblyMetadataAttribute metadataAttr)
            {
                constructorArgs[0] = metadataAttr.Key;
                constructorArgs[1] = metadataAttr.Value!;
            }
            else if (attribute is AssemblyInformationalVersionAttribute versionAttr)
            {
                constructorArgs[0] = versionAttr.InformationalVersion;
            }

            return new CustomAttributeBuilder(constructor, constructorArgs);
        }
    }
}
