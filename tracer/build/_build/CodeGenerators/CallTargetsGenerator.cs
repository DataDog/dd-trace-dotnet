using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Newtonsoft.Json;
using Nuke.Common.IO;
using Logger = Serilog.Log;

namespace CodeGenerators
{
    internal static class CallTargetsGenerator
    {
        private const string NullLiteral = "null";

        public static void GenerateCallTargets(IEnumerable<TargetFramework> targetFrameworks, Func<string, string> getDllPath, AbsolutePath outputPath, string version, AbsolutePath dependabotPath) 
        {
            Logger.Debug("Generating CallTarget definitions file ...");

            var callTargets = new Dictionary<CallTargetDefinitionSource, TargetFrameworks>();
            foreach(var tfm in targetFrameworks)
            {
                var tfmCategory = GetCategory(tfm);
                var dllPath = getDllPath(tfm);
                // We check if the assembly file exists.
                if (!File.Exists(dllPath))
                {
                    throw new FileNotFoundException($"Error extracting types for CallTarget generation. Assembly file was not found. Path: {dllPath}", dllPath);
                }

                // Open dll to extract all AspectsClass attributes.
                using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(dllPath);

                RetrieveCallTargets(callTargets, assembly, tfmCategory);

                RetrieveAdoNetCallTargets(callTargets, assembly, tfmCategory);
            }


            GenerateNativeFile(callTargets, outputPath, version);
            GenerateJsonFile(callTargets, dependabotPath);
        }

        internal static void RetrieveCallTargets(Dictionary<CallTargetDefinitionSource, TargetFrameworks> callTargets, AssemblyDefinition assembly, TargetFrameworks tfmCategory)
        {
            foreach (var type in EnumTypes(assembly.MainModule.Types))
            {
                foreach (var attribute in type.CustomAttributes.Where(IsTargetAttribute))
                {
                    foreach (var definition in GetCallTargetDefinition(type, attribute))
                    {
                        callTargets.TryGetValue(definition, out var tfms);
                        callTargets[definition] = (tfms | tfmCategory);
                    }
                }
            }

            static bool IsTargetAttribute(Mono.Cecil.CustomAttribute attribute)
            {
                return attribute.AttributeType.FullName.StartsWith("Datadog.Trace.ClrProfiler.InstrumentMethodAttribute");
            }

            static List<CallTargetDefinitionSource> GetCallTargetDefinition(TypeDefinition type, CustomAttribute attribute)
            {
                var res = new List<CallTargetDefinitionSource>();
                string assemblyName = null;
                string[] assemblyNames = null;
                string integrationName = null;
                string typeName = null;
                string[] typeNames = null;
                string methodName = null;
                string returnTypeName = null;
                string minimumVersion = null;
                string maximumVersion = null;
                string[] parameterTypeNames = null;
                string callTargetType = null;
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
                            callTargetType = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(InstrumentAttributeProperties.CallTargetIntegrationKind):
                            integrationKind = namedArgument.Argument.Value as int?;
                            break;
                        case nameof(InstrumentAttributeProperties.InstrumentationCategory):
                            instrumentationCategory = (InstrumentationCategory)(namedArgument.Argument.Value as uint?).GetValueOrDefault();
                            break;
                        default:
                            throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid property: '{namedArgument.Name}'");
                    }
                }

                (ushort Major, ushort Minor, ushort Patch) minVersion = default;
                if (!TryGetVersion(minimumVersion, ushort.MinValue, out minVersion))
                {
                    throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid value for minimum: '{minimumVersion}'");
                }

                (ushort Major, ushort Minor, ushort Patch) maxVersion = default;
                if (!TryGetVersion(maximumVersion, ushort.MaxValue, out maxVersion))
                {
                    throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid value for maximum: '{maximumVersion}'");
                }

                if (assemblyNames is null or { Length: 0 } && assemblyName is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid value for assemblyNames: '{assemblyNames}'");
                }

                if (typeNames is null or { Length: 0 } && typeName is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid value for typeNames: '{typeNames}'");
                }

                if (integrationName is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid value for integrationName: '{integrationName}'");
                }

                if (methodName is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid value for methodName: '{methodName}'");
                }

                if (returnTypeName is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid value for returnTypeName: '{returnTypeName}'");
                }

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
        }

