using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.SourceGenerators.Helpers;
using Mono.Cecil;
using NuGet.Protocol;
using Nuke.Common.IO;
using YamlDotNet.Core;

namespace CodeGenerators
{
    internal static class CallTargetsGenerator
    {
        private const string NullLiteral = "null";

        public static void GenerateCallTargets(IEnumerable<TargetFramework> targetFrameworks, Func<string, string> getDllPath, AbsolutePath outputPath, string version) 
        {
            Serilog.Log.Debug("Generating CallTarget definitions file ...");

            Dictionary<CallTargetDefinitionSource, TargetFrameworks> definitions = new Dictionary<CallTargetDefinitionSource, TargetFrameworks>();
            foreach(var tfm in targetFrameworks)
            {
                var dllPath = getDllPath(tfm);
                RetrieveCallTargets(definitions, dllPath, tfm);
            }

            GenerateCallSites(definitions, outputPath, version);
        }

        internal static void RetrieveCallTargets(Dictionary<CallTargetDefinitionSource, TargetFrameworks> definitions, string dllPath, TargetFramework tfm)
        {
            // We check if the assembly file exists.
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"Error extracting types for CallTarget generation. Assembly file was not found. Path: {dllPath}", dllPath);
            }

            var tfmCategory = GetCategory(tfm);

            // Open dll to extract all AspectsClass attributes.
            using var asmDefinition = Mono.Cecil.AssemblyDefinition.ReadAssembly(dllPath);

            foreach (var type in asmDefinition.MainModule.Types)
            {
                var attribute = type.CustomAttributes.FirstOrDefault(IsCallTargetClass);
                if (attribute is null)
                {
                    continue;
                }

                foreach (var definition in GetCallTargetDefinition(type, attribute))
                {
                    definitions.TryGetValue(definition, out var tfms);
                    definitions[definition] = (tfms | tfmCategory);
                }
            }

            static bool IsCallTargetClass(Mono.Cecil.CustomAttribute attribute)
            {
                return attribute.AttributeType.FullName.StartsWith("Datadog.Trace.ClrProfiler.InstrumentMethodAttribute");
            }

            static List<CallTargetDefinitionSource> GetCallTargetDefinition(TypeDefinition type, CustomAttribute attribute)
            {
                List<CallTargetDefinitionSource> res = new List<CallTargetDefinitionSource>();
                var hasMisconfiguredInput = false;
                string? assemblyName = null;
                string[]? assemblyNames = null;
                string? integrationName = null;
                string? typeName = null;
                string[]? typeNames = null;
                string? methodName = null;
                string? returnTypeName = null;
                string? minimumVersion = null;
                string? maximumVersion = null;
                string[]? parameterTypeNames = null;
                string? callTargetType = null;
                int? integrationKind = null;
                var instrumentationCategory = InstrumentationCategory.Tracing;

                foreach (var namedArgument in attribute.Properties)
                {
                    switch (namedArgument.Name)
                    {
                        case nameof(InstrumentAttributeProperties.AssemblyName):
                            assemblyName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(InstrumentAttributeProperties.AssemblyNames):
                            assemblyNames = GetStringArray(namedArgument.Argument.Value);
                            break;
                        case nameof(InstrumentAttributeProperties.IntegrationName):
                            integrationName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(InstrumentAttributeProperties.TypeName):
                            typeName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(InstrumentAttributeProperties.TypeNames):
                            typeNames = GetStringArray(namedArgument.Argument.Value);
                            break;
                        case nameof(InstrumentAttributeProperties.MethodName):
                            methodName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(InstrumentAttributeProperties.ReturnTypeName):
                            returnTypeName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(InstrumentAttributeProperties.MinimumVersion):
                            minimumVersion = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(InstrumentAttributeProperties.MaximumVersion):
                            maximumVersion = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(InstrumentAttributeProperties.ParameterTypeNames):
                            parameterTypeNames = GetStringArray(namedArgument.Argument.Value);
                            break;
                        case nameof(InstrumentAttributeProperties.CallTargetType):
                            //callTargetType = (namedArgument.Argument.Value as INamedTypeSymbol)?.ToDisplayString();
                            break;
                        case nameof(InstrumentAttributeProperties.CallTargetIntegrationKind):
                            integrationKind = namedArgument.Argument.Value as int?;
                            break;
                        case nameof(InstrumentAttributeProperties.InstrumentationCategory):
                            instrumentationCategory = (InstrumentationCategory)(namedArgument.Argument.Value as uint?).GetValueOrDefault();
                            break;
                        default:
                            hasMisconfiguredInput = true;
                            break;
                    }

                    if (hasMisconfiguredInput)
                    {
                        break;
                    }
                }

                (ushort Major, ushort Minor, ushort Patch) minVersion = default;
                (ushort Major, ushort Minor, ushort Patch) maxVersion = default;
                TryGetVersion(minimumVersion, ushort.MinValue, out minVersion);
                TryGetVersion(maximumVersion, ushort.MaxValue, out maxVersion);

                foreach (var assembly in assemblyNames ?? new[] { assemblyName })
                {
                    foreach (var t in typeNames ?? new[] { typeName })
                    {
                        res.Add(
                            new CallTargetDefinitionSource(
                                integrationName: integrationName!,
                                assemblyName: assembly!,
                                targetTypeName: t!,
                                targetMethodName: methodName!,
                                targetReturnType: returnTypeName!,
                                targetParameterTypes: parameterTypeNames ?? Array.Empty<string>(),
                                minimumVersion: minVersion,
                                maximumVersion: maxVersion,
                                instrumentationTypeName: callTargetType ?? type.FullName,
                                integrationKind: integrationKind ?? 0,
                                isAdoNetIntegration: false,
                                instrumentationCategory: instrumentationCategory));
                    }
                }
                return res;
            }

