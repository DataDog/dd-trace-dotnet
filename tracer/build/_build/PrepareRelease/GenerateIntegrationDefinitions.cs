// <copyright file="GenerateIntegrationDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PrepareRelease
{
    public static class GenerateIntegrationDefinitions
    {
        const string InstrumentMethodAttributeName = "Datadog.Trace.ClrProfiler.InstrumentMethodAttribute";
        const string InterceptMethodAttributeName = "Datadog.Trace.ClrProfiler.InterceptMethodAttribute";

        public static void Run(IntegrationGroups integrations, params string[] outputDirectories)
        {
            Console.WriteLine("Updating the integrations definitions");

            // Create json serializer
            var serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };

            var json = JsonConvert.SerializeObject(integrations.CallSite, serializerSettings);

            foreach (var outputDirectory in outputDirectories)
            {
                var filename = Path.Combine(outputDirectory, "integrations.json");
                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                Console.WriteLine($"Writing {filename}...");
                File.WriteAllText(filename, json, utf8NoBom);

                // CallTarget
                var calltargetPath = Path.Combine(outputDirectory, "src", "Datadog.Trace", "ClrProfiler", "InstrumentationDefinitions.Generated.cs");
                Console.WriteLine($"Writing {calltargetPath}...");
                using var fs = new FileStream(calltargetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var sw = new StreamWriter(fs, utf8NoBom);
                WriteCallTargetDefinitionFile(sw, integrations.CallTarget);
            }
        }

        public static IntegrationGroups GetAllIntegrations(ICollection<string> assemblyPaths)
        {
            var callTargetIntegrations = Enumerable.Empty<CallTargetDefinitionSource>();
            var callSiteIntegrations = Enumerable.Empty<Integration>();

            foreach (var path in assemblyPaths)
            {
                Console.WriteLine($"Reading integrations for {path}...");
                var assemblyLoadContext = new CustomAssemblyLoadContext(Path.GetDirectoryName(path));
                var assembly = assemblyLoadContext.LoadFromAssemblyPath(path);

                callTargetIntegrations = callTargetIntegrations.Concat(GetCallTargetIntegrations(new[] { assembly }));
                callSiteIntegrations = callSiteIntegrations.Concat(GetCallSiteIntegrations(new[] { assembly }));

                assemblyLoadContext.Unload();
            }

            // remove duplicates
            callSiteIntegrations = callSiteIntegrations
                                  .GroupBy(x => x.Name)
                                  .Select(x => new Integration()
                                  {
                                      Name = x.Key,
                                      MethodReplacements = x
                                                           .SelectMany(y => y.MethodReplacements)
                                                           .Distinct()
                                                           .ToArray(),
                                  });

            var integrations = new IntegrationGroups()
            {
                CallSite = callSiteIntegrations.ToList(),
                CallTarget = callTargetIntegrations.ToList(),
            };

            return integrations;
        }

        static IEnumerable<CallTargetDefinitionSource> GetCallTargetIntegrations(ICollection<Assembly> assemblies)
        {
            var assemblyInstrumentMethodAttributes = from assembly in assemblies
                                                     let attributes = assembly.GetCustomAttributes(inherit: false)
                                                                              .Where(a => InheritsFrom(a.GetType(), InstrumentMethodAttributeName))
                                                                              .ToList()
                                                     from attribute in attributes
                                                     let callTargetType = GetPropertyValue<Type>(attribute, "CallTargetType")
                                                                       ?? throw new NullReferenceException($"The usage of InstrumentMethodAttribute[Type={GetPropertyValue<string>(attribute, "TypeName")}, Method={GetPropertyValue<Type>(attribute, "MethodName")}] in assembly scope must define the CallTargetType property.")
                                                     select (callTargetType, attribute);

            // Extract all InstrumentMethodAttribute from the classes
            var classesInstrumentMethodAttributes = from assembly in assemblies
                                                    from wrapperType in GetLoadableTypes(assembly)
                                                    let attributes = wrapperType.GetCustomAttributes(inherit: false)
                                                                                .Where(a => InheritsFrom(a.GetType(), InstrumentMethodAttributeName))
                                                                                .Select(a => (wrapperType, a))
                                                                                .ToList()
                                                    from attribute in attributes
                                                    select attribute;

            // combine all InstrumentMethodAttributes
            // and create objects that will generate correct JSON schema
            var callTargetIntegrations = from attributePair in assemblyInstrumentMethodAttributes.Concat(classesInstrumentMethodAttributes)
                                         let callTargetType = attributePair.Item1
                                         let attribute = attributePair.Item2
                                         let integrationName = GetPropertyValue<string>(attribute, "IntegrationName")
                                         let assembly = callTargetType.Assembly
                                         let wrapperType = callTargetType
                                         from assemblyNames in GetPropertyValue<string[]>(attribute, "AssemblyNames")
                                         let versionRange = GetPropertyValue<object>(attribute, "VersionRange")
                                         orderby integrationName, assemblyNames, GetPropertyValue<string>(attribute, "TypeName"), GetPropertyValue<string>(attribute, "MethodName")
                                         select new CallTargetDefinitionSource
                                         {
                                             IntegrationName = integrationName,
                                             TargetAssembly = assemblyNames,
                                             TargetType = GetPropertyValue<string>(attribute, "TypeName"),
                                             TargetMethod = GetPropertyValue<string>(attribute, "MethodName"),
                                             TargetSignatureTypes = new string[] { GetPropertyValue<string>(attribute, "ReturnTypeName") }
                                                                   .Concat(GetPropertyValue<string[]>(attribute, "ParameterTypeNames") ?? Enumerable.Empty<string>())
                                                                   .ToArray(),
                                             TargetMinimumMajor = GetPropertyValue<ushort>(versionRange, "MinimumMajor"),
                                             TargetMinimumMinor = GetPropertyValue<ushort>(versionRange, "MinimumMinor"),
                                             TargetMinimumPatch = GetPropertyValue<ushort>(versionRange, "MinimumPatch"),
                                             TargetMaximumMajor = GetPropertyValue<ushort>(versionRange, "MaximumMajor"),
                                             TargetMaximumMinor = GetPropertyValue<ushort>(versionRange, "MaximumMinor"),
                                             TargetMaximumPatch = GetPropertyValue<ushort>(versionRange, "MaximumPatch"),
                                             WrapperAssembly = assembly.FullName,
                                             WrapperType = wrapperType.FullName
                                         };
            var cTargetInt = callTargetIntegrations.ToList();
            return callTargetIntegrations.ToList();
        }

        static void WriteCallTargetDefinitionFile(StreamWriter swriter, IEnumerable<CallTargetDefinitionSource> callTargetIntegrations)
        {
            swriter.WriteLine("// <copyright file=\"InstrumentationDefinitions.Generated.cs\" company=\"Datadog\">");
            swriter.WriteLine("// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.");
            swriter.WriteLine("// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.");
            swriter.WriteLine("// </copyright>");
            swriter.WriteLine();
            swriter.WriteLine("namespace Datadog.Trace.ClrProfiler");
            swriter.WriteLine("{");
            swriter.WriteLine("    internal static partial class InstrumentationDefinitions");
            swriter.WriteLine("    {");
            swriter.WriteLine("        private static NativeCallTargetDefinition[] GetDefinitionsArray()");
            swriter.WriteLine("        {");
            swriter.WriteLine("            return new NativeCallTargetDefinition[]");
            swriter.WriteLine("            {");
            foreach (var integrationGroup in callTargetIntegrations.Distinct().GroupBy(i => i.IntegrationName))
            {
                swriter.WriteLine($"                // {integrationGroup.Key}");

                foreach (var integration in integrationGroup)
                {
                    swriter.Write($"                new(");
                    swriter.Write($"\"{integration.TargetAssembly}\", ");
                    swriter.Write($"\"{integration.TargetType}\", ");
                    swriter.Write($"\"{integration.TargetMethod}\", ");

                    swriter.Write($" new[] {{ ");
                    for (var s = 0; s < integration.TargetSignatureTypes.Length; s++)
                    {
                        if (s == integration.TargetSignatureTypes.Length - 1)
                        {
                            swriter.Write($"\"{integration.TargetSignatureTypes[s]}\"");
                        }
                        else
                        {
                            swriter.Write($"\"{integration.TargetSignatureTypes[s]}\", ");
                        }
                    }

                    swriter.Write(" }, ");

                    swriter.Write($"{integration.TargetMinimumMajor}, ");
                    swriter.Write($"{integration.TargetMinimumMinor}, ");
                    swriter.Write($"{integration.TargetMinimumPatch}, ");
                    swriter.Write($"{integration.TargetMaximumMajor}, ");
                    swriter.Write($"{integration.TargetMaximumMinor}, ");
                    swriter.Write($"{integration.TargetMaximumPatch}, ");
                    swriter.Write($"assemblyFullName, ");
                    swriter.Write($"\"{integration.WrapperType}\"");
                    swriter.WriteLine($"),");
                }
                swriter.WriteLine();
            }
            swriter.WriteLine("            };");
            swriter.WriteLine("        }");
            swriter.WriteLine("    }");
            swriter.WriteLine("}");
        }

        static IEnumerable<Integration> GetCallSiteIntegrations(ICollection<Assembly> assemblies)
        {
            // find all methods in Datadog.Trace.dll with [InterceptMethod]
            // and create objects that will generate correct JSON schema
            var integrations = from assembly in assemblies
                               from wrapperType in GetLoadableTypes(assembly)
                               from wrapperMethod in wrapperType.GetRuntimeMethods()
                               let attributes = wrapperMethod.GetCustomAttributes(inherit: false)
                                                             .Where(a => InheritsFrom(a.GetType(), InterceptMethodAttributeName))
                                                             .ToList()
                               where attributes.Any()
                               from attribute in attributes
                               let integrationName = GetPropertyValue<string>(attribute, "Integration") ?? GetIntegrationName(wrapperType)
                               orderby integrationName
                               group new
                               {
                                   assembly,
                                   wrapperType,
                                   wrapperMethod,
                                   attribute
                               }
                                   by integrationName into g
                               select new Integration
                               {
                                   Name = g.Key,
                                   MethodReplacements = (from item in g
                                                         let version = GetPropertyValue<object>(item.attribute, "TargetVersionRange")
                                                         let methodReplacementAction = GetPropertyValue<object>(item.attribute, "MethodReplacementAction").ToString()
                                                         from targetAssembly in GetPropertyValue<string[]>(item.attribute, "TargetAssemblies")
                                                         select new Integration.MethodReplacement
                                                         {
                                                             Caller = new Integration.CallerDetail
                                                             {
                                                                 Assembly = GetPropertyValue<string>(item.attribute, "CallerAssembly"),
                                                                 Type = GetPropertyValue<string>(item.attribute, "CallerType"),
                                                                 Method = GetPropertyValue<string>(item.attribute, "CallerMethod"),
                                                             },
                                                             Target = new Integration.TargetDetail
                                                             {
                                                                 Assembly = targetAssembly,
                                                                 Type = GetPropertyValue<string>(item.attribute, "TargetType"),
                                                                 Method = GetPropertyValue<string>(item.attribute, "TargetMethod") ?? item.wrapperMethod.Name,
                                                                 Signature = GetPropertyValue<string>(item.attribute, "TargetSignature"),
                                                                 SignatureTypes = GetPropertyValue<string[]>(item.attribute, "TargetSignatureTypes"),
                                                                 MinimumMajor = GetPropertyValue<ushort>(version, "MinimumMajor"),
                                                                 MinimumMinor = GetPropertyValue<ushort>(version, "MinimumMinor"),
                                                                 MinimumPatch = GetPropertyValue<ushort>(version, "MinimumPatch"),
                                                                 MaximumMajor = GetPropertyValue<ushort>(version, "MaximumMajor"),
                                                                 MaximumMinor = GetPropertyValue<ushort>(version, "MaximumMinor"),
                                                                 MaximumPatch = GetPropertyValue<ushort>(version, "MaximumPatch"),
                                                             },
                                                             Wrapper = new Integration.WrapperDetail
                                                             {
                                                                 Assembly = item.assembly.FullName,
                                                                 Type = item.wrapperType.FullName,
                                                                 Method = item.wrapperMethod.Name,
                                                                 Signature = GetMethodSignature(item.wrapperMethod, item.attribute, methodReplacementAction),
                                                                 Action = methodReplacementAction
                                                             }
                                                         }).ToArray()
                               };

            return integrations.ToList();
        }

        private static bool InheritsFrom(Type type, string baseType)
        {
            if (type.FullName == baseType)
            {
                return true;
            }

            if (type.BaseType is null)
            {
                return false;
            }

            return InheritsFrom(type.BaseType, baseType);
        }

        private static T GetPropertyValue<T>(object attribute, string propertyName)
        {
            var type = attribute.GetType();
            var getValue = type.GetProperty(propertyName)?.GetGetMethod();
            if (getValue is null || !getValue.ReturnType.IsAssignableTo(typeof(T)))
            {
                throw new ArgumentException($"Provided type {type} does not contain a property {propertyName} with a getter that returns {typeof(T)}");
            }

            return (T)getValue.Invoke(attribute, Array.Empty<object>());
        }

        private static string GetIntegrationName(Type wrapperType)
        {
            const string integrations = "Integration";
            var typeName = wrapperType.Name;

            if (typeName.EndsWith(integrations, StringComparison.OrdinalIgnoreCase))
            {
                return typeName.Substring(startIndex: 0, length: typeName.Length - integrations.Length);
            }

            return typeName;
        }

        private static string GetMethodSignature(MethodInfo method, object attribute, string methodReplacementAction)
        {
            var returnType = method.ReturnType;
            var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();

            var requiredParameterTypes = new[] { typeof(int), typeof(int), typeof(long) };
            var lastParameterTypes = parameters.Skip(parameters.Length - requiredParameterTypes.Length);

            if (methodReplacementAction == "ReplaceTargetMethod")
            {
                if (!lastParameterTypes.SequenceEqual(requiredParameterTypes))
                {
                    throw new Exception(
                        $"Method {method.DeclaringType.FullName}.{method.Name}() does not meet parameter requirements. " +
                        "Wrapper methods must have at least 3 parameters and the last 3 must be of types Int32 (opCode), Int32 (mdToken), and Int64 (moduleVersionPtr).");
                }
            }
            else if (methodReplacementAction == "InsertFirst")
            {
                var callerAssembly = GetPropertyValue<string>(attribute, "CallerAssembly");
                var callerType = GetPropertyValue<string>(attribute, "CallerType");
                var callerMethod = GetPropertyValue<string>(attribute, "CallerMethod");
                if (callerAssembly == null || callerType == null || callerMethod == null)
                {
                    throw new Exception(
                        $"Method {method.DeclaringType.FullName}.{method.Name}() does not meet InterceptMethodAttribute requirements. " +
                        "Currently, InsertFirst methods must have CallerAssembly, CallerType, and CallerMethod defined. " +
                        $"Current values: CallerAssembly=\"{callerAssembly}\", CallerType=\"{callerType}\", CallerMethod=\"{callerMethod}\"");
                }
                else if (parameters.Any())
                {
                    throw new Exception(
                        $"Method {method.DeclaringType.FullName}.{method.Name}() does not meet parameter requirements. " +
                        "Currently, InsertFirst methods must have zero parameters.");
                }
                else if (returnType != typeof(void))
                {
                    throw new Exception(
                        $"Method {method.DeclaringType.FullName}.{method.Name}() does not meet return type requirements. " +
                        "Currently, InsertFirst methods must have a void return type.");
                }
            }

            var signatureHelper = SignatureHelper.GetMethodSigHelper(method.CallingConvention, returnType);
            signatureHelper.AddArguments(parameters, requiredCustomModifiers: null, optionalCustomModifiers: null);
            var signatureBytes = signatureHelper.GetSignature();

            if (method.IsGenericMethod)
            {
                // if method is generic, fix first byte (calling convention)
                // and insert a second byte with generic parameter count
                const byte IMAGE_CEE_CS_CALLCONV_GENERIC = 0x10;
                var genericArguments = method.GetGenericArguments();

                var newSignatureBytes = new byte[signatureBytes.Length + 1];
                newSignatureBytes[0] = (byte)(signatureBytes[0] | IMAGE_CEE_CS_CALLCONV_GENERIC);
                newSignatureBytes[1] = (byte)genericArguments.Length;
                Array.Copy(signatureBytes, 1, newSignatureBytes, 2, signatureBytes.Length - 1);

                signatureBytes = newSignatureBytes;
            }

            return string.Join(" ", signatureBytes.Select(b => b.ToString("X2")));
        }

        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                // Ignore types that cannot be loaded. In particular, TracingHttpModule inherits from
                // IHttpModule, which is not available to the nuke builds because they run on net5.0.
                return e.Types.Where(t => t != null);
            }
        }

        class CustomAssemblyLoadContext : AssemblyLoadContext
        {
            readonly string _assemblyLoadPath;

            public CustomAssemblyLoadContext(string assemblyLoadPath)
                : base("IntegrationsJsonLoadContext", true)
            {
                _assemblyLoadPath = assemblyLoadPath;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                var assemblyPath = Path.Combine(_assemblyLoadPath, $"{assemblyName.Name}.dll");
                if (File.Exists(assemblyPath))
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }

                return null;
            }

        }
    }
}
