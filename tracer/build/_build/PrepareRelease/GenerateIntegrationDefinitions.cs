// <copyright file="GenerateIntegrationDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace PrepareRelease
{
    public static class GenerateIntegrationDefinitions
    {
        const string InstrumentMethodAttributeName = "Datadog.Trace.ClrProfiler.InstrumentMethodAttribute";

        public static void Run(ICollection<CallTargetDefinitionSource> integrations, params string[] outputDirectories)
        {
            Console.WriteLine("Updating the integrations definitions");

            foreach (var outputDirectory in outputDirectories)
            {
                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

                // CallTarget
                var calltargetPath = Path.Combine(outputDirectory, "src", "Datadog.Trace", "ClrProfiler", "InstrumentationDefinitions.Generated.cs");
                Console.WriteLine($"Writing {calltargetPath}...");
                using var fs = new FileStream(calltargetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var sw = new StreamWriter(fs, utf8NoBom);
                WriteCallTargetDefinitionFile(sw, integrations);
            }
        }

        public static List<CallTargetDefinitionSource> GetAllIntegrations(ICollection<string> assemblyPaths)
        {
            var callTargetIntegrations = Enumerable.Empty<CallTargetDefinitionSource>();

            foreach (var path in assemblyPaths)
            {
                Console.WriteLine($"Reading integrations for {path}...");
                var assemblyLoadContext = new CustomAssemblyLoadContext(Path.GetDirectoryName(path));
                var assembly = assemblyLoadContext.LoadFromAssemblyPath(path);

                callTargetIntegrations = callTargetIntegrations.Concat(GetCallTargetIntegrations(new[] { assembly }));

                assemblyLoadContext.Unload();
            }

            return callTargetIntegrations.ToList();
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
                                         let integrationType = GetPropertyValue<object>(attribute, "CallTargetIntegrationType")
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
                                             WrapperType = wrapperType.FullName,
                                             IntegrationType = (IntegrationType)(int)integrationType
                                         };
            return callTargetIntegrations.ToList();
        }

        static void WriteCallTargetDefinitionFile(StreamWriter swriter, IEnumerable<CallTargetDefinitionSource> callTargetIntegrations)
        {
            swriter.WriteLine("// <copyright file=\"InstrumentationDefinitions.Generated.cs\" company=\"Datadog\">");
            swriter.WriteLine("// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.");
            swriter.WriteLine("// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.");
            swriter.WriteLine("// </copyright>");
            swriter.WriteLine();
            swriter.WriteLine("using System.Collections.Generic;");
            swriter.WriteLine();
            swriter.WriteLine("namespace Datadog.Trace.ClrProfiler");
            swriter.WriteLine("{");
            swriter.WriteLine("    internal static partial class InstrumentationDefinitions");
            swriter.WriteLine("    {");

            // Default Integrations
            var normalCallTargetIntegrations = callTargetIntegrations.Where(i => i.IntegrationType == IntegrationType.Default).Distinct().ToList();
            swriter.WriteLine("        private static List<NativeCallTargetDefinition> GetDefinitionsList()");
            swriter.WriteLine("        {");
            swriter.WriteLine("            List<NativeCallTargetDefinition> definitionList = new List<NativeCallTargetDefinition>();");
            swriter.WriteLine();

            foreach (var integrationGroup in normalCallTargetIntegrations.GroupBy(i => i.IntegrationName))
            {
                swriter.WriteLine($"            // {integrationGroup.Key}");

                foreach (var integration in integrationGroup)
                {
                    swriter.Write($"            definitionList.Add(new(");
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
                    swriter.WriteLine($"));");
                }
                swriter.WriteLine();
            }
            swriter.WriteLine("            return definitionList;");
            swriter.WriteLine("        }");
            swriter.WriteLine("");

            // Derived Integrations
            var derivedCallTargetIntegrations = callTargetIntegrations.Where(i => i.IntegrationType == IntegrationType.Derived).Distinct().ToList();
            swriter.WriteLine("        private static List<NativeCallTargetDefinition> GetDerivedDefinitionsList()");
            swriter.WriteLine("        {");
            swriter.WriteLine("            List<NativeCallTargetDefinition> definitionList = new List<NativeCallTargetDefinition>();");
            swriter.WriteLine();

            foreach (var integrationGroup in derivedCallTargetIntegrations.GroupBy(i => i.IntegrationName))
            {
                swriter.WriteLine($"            // {integrationGroup.Key}");

                foreach (var integration in integrationGroup)
                {
                    swriter.Write($"            definitionList.Add(new(");
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
                    swriter.WriteLine($"));");
                }
                swriter.WriteLine();
            }
            swriter.WriteLine("            return definitionList;");
            swriter.WriteLine("        }");

            swriter.WriteLine("    }");
            swriter.WriteLine("}");
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