            static string[] GetStringArray(object value)
            {
                if(value is CustomAttributeArgument[] values)
                {
                    return values.Select(v => Convert.ToString(v.Value)).ToArray();
                }

                return null;
            }

            static bool TryGetVersion(string version, ushort defaultValue, out (ushort Major, ushort Minor, ushort Patch) parsedVersion)
            {
                var major = defaultValue;
                var minor = defaultValue;
                var patch = defaultValue;

                var parts = version.Split('.');
                if (parts.Length >= 1 && !ParsePart(parts[0], defaultValue, out major))
                {
                    parsedVersion = default;
                    return false;
                }

                if (parts.Length >= 2 && !ParsePart(parts[1], defaultValue, out minor))
                {
                    parsedVersion = default;
                    return false;
                }

                if (parts.Length >= 3 && !ParsePart(parts[2], defaultValue, out patch))
                {
                    parsedVersion = default;
                    return false;
                }

                parsedVersion = (major, minor, patch);
                return true;

                static bool ParsePart(string part, ushort defaultValue, out ushort value)
                {
                    if (part == "*")
                    {
                        value = defaultValue;
                        return true;
                    }

                    if (ushort.TryParse(part, out value))
                    {
                        return true;
                    }

                    value = defaultValue;
                    return false;
                }
            }
        }

        internal static void GenerateCallSites(Dictionary<CallTargetDefinitionSource, TargetFrameworks> definitions, AbsolutePath outputPath, string version)
        {
            var sb = new StringBuilder();
            sb.AppendLine("""
                // <copyright company="Datadog">
                // Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
                // This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
                // </copyright>
                // <auto-generated/>
                #pragma once
                #include "generated_definitions.h"

                namespace trace
                {
                """);

            var assemblyName = $"Datadog.Trace, Version={version}.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";
            sb.AppendLine($"WCHAR* assemblyName = (WCHAR*)WStr(\"{assemblyName}\");");
            sb.AppendLine();

            // Retrieve all signatures
            HashSet<string> signatureTexts = new HashSet<string>();
            foreach (var definition in definitions)
            {
                signatureTexts.Add(GetSignature(definition.Key));
            }
           
            Dictionary<string, string> signatures = new Dictionary<string, string>();
            foreach (var sig in signatureTexts.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                if (!signatures.TryGetValue(sig, out var sigName))
                {
                    sigName = GetSignatureName(signatures.Count);
                    signatures[sig] = sigName;
                }
            }

            //Write all signatures
            foreach(var signature in signatures.OrderBy(x => x.Value, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(GetSignatureField(signature.Value, signature.Key));
            }


            //Write all CallTargets
            sb.AppendLine();
            sb.AppendLine("std::vector<CallTargetDefinition2> g_callTargets=");
            sb.AppendLine("{");
            int x = 0;
            foreach (var definition in definitions
                                        .OrderBy(static x => x.Key.IntegrationName)
                                        .ThenBy(static x => x.Key.AssemblyName)
                                        .ThenBy(static x => x.Key.TargetTypeName)
                                        .ThenBy(static x => x.Key.TargetMethodName))
            {
                sb.AppendLine(GetCallTarget(definition.Key, signatures[GetSignature(definition.Key)], definition.Value, x++));
            }

            sb.AppendLine("""
                };
                }
                """);


            if (!Directory.Exists(outputPath)) { Directory.CreateDirectory(outputPath); }
            var fileName = outputPath / "generated_calltargets.g.h";
            File.WriteAllText(fileName, sb.ToString());

            Serilog.Log.Information("CallTarget definitions File saved: {File}", fileName);

            static string GetSignature(CallTargetDefinitionSource definition)
            {
                string sig = $"(WCHAR*)WStr(\"{definition.TargetReturnType}\"),";
                foreach (var arg in definition.TargetParameterTypes)
                {
                    sig += $"(WCHAR*)WStr(\"{arg}\"),";
                }

                return sig;
            }

            static string GetSignatureName(int index)
            {
                return $"g_callTargets_Sig_{index:000}";
            }

            static string GetSignatureField(string signatureName, string signature)
            {
                return $"WCHAR* {signatureName}[]={{{signature}}};";
            }

            static string GetCallTarget(CallTargetDefinitionSource definition, string signature, TargetFrameworks tfms, int index)
            {
                var min = definition.MinimumVersion;
                var max = definition.MaximumVersion;

                var typ = $"(WCHAR*)WStr(\"{definition.AssemblyName}\"),(WCHAR*)WStr(\"{definition.TargetTypeName}\"),(WCHAR*)WStr(\"{definition.TargetMethodName}\"),";
                var sig = $"{signature},{definition.TargetParameterTypes.Count + 1},";
                var ver = $"{min.Major},{min.Minor},{min.Patch},{max.Major},{max.Minor},{max.Patch},";
                var asy = $"assemblyName,(WCHAR*)WStr(\"{definition.InstrumentationTypeName}\"),{GetCallTargetKind(definition.IntegrationKind)},{(int)definition.InstrumentationCategory}";
                var tfm = $",{(uint)tfms}";
                return $"{{{typ}{sig}{ver}{asy}{tfm}}},";
            }

            static string GetCallTargetKind(int kind)
            {
                /*
                 enum class CallTargetKind : UINT8
                {
                    Default = 0,
                    Derived = 1,
                    Interface = 2
                };
                 */
                return kind switch
                {
                    0 => "CallTargetKind::Default",
                    1 => "CallTargetKind::Derived",
                    2 => "CallTargetKind::Interface",
                    _ => "ERROR"
                };
            }
        }

        internal static TargetFrameworks GetCategory(TargetFramework tfm)
        {
            return (TargetFrameworks)Enum.Parse<TargetFrameworks>(tfm.ToString().ToUpper().Replace('.', '_'));
        }

        internal record CallTargetDefinitionSource
        {
            public CallTargetDefinitionSource(string integrationName, string assemblyName, string targetTypeName, string targetMethodName, string targetReturnType, string[] targetParameterTypes, (ushort Major, ushort Minor, ushort Patch) minimumVersion, (ushort Major, ushort Minor, ushort Patch) maximumVersion, string instrumentationTypeName, int integrationKind, bool isAdoNetIntegration, InstrumentationCategory instrumentationCategory)
            {
                IntegrationName = integrationName;
                AssemblyName = assemblyName;
                TargetTypeName = targetTypeName;
                TargetMethodName = targetMethodName;
                TargetReturnType = targetReturnType;
                TargetParameterTypes = new EquatableArray<string>(targetParameterTypes);
                MinimumVersion = minimumVersion;
                MaximumVersion = maximumVersion;
                InstrumentationTypeName = instrumentationTypeName;
                IntegrationKind = integrationKind;
                IsAdoNetIntegration = isAdoNetIntegration;
                InstrumentationCategory = instrumentationCategory;
            }

            public string IntegrationName { get; }

            public string AssemblyName { get; }

            public string TargetTypeName { get; }

            public string TargetMethodName { get; }

            public string TargetReturnType { get; }

            public EquatableArray<string> TargetParameterTypes { get; }

            public (ushort Major, ushort Minor, ushort Patch) MinimumVersion { get; }

            public (ushort Major, ushort Minor, ushort Patch) MaximumVersion { get; }

            public string InstrumentationTypeName { get; }

            public int IntegrationKind { get; }

            public bool IsAdoNetIntegration { get; }

            public InstrumentationCategory InstrumentationCategory { get; }
        }

        private static class InstrumentAttributeProperties
        {
            public const string AssemblyName = nameof(AssemblyName);
            public const string AssemblyNames = nameof(AssemblyNames);
            public const string TypeName = nameof(TypeName);
            public const string TypeNames = nameof(TypeNames);
            public const string MethodName = nameof(MethodName);
            public const string ReturnTypeName = nameof(ReturnTypeName);
            public const string ParameterTypeNames = nameof(ParameterTypeNames);
            public const string MinimumVersion = nameof(MinimumVersion);
            public const string MaximumVersion = nameof(MaximumVersion);
            public const string IntegrationName = nameof(IntegrationName);
            public const string CallTargetType = nameof(CallTargetType);
            public const string CallTargetIntegrationKind = nameof(CallTargetIntegrationKind);
            public const string InstrumentationCategory = nameof(InstrumentationCategory);
        }
    }
}