        internal static void RetrieveAdoNetCallTargets(Dictionary<CallTargetDefinitionSource, TargetFrameworks> callTargets, AssemblyDefinition assembly, TargetFrameworks tfmCategory)
        {
            var adoNetClientInstruments = new List<AssemblyCallTargetDefinitionSource>();
            foreach (var attribute in assembly.MainModule.Assembly.CustomAttributes.Where(IsTargetClientInstrumentAttribute))
            {
                adoNetClientInstruments.AddRange(GetAdoNetClientInstruments(attribute));
            }

            var adoNetSignatures = new List<AdoNetSignature>();
            foreach (var type in EnumTypes(assembly.MainModule.Types))
            {
                foreach (var attribute in type.CustomAttributes.Where(IsTargetSignatureAttribute))
                {
                    adoNetSignatures.Add(GetAdoNetSignature(type, attribute));
                }
            }

            var merged = MergeAdoNetAttributes(adoNetClientInstruments, adoNetSignatures);

            foreach (var definition in merged)
            {
                callTargets.TryGetValue(definition, out var tfms);
                callTargets[definition] = (tfms | tfmCategory);
            }

            static bool IsTargetClientInstrumentAttribute(Mono.Cecil.CustomAttribute attribute)
            {
                return attribute.AttributeType.FullName.Equals("Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute");
            }

            static bool IsTargetSignatureAttribute(Mono.Cecil.CustomAttribute attribute)
            {
                return attribute.AttributeType.FullName.Equals("Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute/AdoNetTargetSignatureAttribute");
            }

            static AdoNetSignature GetAdoNetSignature(TypeDefinition type, CustomAttribute attribute)
            {
                string methodName = null;
                string returnTypeName = null;
                string[] parameterTypeNames = null;
                int? integrationKind = null;
                int? returnType = null;
                string callTargetType = null;

                foreach (var namedArgument in attribute.Properties)
                {
                    switch (namedArgument.Name)
                    {
                        case nameof(AdoNetSignatureAttributeProperties.MethodName):
                            methodName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(AdoNetSignatureAttributeProperties.ReturnTypeName):
                            returnTypeName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(AdoNetSignatureAttributeProperties.ParameterTypeNames):
                            parameterTypeNames = GetStringArray(namedArgument.Argument.Value);
                            break;
                        case nameof(AdoNetSignatureAttributeProperties.CallTargetType):
                            callTargetType = ((TypeDefinition)namedArgument.Argument.Value).FullName;
                            break;
                        case nameof(AdoNetSignatureAttributeProperties.CallTargetIntegrationKind):
                            integrationKind = namedArgument.Argument.Value as int?;
                            break;
                        case nameof(AdoNetSignatureAttributeProperties.ReturnType):
                            returnType = namedArgument.Argument.Value as int?;
                            break;
                        default:
                            throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid property: '{namedArgument.Name}'");
                    }
                }

                if (methodName is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid value for methodName: '{methodName}'");
                }

                if (callTargetType is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid value for callTargetType: '{callTargetType}'");
                }

                if (returnType is 0 && returnTypeName is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{type}' has invalid value for returnType: '{returnType}'");
                }

                return new AdoNetSignature(
                    className: type.FullName,
                    targetMethodName: methodName!,
                    targetReturnType: returnTypeName,
                    targetParameterTypes: parameterTypeNames ?? Array.Empty<string>(),
                    instrumentationTypeName: callTargetType!.ToString(),
                    callTargetIntegrationKind: integrationKind ?? 0,
                    returnType: returnType ?? 0);
            }

            static List<AssemblyCallTargetDefinitionSource> GetAdoNetClientInstruments(CustomAttribute attribute)
            {
                var res = new List<AssemblyCallTargetDefinitionSource>();
                string assemblyName = null;
                string integrationName = null;
                string typeName = null;
                string minimumVersion = null;
                string maximumVersion = null;
                string dataReaderTypeName = null;
                string dataReaderTaskTypeName = null;
                string[] signatureAttributeTypes = null;

                foreach (var namedArgument in attribute.Properties)
                {
                    switch (namedArgument.Name)
                    {
                        case nameof(AdoNetInstrumentAttributeProperties.AssemblyName):
                            assemblyName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(AdoNetInstrumentAttributeProperties.TypeName):
                            typeName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(AdoNetInstrumentAttributeProperties.MinimumVersion):
                            minimumVersion = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(AdoNetInstrumentAttributeProperties.MaximumVersion):
                            maximumVersion = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(AdoNetInstrumentAttributeProperties.IntegrationName):
                            integrationName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(AdoNetInstrumentAttributeProperties.DataReaderType):
                            dataReaderTypeName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(AdoNetInstrumentAttributeProperties.DataReaderTaskType):
                            dataReaderTaskTypeName = namedArgument.Argument.Value?.ToString();
                            break;
                        case nameof(AdoNetInstrumentAttributeProperties.TargetMethodAttributes):
                            signatureAttributeTypes = GetStringArray(namedArgument.Argument.Value);
                            break;
                        default:
                            throw new InvalidOperationException($"Error: Assembly ADO Attribute Integration '{attribute}' has invalid property: '{namedArgument.Name}'");
                    }
                }

                (ushort Major, ushort Minor, ushort Patch) minVersion = default;
                if (!TryGetVersion(minimumVersion, ushort.MinValue, out minVersion))
                {
                    throw new InvalidOperationException($"Error: Assembly ADO Attribute Integration '{attribute}' has invalid value for minimum: '{minimumVersion}'");
                }

                (ushort Major, ushort Minor, ushort Patch) maxVersion = default;
                if (!TryGetVersion(maximumVersion, ushort.MaxValue, out maxVersion))
                {
                    throw new InvalidOperationException($"Error: Assembly ADO Attribute Integration '{attribute}' has invalid value for maximum: '{maximumVersion}'");
                }

                if (string.IsNullOrEmpty(assemblyName))
                {
                    throw new InvalidOperationException($"Error: Integration type  '{attribute}' has invalid value for assemblyName: '{assemblyName}'");
                }

                if (string.IsNullOrEmpty(typeName))
                {
                    throw new InvalidOperationException($"Error: Integration type  '{attribute}' has invalid value for typeName: '{typeName}'");
                }

                if (integrationName is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{attribute}' has invalid value for integrationName: '{integrationName}'");
                }

                if (dataReaderTypeName is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{attribute}' has invalid value for dataReaderTypeName: '{dataReaderTypeName}'");
                }

                if (dataReaderTaskTypeName is null)
                {
                    throw new InvalidOperationException($"Error: Integration type  '{attribute}' has invalid value for dataReaderTaskTypeName: '{dataReaderTaskTypeName}'");
                }

                if (signatureAttributeTypes is null or { Length: 0 })
                {
                    throw new InvalidOperationException($"Error: Integration type  '{attribute}' has invalid value for signatureAttributeTypes: '{signatureAttributeTypes}'");
                }

                foreach (var signatureAttributeName in signatureAttributeTypes!)
                {
                    res.Add(
                        new AssemblyCallTargetDefinitionSource(
                            signatureAttributeName: signatureAttributeName,
                            integrationName: integrationName!,
                            assemblyName: assemblyName!,
                            targetTypeName: typeName!,
                            minimumVersion: minVersion,
                            maximumVersion: maxVersion,
                            isAdoNetIntegration: true,
                            instrumentationCategory: InstrumentationCategory.Tracing,
                            dataReaderTypeName,
                            dataReaderTaskTypeName));
                }

                return res;
            }

            static List<CallTargetDefinitionSource> MergeAdoNetAttributes(List<AssemblyCallTargetDefinitionSource> attributes, List<AdoNetSignature> signatures)
            {
                List<CallTargetDefinitionSource> res = new List<CallTargetDefinitionSource>();

                foreach (var attribute in attributes)
                {
                    foreach (var signature in signatures)
                    {
                        if (signature.ClassName == attribute.SignatureAttributeName)
                        {
                            // found it
                            var returnTypeName = signature.ReturnType switch
                            {
                                1 => attribute.DataReaderTypeName,
                                2 => attribute.DataReaderTaskTypeName,
                                _ => signature.TargetReturnType
                            };

                            var callTargetSource =
                                new CallTargetDefinitionSource(
                                    integrationName: attribute.IntegrationName!,
                                    assemblyName: attribute.AssemblyName!,
                                    targetTypeName: attribute.TargetTypeName!,
                                    targetMethodName: signature.TargetMethodName,
                                    targetReturnType: returnTypeName!,
                                    targetParameterTypes: signature.TargetParameterTypes.AsArray(),
                                    minimumVersion: attribute.MinimumVersion,
                                    maximumVersion: attribute.MaximumVersion,
                                    instrumentationTypeName: signature.InstrumentationTypeName,
                                    integrationKind: signature.CallTargetIntegrationKind,
                                    isAdoNetIntegration: true,
                                    instrumentationCategory: InstrumentationCategory.Tracing);

                            res.Add(callTargetSource);
                        }
                    }
                }

                return res;
            }
        }

