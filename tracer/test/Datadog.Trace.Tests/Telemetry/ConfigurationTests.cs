// <copyright file="ConfigurationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry;

public class ConfigurationTests
{
    private static readonly string[] ExcludedKeys =
    {
        // Lambda handler extracts these directly from env var, and no reason to think that will change
        "_DD_EXTENSION_ENDPOINT",
        "_DD_EXTENSION_PATH",
        // mini agent uses this directly from env var, and no reason to think that will change
        "DD_MINI_AGENT_PATH",
        "DD_ENTITY_ID", // Datadog.Trace.Vendors.StatsdClient.StatsdConfig.EntityIdEnvVar (we don't use this, it was just vendored in)
        // CIapp extracts  directly from env var, and no reason to think that will change
        "DD_CUSTOM_TRACE_ID",
        "DD_GIT_BRANCH",
        "DD_GIT_TAG",
        "DD_GIT_REPOSITORY_URL",
        "DD_GIT_COMMIT_SHA",
        "DD_GIT_COMMIT_MESSAGE",
        "DD_GIT_COMMIT_AUTHOR_NAME",
        "DD_GIT_COMMIT_AUTHOR_EMAIL",
        "DD_GIT_COMMIT_AUTHOR_DATE",
        "DD_GIT_COMMIT_COMMITTER_NAME",
        "DD_GIT_COMMIT_COMMITTER_EMAIL",
        "DD_GIT_COMMIT_COMMITTER_DATE",
        "DD_ACTION_EXECUTION_ID",
        "DD_PIPELINE_EXECUTION_ID",
        "DD_TESTSESSION_COMMAND",
        "DD_TESTSESSION_WORKINGDIRECTORY",
        "DD_CIVISIBILITY_CODE_COVERAGE_MODE",
        // Internal env vars that we only ever read from environment
        "DD_INTERNAL_TRACE_NATIVE_ENGINE_PATH",
        "DD_DOTNET_TRACER_HOME",
        "DD_INSTRUMENTATION_INSTALL_ID",
        "DD_INSTRUMENTATION_INSTALL_TYPE",
        "DD_INSTRUMENTATION_INSTALL_TIME",
        "DD_INJECTION_ENABLED",
        "DD_INJECT_FORCE",
    };

    [Fact]
    public void AllConfigurationValuesAreRegisteredWithIntake()
    {
        // Only configuration keys defined in the following json documents should be submitted
        // https://github.com/DataDog/dd-go/blob/prod/trace/apps/tracer-telemetry-intake/telemetry-payload/static/config_norm_rules.json
        // https://github.com/DataDog/dd-go/blob/prod/trace/apps/tracer-telemetry-intake/telemetry-payload/static/config_prefix_block_list.json
        //
        // These are duplicated in this repo. When adding new configuration, add them into the embedded json files here, then
        // after merging, update the source JSON file in dd-go
        var configNormRules = JsonConvert.DeserializeObject<Dictionary<string, string>>(GetData("config_norm_rules.json"));
        configNormRules.Should().NotBeNullOrEmpty();
        var blockPrefixes = JsonConvert.DeserializeObject<List<string>>(GetData("config_prefix_block_list.json"));

        // Extract user strings from assembly, based on:
        // https://gist.github.com/vbelcik/01d0f803b9db6ec9b90e8693e4b0493b#file-extractexenetstrings-cs
        // Crude, but easier than regex-ing the source code etc
        var assemblyStrings = ReadAllUserStrings(typeof(Datadog.Trace.Tracer).Assembly);
        assemblyStrings.Should().NotBeNullOrEmpty();

        // we know that we generally store config keys in `ConfigurationKeys` so examine all those
        var configKeyStrings = GetConfigurationKeyStrings();

        var allPotentialConfigKeys = assemblyStrings
                                    .Where(x => (x.StartsWith("DD_") || x.StartsWith("_DD") || x.StartsWith("DATADOG_") || x.StartsWith("OTEL_")) && !x.Contains(" "))
                                    .Concat(configKeyStrings)
                                    .Where(x => !x.Contains("{0}")) // exclude the format string ones
                                    .Distinct()
                                    .Where(x => !ExcludedKeys.Contains(x))
                                    .ToList();

        var keysWithoutConfig = new List<string>();
        foreach (var configKey in allPotentialConfigKeys)
        {
            if (configNormRules.ContainsKey(configKey))
            {
                continue;
            }

            var foundPrefix = false;
            foreach (var prefix in blockPrefixes)
            {
                if (configKey.StartsWith(prefix))
                {
                    foundPrefix = true;
                    break;
                }
            }

            if (!foundPrefix)
            {
                keysWithoutConfig.Add(configKey);
            }
        }

        keysWithoutConfig
           .OrderBy(x => x)
           .Should()
           .BeEmpty($"Keys should be listed in config_norm_rules or block_prefixes");
    }

