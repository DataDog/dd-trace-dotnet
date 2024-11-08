using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Nuke.Common.IO;

namespace CodeGenerators
{
    internal static class CallSitesGenerator
    {
        private const string NullLiteral = "null";

        public static void GenerateCallSites(IEnumerable<TargetFramework> targetFrameworks, Func<string, string> getDllPath, AbsolutePath outputPath) 
        {
            Serilog.Log.Debug("Generating CallSite definitions file ...");

            Dictionary<string, AspectClass> aspectClasses = new Dictionary<string, AspectClass>();
            foreach(var tfm in targetFrameworks)
            {
                var dllPath = getDllPath(tfm);
                RetrieveCallSites(aspectClasses, dllPath, tfm);
            }

            GenerateFile(aspectClasses, outputPath);
        }

        internal static void RetrieveCallSites(Dictionary<string, AspectClass> aspectClasses, string dllPath, TargetFramework tfm)
        {
            // We check if the assembly file exists.
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"Error extracting types for CallSite generation. Assembly file was not found. Path: {dllPath}", dllPath);
            }

            var tfmCategory = GetCategory(tfm);

            // Open dll to extract all AspectsClass attributes.
            using var asmDefinition = AssemblyDefinition.ReadAssembly(dllPath);

            foreach (var type in asmDefinition.MainModule.Types)
            {
                var aspectClassAttribute = type.CustomAttributes.FirstOrDefault(IsAspectClass);
                if (aspectClassAttribute is null)
                {
                    continue;
                }

                var aspectClassLine = $"{GetAspectLine(aspectClassAttribute, out var category)} {type.FullName}";
                if (!aspectClasses.TryGetValue(aspectClassLine, out var aspectClass))
                {
                    aspectClass = new AspectClass();
                    aspectClass.Categories |= category;
                    aspectClasses[aspectClassLine] = aspectClass;
                }

                // Retrieve aspects
                foreach(var method in type.Methods)
                {
                    foreach(var aspectAttribute in method.CustomAttributes.Where(IsAspect))
                    {
                        var aspectLine = $"{GetAspectLine(aspectAttribute, out _)} {GetMethodName(method)}";
                        if (!aspectClass.Aspects.TryGetValue(aspectLine, out var aspect))
                        {
                            aspect = new Aspect();
                        }

                        aspect.Tfms |= tfmCategory;
                        aspectClass.Aspects[aspectLine] = aspect;
                    }
                }
            }

            static string GetMethodName(MethodDefinition method)
            {
                var fullName = method.FullName;
                var methodNameStart = fullName.IndexOf("::");
                if (methodNameStart < 0)
                {
                    throw new InvalidOperationException("Could not find '::' in method name " + fullName);
                }

                return fullName.Substring(methodNameStart + 2).Replace("<T>", "<!!0>").Replace("&", "");
            }

            static bool IsAspectClass(Mono.Cecil.CustomAttribute attribute)
            {
                return attribute.AttributeType.FullName.StartsWith("Datadog.Trace.Iast.Dataflow.AspectClass");
            }

            static bool IsAspect(Mono.Cecil.CustomAttribute attribute)
            {
                return attribute.AttributeType.FullName.StartsWith("Datadog.Trace.Iast.Dataflow.Aspect");
            }

