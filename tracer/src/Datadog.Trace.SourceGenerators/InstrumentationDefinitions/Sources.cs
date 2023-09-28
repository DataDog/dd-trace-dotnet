// <copyright file="Sources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.SourceGenerators.InstrumentationDefinitions
{
    internal static class Sources
    {
        private const string InstrumentationsCollectionName = "Instrumentations";

        public static string CreateCallTargetDefinitions(IReadOnlyCollection<CallTargetDefinitionSource> definitions)
        {
            void BuildInstrumentationDefinitions(StringBuilder sb, List<CallTargetDefinitionSource> orderedDefinitions, string instrumentationsCollectionName)
            {
                string? integrationName = null;

                sb.Append($@"
            // CallTarget types
            {instrumentationsCollectionName} = new NativeCallTargetDefinition2[]
            {{
");
                foreach (var definition in orderedDefinitions)
                {
                    integrationName = WriteDefinition(definition, integrationName, sb);
                }

                sb.Append($@"
            }};");
            }

            var sb = new StringBuilder();
            sb.Append(Datadog.Trace.SourceGenerators.Constants.FileHeader);
            sb.Append(
                $@"using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{{
    internal static partial class InstrumentationDefinitions
    {{
        internal static NativeCallTargetDefinition2[] {InstrumentationsCollectionName};

        static InstrumentationDefinitions()
        {{");
            var orderedDefinitions = definitions
                                    .OrderBy(static x => x.IntegrationName)
                                    .ThenBy(static x => x.AssemblyName)
                                    .ThenBy(static x => x.TargetTypeName)
                                    .ThenBy(static x => x.TargetMethodName)
                                    .ToList();

            BuildInstrumentationDefinitions(sb, orderedDefinitions, InstrumentationsCollectionName);
            sb.Append(@$"
        }}

        internal static Datadog.Trace.Configuration.IntegrationId? GetIntegrationId(string? integrationTypeName, System.Type targetType)
        {{
            return integrationTypeName switch
            {{
                // integrations with a single IntegrationId per implementation type
                ");
            string? integrationName = null;
            foreach (var definition in orderedDefinitions)
            {
                if (definition.IsAdoNetIntegration)
                {
                    // Only "normal" integrations
                    continue;
                }

                // This assumes that each type is only associated with a single integration name
                // will cause compilation failures if that's not the case (so "safe" in that sense)
                if (definition.IntegrationName == integrationName)
                {
                    sb.Append(@"or ");
                }
                else if (definition.IntegrationName != integrationName)
                {
                    if (integrationName is not null)
                    {
                        // write the previous result
                        // Assumes that IntegrationName is a valid IntegrationId
                        // (Will cause compilation failures if not (so "safe")
                        sb.Append(@"=> Datadog.Trace.Configuration.IntegrationId.")
                          .Append(integrationName)
                          .Append(@",
                ");
                    }

                    integrationName = definition.IntegrationName;
                }

                sb.Append('"')
                  .Append(definition.InstrumentationTypeName)
                  .Append(@"""
                    ");
            }

            if (integrationName is not null)
            {
                // write the last one
                sb.Append(@"=> Datadog.Trace.Configuration.IntegrationId.")
                  .Append(integrationName)
                  .Append(',')
                  .AppendLine();
            }

            sb.Append(@"
                // adonet integrations
                ");

            bool doneFirst = false;
            foreach (var definition in orderedDefinitions)
            {
                if (!definition.IsAdoNetIntegration)
                {
                    // Only "adonet" integrations
                    continue;
                }

                if (doneFirst)
                {
                    sb.Append("or ");
                }

                doneFirst = true;
                sb.Append('"')
                  .Append(definition.InstrumentationTypeName)
                  .Append(@"""
                    ");
            }

            if (doneFirst)
            {
                sb.Append(
                    @"=> GetAdoNetIntegrationId(
                        integrationTypeName: integrationTypeName,
                        targetTypeName: targetType.FullName,
                        assemblyName: targetType.Assembly.GetName().Name),
                ");
            }

            sb.Append(
                @"_ => null,
            };
        }

        public static Datadog.Trace.Configuration.IntegrationId? GetAdoNetIntegrationId(string? integrationTypeName, string? targetTypeName, string? assemblyName)
        {
            return new System.Collections.Generic.KeyValuePair<string?, string?>(assemblyName, targetTypeName) switch
            {
                ");

            integrationName = null;
            foreach (var definition in orderedDefinitions)
            {
                if (!definition.IsAdoNetIntegration || definition.IntegrationKind != 0)
                {
                    // only non-derived "adonet" integrations
                    continue;
                }

                if (definition.IntegrationName == integrationName)
                {
                    sb.Append(@"or ");
                }
                else if (definition.IntegrationName != integrationName)
                {
                    if (integrationName is not null)
                    {
                        // write the previous result
                        // Assumes that IntegrationName is a valid IntegrationId
                        // (Will cause compilation failures if not (so "safe")
                        sb.Append(@"=> Datadog.Trace.Configuration.IntegrationId.")
                          .Append(integrationName)
                          .Append(@",
                    ");
                    }

                    integrationName = definition.IntegrationName;
                }

                sb.Append(@"{ Key: """)
                  .Append(definition.AssemblyName)
                  .Append(@""", Value: """)
                  .Append(definition.TargetTypeName)
                  .Append(@""" }
                    ");
            }

            if (integrationName is not null)
            {
                // write the last one
                sb.Append(@"=> Datadog.Trace.Configuration.IntegrationId.")
                  .Append(integrationName)
                  .Append(@",
                ");
            }

            sb.Append(@"// derived attribute, assume ADO.NET
                _ => Datadog.Trace.Configuration.IntegrationId.AdoNet,
            };
        }
    }
}
");

            return sb.ToString();
        }

        private static string WriteDefinition(CallTargetDefinitionSource definition, string? integrationName, StringBuilder sb)
        {
            if (definition.IntegrationName != integrationName)
            {
                if (integrationName is not null)
                {
                    sb.AppendLine();
                }

                integrationName = definition.IntegrationName;
                sb.Append(
                $@"
                // {integrationName}");
            }

            sb.Append(
               @"
                new (NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(""")
              .Append(definition.AssemblyName)
              .Append(@"""), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(""")
              .Append(definition.TargetTypeName)
              .Append(@"""), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(""")
              .Append(definition.TargetMethodName)
              .Append(@"""), ");

            var paramLengths = (definition.TargetParameterTypes?.Length ?? 0) + 1;
            if (paramLengths > 9)
            {
                sb.Append(@"NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray(new[] { """)
                  .Append(definition.TargetReturnType)
                  .Append(@"""");

                foreach (var parameterType in definition.TargetParameterTypes!)
                {
                    sb.Append(@", """)
                      .Append(parameterType)
                      .Append('"');
                }

                sb.Append(" }), ")
                  .Append(paramLengths)
                  .Append(", ");
            }
            else
            {
                sb.Append(@"NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16StringArray(""")
                  .Append(definition.TargetReturnType)
                  .Append(@"""");

                if (definition.TargetParameterTypes is { Length: > 0 } types)
                {
                    foreach (var parameterType in types)
                    {
                        sb.Append(@", """)
                          .Append(parameterType)
                          .Append('"');
                    }
                }

                sb.Append("), ")
                  .Append(paramLengths)
                  .Append(", ");
            }

            var min = definition.MinimumVersion;
            var max = definition.MaximumVersion;
            sb.Append(min.Major)
              .Append(", ")
              .Append(min.Minor)
              .Append(", ")
              .Append(min.Patch)
              .Append(", ")
              .Append(max.Major)
              .Append(", ")
              .Append(max.Minor)
              .Append(", ")
              .Append(max.Patch);

            sb.Append(@", NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(assemblyFullName), NativeCallTargetUnmanagedMemoryHelper.AllocateAndWriteUtf16String(""")
              .Append(definition.InstrumentationTypeName)
              .Append(@"""), ")
              .Append($"{definition.IntegrationKind}, ")
              .Append($"{(int)definition.InstrumentationCategory}),");
            return integrationName;
        }
    }
}