        static IEnumerable<TypeDefinition> EnumTypes(IEnumerable<TypeDefinition> types)
        {
            foreach (var type in types)
            {
                foreach (var nestedType in EnumTypes(type.NestedTypes))
                {
                    yield return nestedType;
                }

                yield return type;
            }
        }

        static string[] GetStringArray(object value)
        {
            if (value is CustomAttributeArgument[] values)
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

        internal static void GenerateNativeFile(Dictionary<CallTargetDefinitionSource, TargetFrameworks> definitions, AbsolutePath outputPath, string version)
        {
            var sb = new StringBuilder();
            sb.AppendLine("""
                // <copyright company="Datadog">
                // Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
                // This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
                // </copyright>
                // <auto-generated/>
                #include "generated_definitions.h"
                #include "../../../../shared/src/native-src/version.h"

                namespace trace
                {
                int GeneratedDefinitions::InitCallTargets(UINT32 enabledCategories, UINT32 platform)
                {
                std::string version = PROFILER_VERSION;
                std::string name = "Datadog.Trace, Version=" + version + ".0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";
                shared::WSTRING nameW = ToWSTRING(name);
                WCHAR* assemblyName = (WCHAR*) (nameW.c_str());
                """);

            sb.AppendLine();

            // Retrieve all signatures
            var signatureTexts = new HashSet<string>();
            foreach (var definition in definitions)
            {
                signatureTexts.Add(GetSignature(definition.Key));
            }
           
            var signatures = new Dictionary<string, string>();
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
            bool inWin32Section = false;
            sb.AppendLine();
            sb.AppendLine("""
                std::vector<CallTargetDefinition3> callTargets =
                """);
            sb.AppendLine("{");
            int x = 0;
            foreach (var definition in definitions
                                        .OrderBy(static x => x.Key.IntegrationName)
                                        .ThenBy(static x => x.Key.AssemblyName)
                                        .ThenBy(static x => x.Key.TargetTypeName)
                                        .ThenBy(static x => x.Key.TargetMethodName))
            {
                bool win32Only = definition.Value.IsNetFxOnly();
                if (win32Only && !inWin32Section)
                {
                    inWin32Section = true;
                    sb.AppendLine("#if _WIN32");
                }
                else if (!win32Only && inWin32Section)
                {
                    inWin32Section = false;
                    sb.AppendLine("#endif");
                }

                sb.AppendLine(GetCallTarget(definition.Key, signatures[GetSignature(definition.Key)], definition.Value, x++));
            }

            if (inWin32Section)
            {
                inWin32Section = false;
                sb.AppendLine("#endif");
            }

            sb.AppendLine("""
                };
                return profiler->RegisterCallTargetDefinitions((WCHAR*) WStr("Tracing"), callTargets.data(), callTargets.size(), enabledCategories, platform);
                }
                }
                """);


            if (!Directory.Exists(outputPath)) { Directory.CreateDirectory(outputPath); }
            var fileName = outputPath / "generated_calltargets.g.cpp";
            File.WriteAllText(fileName, sb.ToString());

            Logger.Information("CallTarget definitions File saved: {File}", fileName);

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
                return $"sig{index:000}";
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
                return kind switch
                {
                    0 => "CallTargetKind::Default",
                    1 => "CallTargetKind::Derived",
                    2 => "CallTargetKind::Interface",
                    _ => throw new InvalidOperationException($"Invalid call target kind: {kind}" ),
                };
            }
        }