            static string GetAspectLine(Mono.Cecil.CustomAttribute data, out InstrumentationCategory category)
            {
                category = InstrumentationCategory.Iast;
                var arguments = data.ConstructorArguments.Select(GetArgument).ToList();
                var name = data.AttributeType.Name;
                var version = string.Empty;

                if (name.EndsWith("FromVersionAttribute"))
                {
                    // Aspect with version limitation
                    name = name.Replace("FromVersionAttribute", "Attribute");
                    version = ";V" + arguments[0].Trim('"');
                    arguments.RemoveAt(0);
                }

                if (name == "AspectClassAttribute")
                {
                    if (arguments.Count == 2)
                    {
                        category = (InstrumentationCategory)Enum.Parse(typeof(InstrumentationCategory), arguments[1]);
                        return $"[AspectClass({arguments[0]},[None],Propagation,[]){version}]";
                    }
                    else if (arguments.Count == 3)
                    {
                        return $"[AspectClass({arguments[0]},[None],{arguments[1]},{Check(arguments[2])}){version}]";
                    }
                    else if (arguments.Count == 4)
                    {
                        category = (InstrumentationCategory)Enum.Parse(typeof(InstrumentationCategory), arguments[1]);
                        return $"[AspectClass({arguments[0]},[None],{arguments[2]},{Check(arguments[3])}){version}]";
                    }
                    else if (arguments.Count == 5)
                    {
                        category = (InstrumentationCategory)Enum.Parse(typeof(InstrumentationCategory), arguments[2]);
                        return $"[AspectClass({arguments[0]},{arguments[1]},{arguments[3]},{Check(arguments[4])}){version}]";
                    }

                    throw new ArgumentException($"Could not find AspectClassAttribute overload with {arguments.Count} parameters");
                }

                return name switch
                {
                    // AspectAttribute(string targetMethod, string targetType, int[] paramShift, bool[] boxParam, AspectFilter[] filters, AspectType aspectType = AspectType.Propagation, VulnerabilityType[] vulnerabilityTypes)
                    "AspectCtorReplaceAttribute" => arguments.Count switch
                    {
                        // AspectCtorReplaceAttribute(string targetMethod)
                        1 => $"[AspectCtorReplace({arguments[0]},\"\",[0],[False],[None],Default,[]){version}]",
                        // AspectCtorReplaceAttribute(string targetMethod, params AspectFilter[] filters)
                        2 => $"[AspectCtorReplace({arguments[0]},\"\",[0],[False],{Check(arguments[1])},Default,[]){version}]",
                        // AspectCtorReplaceAttribute(string targetMethod, AspectType aspectType = AspectType.Default, params VulnerabilityType[] vulnerabilityTypes)
                        3 => $"[AspectCtorReplace({arguments[0]},\"\",[0],[False],[None],{arguments[1]},{Check(arguments[2])}){version}]",
                        // AspectCtorReplaceAttribute(string targetMethod, AspectFilter[] filters, AspectType aspectType = AspectType.Default, params VulnerabilityType[] vulnerabilityTypes)
                        4 => $"[AspectCtorReplace({arguments[0]},\"\",[0],[False],[{arguments[1]}],{arguments[2]},{Check(arguments[3])}){version}]",
                        _ => throw new ArgumentException($"Could not find AspectCtorReplaceAttribute overload with {arguments.Count} parameters")
                    },
                    "AspectMethodReplaceAttribute" => arguments.Count switch
                    {
                        // AspectMethodReplaceAttribute(string targetMethod)
                        1 => $"[AspectMethodReplace({arguments[0]},\"\",[0],[False],[None],Default,[]){version}]",
                        // AspectMethodReplaceAttribute(string targetMethod, params AspectFilter[] filters)
                        2 => $"[AspectMethodReplace({arguments[0]},\"\",[0],[False],{Check(arguments[1], "[None]")},Default,[]){version}]",
                        // AspectMethodReplaceAttribute(string targetMethod, string targetType, params AspectFilter[] filters)
                        3 => arguments[1] switch
                        {
                            { } when arguments[1].StartsWith("[") => $"[AspectMethodReplace({arguments[0]},\"\",{arguments[1]},{arguments[2]},[None],Default,[]){version}]",
                            // AspectMethodReplaceAttribute(string targetMethod, string targetType, params AspectFilter[] filters)
                            _ => $"[AspectMethodReplace({arguments[0]},{arguments[1]},[0],[False],{Check(arguments[2], "[None]")},Default,[]){version}]",
                        },
                        _ => throw new ArgumentException($"Could not find AspectMethodReplaceAttribute overload with {arguments.Count} parameters")
                    },
                    "AspectMethodInsertBeforeAttribute" => arguments.Count switch
                    {
                        // AspectMethodInsertBeforeAttribute(string targetMethod, params int[] paramShift)
                        2 => $"[AspectMethodInsertBefore({arguments[0]},\"\",{MakeSameSize(Check(arguments[1]))},[None],Default,[]){version}]",
                        // AspectMethodInsertBeforeAttribute(string targetMethod, int[] paramShift, bool[] boxParam)
                        3 => $"[AspectMethodInsertBefore({arguments[0]},\"\",[{arguments[1]}],[{arguments[2]}],[None],Default,[]){version}]",
                        _ => throw new ArgumentException($"Could not find AspectMethodInsertBeforeAttribute overload with {arguments.Count} parameters")
                    },
                    "AspectMethodInsertAfterAttribute" => arguments.Count switch
                    {
                        // AspectMethodInsertAfterAttribute(string targetMethod)
                        1 => $"[AspectMethodInsertAfter({arguments[0]},\"\",[0],[False],[None],Default,[]){version}]",
                        // AspectMethodInsertAfterAttribute(string targetMethod, AspectType aspectType, params VulnerabilityType[] vulnerabilityTypes)
                        3 => $"[AspectMethodInsertAfter({arguments[0]},\"\",[0],[False],[None],{arguments[1]},{Check(arguments[2])}){version}]",
                        _ => throw new ArgumentException($"Could not find AspectMethodInsertAfterAttribute overload with {arguments.Count} parameters")
                    },
                    _ => throw new Exception()
                };

                static string Check(string val, string ifEmpty = "[]")
                {
                    return (string.IsNullOrEmpty(val) || val == NullLiteral || val == "[]") ? ifEmpty : val;
                }

                static string MakeSameSize(string val, string ifEmpty = "[0]", string defaultValue = "False")
                {
                    val = Check(val, ifEmpty);
                    int count = val.Count(c => c == ',');
                    string values = string.Empty;
                    for (int x = 0; x < count + 1; x++)
                    {
                        values += defaultValue;
                        if (x < count) { values += ","; }
                    }

                    return $"{val},[{values}]";
                }

                static string GetArgument(Mono.Cecil.CustomAttributeArgument customAttributeArgument)
                {
                    if (customAttributeArgument.Value is null) 
                    {
                        return NullLiteral; 
                    }
                    else if (customAttributeArgument.Type.IsPrimitive)
                    {
                        return customAttributeArgument.Value?.ToString() ?? NullLiteral;
                    }
                    else if (customAttributeArgument.Type.FullName == "System.String")
                    {
                        return $"\"{customAttributeArgument.Value}\"";
                    }
                    else
                    {
                        var type = customAttributeArgument.Type.Resolve();
                        if (customAttributeArgument.Value is CustomAttributeArgument[] argArray)
                        {
                            return $"[{string.Join(",", argArray.Select(GetArgument))}]";
                        }
                        else if (type.IsEnum)
                        {
                            var value = type.Fields.FirstOrDefault(f => customAttributeArgument.Value.Equals(f.Constant));
                            return value.Name;
                        }
                    }

                    return string.Empty;
                }
            }
        }

        internal static void GenerateFile(Dictionary<string, AspectClass> aspectClasses, AbsolutePath outputPath)
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

                std::vector<WCHAR*> g_callSites=
                {
                """);

            foreach (var aspectClass in aspectClasses.OrderBy(static k => k.Key.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(Format(aspectClass.Key + aspectClass.Value.Subfix()));

                foreach (var method in aspectClass.Value.Aspects.OrderBy(static k => k.Key.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine(Format("  " + method.Key + method.Value.Subfix()));
                }
            }

            sb.AppendLine("""
                };
                }
                """);


            if (!Directory.Exists(outputPath)) { Directory.CreateDirectory(outputPath); }
            var fileName = outputPath / "generated_callsites.g.h";
            File.WriteAllText(fileName, sb.ToString());

            Serilog.Log.Information("CallSite definitions File saved: {File}", fileName);

            string Format(string line)
            {
                return $"(WCHAR*)WStr(\"{line.Replace("\"", "\\\"")}\"),";
            }
        }

        internal static TargetFrameworks GetCategory(TargetFramework tfm)
        {
            return (TargetFrameworks)Enum.Parse<TargetFrameworks>(tfm.ToString().ToUpper().Replace('.', '_'));
        }

        internal record AspectClass
        {
            public AspectClass() {}

            public Dictionary<string, Aspect> Aspects = new Dictionary<string, Aspect>();
            public InstrumentationCategory Categories = InstrumentationCategory.Iast;

            public string Subfix()
            {
                return $" {((long)Categories).ToString()}";
            }
        }

        internal record Aspect
        {
            public Aspect() { }

            public TargetFrameworks Tfms = TargetFrameworks.None;

            public string Subfix()
            {
                return $" {((long)Tfms).ToString()}";
            }
        }
    }
}
