// <copyright file="IisConfigurationTemplateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using System.Xml.Linq;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class IisConfigurationTemplateTests
    {
        private const string CurrentTemplate = """
            <configuration>
              <system.webServer>
                <handlers>[ASPNETCORE_HANDLER]</handlers>
                <aspNetCore processPath="[PROCESS_PATH]"[ARGUMENTS_ATTRIBUTE] hostingModel="[HOSTING_MODEL]" />
              </system.webServer>
            </configuration>
            """;

        private const string LegacyTemplate = """
            <configuration>
              <system.webServer>
                <handlers />
                <aspNetCore processPath="[DOTNET]" arguments="[RELATIVE_SAMPLE_PATH]" hostingModel="[HOSTING_MODEL]" />
              </system.webServer>
            </configuration>
            """;

        [Fact]
        public void AllRepositoryIisTemplatesSupportCoreClrExpansion()
        {
            var testDirectory = Path.Combine(EnvironmentTools.GetSolutionDirectory(), "tracer", "test");
            var templatePaths = Directory.GetFiles(testDirectory, "applicationHost.config", SearchOption.AllDirectories);
            templatePaths.Should().NotBeEmpty();

            foreach (var templatePath in templatePaths)
            {
                using (new AssertionScope(templatePath))
                {
                    var template = File.ReadAllText(templatePath)
                                       .Replace("[PATH]", @"C:\sample")
                                       .Replace("[PORT]", "12345")
                                       .Replace("[POOL]", "UnmanagedClassicAppPool")
                                       .Replace("[VIRTUAL_APPLICATION]", string.Empty);

                    _ = Expand(template, IisAppType.AspNetCoreOutOfProcess, isCoreClr: true, "Samples.AspNetCore.dll", out _);
                }
            }
        }

        [Theory]
        [InlineData(IisAppType.AspNetClassic, false)]
        [InlineData(IisAppType.AspNetClassic, true)]
        [InlineData(IisAppType.AspNetIntegrated, false)]
        [InlineData(IisAppType.AspNetIntegrated, true)]
        public void ClassicAspNetTemplatesAreValidAndHaveNoUnresolvedTokens(IisAppType appType, bool useLegacyTemplate)
        {
            var document = Expand(useLegacyTemplate ? LegacyTemplate : CurrentTemplate, appType, isCoreClr: false, "Samples.AspNetMvc5.exe", out var processToProfile);
            var aspNetCore = document.Descendants("aspNetCore").Single();

            processToProfile.Should().Be("dotnet.exe");
            aspNetCore.Attribute("processPath").Value.Should().Be("dotnet.exe");
            if (useLegacyTemplate)
            {
                aspNetCore.Attribute("arguments").Value.Should().BeEmpty();
            }
            else
            {
                aspNetCore.Attribute("arguments").Should().BeNull();
            }
        }

        [Theory]
        [InlineData(IisAppType.AspNetCoreInProcess, false, "inprocess")]
        [InlineData(IisAppType.AspNetCoreInProcess, true, "inprocess")]
        [InlineData(IisAppType.AspNetCoreOutOfProcess, false, "outofprocess")]
        [InlineData(IisAppType.AspNetCoreOutOfProcess, true, "outofprocess")]
        public void CoreClrTemplatesUseDotnetAndSampleDll(IisAppType appType, bool useLegacyTemplate, string expectedHostingModel)
        {
            var document = Expand(useLegacyTemplate ? LegacyTemplate : CurrentTemplate, appType, isCoreClr: true, "Samples.AspNetCoreMvc31.dll", out var processToProfile);
            var aspNetCore = document.Descendants("aspNetCore").Single();

            processToProfile.Should().Be("dotnet.exe");
            aspNetCore.Attribute("processPath").Value.Should().Be("dotnet.exe");
            aspNetCore.Attribute("arguments").Value.Should().Be(@".\Samples.AspNetCoreMvc31.dll");
            aspNetCore.Attribute("hostingModel").Value.Should().Be(expectedHostingModel);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FrameworkOutOfProcessTemplatesLaunchAndProfileSampleExecutable(bool useLegacyTemplate)
        {
            var document = Expand(useLegacyTemplate ? LegacyTemplate : CurrentTemplate, IisAppType.AspNetCoreOutOfProcess, isCoreClr: false, "Samples.AspNetCoreNetFramework.exe", out var processToProfile);
            var aspNetCore = document.Descendants("aspNetCore").Single();

            processToProfile.Should().Be("Samples.AspNetCoreNetFramework.exe");
            aspNetCore.Attribute("processPath").Value.Should().Be(@".\Samples.AspNetCoreNetFramework.exe");
            if (useLegacyTemplate)
            {
                aspNetCore.Attribute("arguments").Value.Should().BeEmpty();
                document.Descendants("add").Should().BeEmpty("legacy templates rely on the sample's published web.config for the ANCM handler");
            }
            else
            {
                aspNetCore.Attribute("arguments").Should().BeNull();
                document.Descendants("add")
                        .Where(element => (string)element.Attribute("name") == "aspNetCore")
                        .Should()
                        .ContainSingle();
            }
        }

        private static XDocument Expand(
            string template,
            IisAppType appType,
            bool isCoreClr,
            string sampleApplicationFileName,
            out string processToProfile)
        {
            var expanded = TestHelper.ExpandIisConfigurationTemplate(
                template,
                appType,
                "dotnet.exe",
                sampleApplicationFileName,
                isCoreClr,
                out processToProfile);

            expanded.Should().NotContain("[DOTNET]")
                    .And.NotContain("[RELATIVE_SAMPLE_PATH]")
                    .And.NotContain("[PROCESS_PATH]")
                    .And.NotContain("[ARGUMENTS_ATTRIBUTE]")
                    .And.NotContain("[HOSTING_MODEL]")
                    .And.NotContain("[ASPNETCORE_HANDLER]");
            return XDocument.Parse(expanded);
        }
    }
}
