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
            static void BuildInstrumentedAssemblies(StringBuilder sb, IReadOnlyCollection<CallTargetDefinitionSource> orderedDefinitions)
            {
                var hashSet = new HashSet<string>();
                foreach (var orderedDefinition in orderedDefinitions)
                {
                    if (!IsKnownAssemblyPrefix(orderedDefinition.AssemblyName))
                    {
                        hashSet.Add(orderedDefinition.AssemblyName);
                    }
                }

                var doneFirst = false;

                foreach (var assembly in hashSet.OrderBy(x => x))
                {
                    if (doneFirst)
                    {
                        sb
                           .Append(
                                """
                                
                                            || 
                                """);
                    }

                    sb
                       .Append("assemblyName.StartsWith(\"")
                       .Append(assembly)
                      .Append(",\", StringComparison.Ordinal)");
                    doneFirst = true;
                }

                if (!doneFirst)
                {
                    sb.Append("false");
                }

                return;

                // NOTE: Keep this in sync with ExceptionRedactor.IsKnownAssemblyPrefix()
                static bool IsKnownAssemblyPrefix(string assemblyName)
                {
                    return assemblyName.StartsWith("Datadog.", StringComparison.Ordinal)
                        || assemblyName.StartsWith("mscorlib,", StringComparison.Ordinal) // note that this uses ',' not '.' as it's the full assembly name
                        || assemblyName.StartsWith("Microsoft.", StringComparison.Ordinal)
                        || assemblyName.StartsWith("System.", StringComparison.Ordinal)
                        || assemblyName.StartsWith("Azure.", StringComparison.Ordinal)
                        || assemblyName.StartsWith("AWSSDK.", StringComparison.Ordinal);
                }
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
        static InstrumentationDefinitions()
        {{");
            var orderedDefinitions = definitions
                                    .OrderBy(static x => x.IntegrationName)
                                    .ThenBy(static x => x.AssemblyName)
                                    .ThenBy(static x => x.TargetTypeName)
                                    .ThenBy(static x => x.TargetMethodName)
                                    .ToList();
            sb.Append(@$"
        }}

        /// <summary>
        /// Checks if the provided <see cref=""System.Reflection.Assembly.FullName""/> assembly
        /// is one we instrument. Assumes you have already checked for ""well-known"" prefixes
        /// like ""System"" and ""Microsoft"".
        /// </summary>
        internal static bool IsInstrumentedAssembly(string assemblyName)
            => ");

            BuildInstrumentedAssemblies(sb, definitions);

            sb.Append($@";

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
    }
}
