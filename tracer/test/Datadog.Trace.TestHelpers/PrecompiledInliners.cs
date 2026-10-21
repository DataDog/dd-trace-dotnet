// <copyright file="PrecompiledInliners.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Datadog.Trace.TestHelpers
{
    public static class PrecompiledInliners
    {
        /// <summary>
        /// Asserts, from the native debug log of the process, that the assembly loaded from its NGen or ReadyToRun image
        /// and that the precompiled callers of its instrumented methods were found. Without them, a test passing doesn't
        /// show that those callers were ReJITed: the image may not have inlined the instrumented methods.
        /// </summary>
        public static void ShouldHaveBeenFound(string logDirectory, int processId, string assemblyName)
        {
            var log = string.Join(
                "\n",
                Directory.GetFiles(logDirectory, $"dotnet-tracer-native-*-{processId}.log").Select(File.ReadAllText));
            var moduleIds = Regex.Matches(log, $@"ModuleLoadFinished: (\d+) {Regex.Escape(assemblyName)} AppDomain .* \| IsNGEN = true")
                                 .Cast<Match>()
                                 .Select(match => match.Groups[1].Value)
                                 .ToList();

            moduleIds.Should().NotBeEmpty($"{assemblyName} should load from its precompiled image");
            moduleIds.Should().Contain(
                moduleId => Regex.IsMatch(log, $@"NGEN:: Processed with \d+ inliners \[ModuleId={moduleId},"),
                $"the precompiled callers of the instrumented methods of {assemblyName} should be found");
        }
    }
}