    private static List<string> GetConfigurationKeyStrings()
    {
        var configKeys = typeof(ConfigurationKeys);
        var results = new List<string>();

        AddConstantValues(results, configKeys);
        foreach (var nestedType in configKeys.GetNestedTypes())
        {
            AddConstantValues(results, nestedType);
        }

        return results;

        static void AddConstantValues(List<string> results, Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                // assume that it's a constant
                if (field.FieldType == typeof(string)
                 && field.GetRawConstantValue() is string value)
                {
                    results.Add(value);
                }
            }
        }
    }

    private static string GetData(string filename)
    {
        var thisAssembly = typeof(ConfigurationTests).Assembly;
        var stream = thisAssembly.GetManifestResourceStream($"Datadog.Trace.Tests.Telemetry.{filename}");
        using var streamReader = new StreamReader(stream);
        return streamReader.ReadToEnd();
    }

    private static List<string> ReadAllUserStrings(Assembly assembly)
    {
        using (BinaryReader r = new BinaryReader(new FileStream(assembly.Location, FileMode.Open, FileAccess.Read)))
        {
            while (true)
            {
                while (r.ReadUInt32() != 0x424A5342)
                {
                    // seek to magic
                }

                long pos = r.BaseStream.Position;

                try
                {
                    return ReadAllUserStringsFromMetadata(r);
                }
                catch { }

                r.BaseStream.Position = pos;
            }
        }

        static List<string> ReadAllUserStringsFromMetadata(BinaryReader r)
        {
            long metadataRootPos = r.BaseStream.Position - 4;

            r.ReadUInt32(); // Major, Minor Version

            if (r.ReadUInt32() != 0)
            {
                // Reserved
                throw new Exception();
            }

            uint length = r.ReadUInt32(); // Length

            r.BaseStream.Position += length; // skip Version string

            if (r.ReadUInt16() != 0)
            {
                // Flags, Reserved
                throw new Exception();
            }

            int streams = r.ReadUInt16(); // Streams

            // StreamHeaders
            while (streams > 0)
            {
                streams--;

                uint offset, size;

                if (ReadStreamHeader(r, out offset, out size) == "#US")
                {
                    r.BaseStream.Position = metadataRootPos + offset;
                    long endPos = metadataRootPos + offset + size;

                    if (ReadUserString(r) != null)
                    {
                        throw new Exception();
                    }

                    List<string> lst = new List<string>();

                    while (r.BaseStream.Position < endPos)
                    {
                        string str = ReadUserString(r);

                        if (str != null)
                        {
                            lst.Add(str);
                        }
                    }

                    return lst;
                }
            }

            throw new Exception();
        }

        static string ReadStreamHeader(BinaryReader r, out uint offset, out uint size)
        {
            offset = r.ReadUInt32();
            size = r.ReadUInt32();

            int cc = 0;
            string name = string.Empty;

            while (true)
            {
                byte b = r.ReadByte();
                cc++;

                if (b == 0)
                {
                    while (cc % 4 != 0)
                    {
                        if (r.ReadByte() != 0)
                        {
                            throw new Exception();
                        }

                        cc++;
                    }

                    if (cc > 32)
                    {
                        throw new Exception();
                    }

                    return name;
                }

                name += (char)b;
            }
        }

        static string ReadUserString(BinaryReader r)
        {
            int b = r.ReadByte();

            int size;

            if ((b & 0x80) == 0)
            {
                size = b;
            }
            else if ((b & 0xC0) == 0x80)
            {
                int x = r.ReadByte();

                size = ((b & ~0xC0) << 8) | x;
            }
            else if ((b & 0xE0) == 0xC0)
            {
                int x = r.ReadByte();
                int y = r.ReadByte();
                int z = r.ReadByte();

                size = ((b & ~0xE0) << 24) | (x << 16) | (y << 8) | z;
            }
            else
            {
                throw new Exception();
            }

            if (size == 0)
            {
                return null;
            }

            if (size % 2 != 1)
            {
                throw new Exception();
            }

            int charCnt = size / 2;

            StringBuilder sb = new StringBuilder(charCnt);

            for (int i = 0; i < charCnt; i++)
            {
                sb.Append((char)r.ReadUInt16());
            }

            byte finalByte = r.ReadByte();

            if ((finalByte != 0) && (finalByte != 1))
            {
                throw new Exception();
            }

            return sb.ToString();
        }
    }
}