        internal static void GenerateJsonFile(Dictionary<CallTargetDefinitionSource, TargetFrameworks> definitions, AbsolutePath outputPath)
        {
            var orderedDefinitions = definitions
                                        .Keys
                                        .OrderBy(static x => x.IntegrationName)
                                        .ThenBy(static x => x.AssemblyName)
                                        .ThenBy(static x => x.TargetTypeName)
                                        .ThenBy(static x => x.TargetMethodName)
                                        .ToArray();

            var options = new JsonSerializerOptions();
            options.IncludeFields = true;
            options.WriteIndented = true;
            string jsonString = JsonConvert.SerializeObject(orderedDefinitions, Formatting.Indented);

            if (!Directory.Exists(outputPath)) { Directory.CreateDirectory(outputPath); }
            var fileName = outputPath / FileNames.DefinitionsJson;
            File.WriteAllText(fileName, jsonString);

            Logger.Information("CallTarget definitions File saved: {File}", fileName);
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
                TargetParameterTypes = new EquatableArray<string>(targetParameterTypes ?? Array.Empty<string>());
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

        internal record AdoNetSignature
        {
            public AdoNetSignature(string className, string targetMethodName, string targetReturnType, string[] targetParameterTypes, string instrumentationTypeName, int callTargetIntegrationKind, int returnType)
            {
                ClassName = className;
                TargetMethodName = targetMethodName;
                TargetReturnType = targetReturnType;
                TargetParameterTypes = new(targetParameterTypes);
                InstrumentationTypeName = instrumentationTypeName;
                CallTargetIntegrationKind = callTargetIntegrationKind;
                ReturnType = returnType;
            }

            public string ClassName { get; }

            public string TargetMethodName { get; }

            public string TargetReturnType { get; }

            public EquatableArray<string> TargetParameterTypes { get; }

            public string InstrumentationTypeName { get; }

            public int CallTargetIntegrationKind { get; }

            public int ReturnType { get; }
        }

        internal record AssemblyCallTargetDefinitionSource
        {
            public AssemblyCallTargetDefinitionSource(string signatureAttributeName, string integrationName, string assemblyName, string targetTypeName, (ushort Major, ushort Minor, ushort Patch) minimumVersion, (ushort Major, ushort Minor, ushort Patch) maximumVersion, bool isAdoNetIntegration, InstrumentationCategory instrumentationCategory, string dataReaderTypeName, string dataReaderTaskTypeName)
            {
                SignatureAttributeName = signatureAttributeName;
                IntegrationName = integrationName;
                AssemblyName = assemblyName;
                TargetTypeName = targetTypeName;
                MinimumVersion = minimumVersion;
                MaximumVersion = maximumVersion;
                IsAdoNetIntegration = isAdoNetIntegration;
                InstrumentationCategory = instrumentationCategory;
                DataReaderTypeName = dataReaderTypeName;
                DataReaderTaskTypeName = dataReaderTaskTypeName;
            }

            public string SignatureAttributeName { get; }

            public string IntegrationName { get; }

            public string AssemblyName { get; }

            public string TargetTypeName { get; }

            public (ushort Major, ushort Minor, ushort Patch) MinimumVersion { get; }

            public (ushort Major, ushort Minor, ushort Patch) MaximumVersion { get; }

            public bool IsAdoNetIntegration { get; }

            public InstrumentationCategory InstrumentationCategory { get; }

            public string DataReaderTypeName { get; }

            public string DataReaderTaskTypeName { get; }
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

        private static class AdoNetSignatureAttributeProperties
        {
            public const string MethodName = nameof(MethodName);
            public const string ReturnTypeName = nameof(ReturnTypeName);
            public const string ParameterTypeNames = nameof(ParameterTypeNames);
            public const string CallTargetType = nameof(CallTargetType);
            public const string CallTargetIntegrationKind = nameof(CallTargetIntegrationKind);
            public const string ReturnType = nameof(ReturnType);
        }

        private static class AdoNetInstrumentAttributeProperties
        {
            public const string AssemblyName = nameof(AssemblyName);
            public const string TypeName = nameof(TypeName);
            public const string MinimumVersion = nameof(MinimumVersion);
            public const string MaximumVersion = nameof(MaximumVersion);
            public const string IntegrationName = nameof(IntegrationName);
            public const string DataReaderType = nameof(DataReaderType);
            public const string DataReaderTaskType = nameof(DataReaderTaskType);
            public const string TargetMethodAttributes = nameof(TargetMethodAttributes);
        }

    }
}
