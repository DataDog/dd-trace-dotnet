using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Datadog.Trace.ClrProfiler;
using Newtonsoft.Json;

namespace GenerateIntegrationDefinitions
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var integrationsAssembly = typeof(Instrumentation).Assembly;

            // find all methods in Datadog.Trace.ClrProfiler.Managed.dll with [InterceptMethod]
            // and create objects that will generate correct JSON schema
            var integrations = from wrapperType in integrationsAssembly.GetTypes()
                               from wrapperMethod in wrapperType.GetRuntimeMethods()
                               let attributes = wrapperMethod.GetCustomAttributes<InterceptMethodAttribute>(inherit: false)
                               where attributes.Any()
                               from attribute in attributes
                               let integrationName = attribute.Integration ?? GetIntegrationName(wrapperType)
                               orderby integrationName
                               group new
                               {
                                   wrapperType,
                                   wrapperMethod,
                                   attribute
                               }
                                   by integrationName into g
                               select new
                               {
                                   name = g.Key,
                                   method_replacements = from item in g
                                                         select new
                                                         {
                                                             caller = new
                                                             {
                                                                 assembly = item.attribute.CallerAssembly,
                                                                 type = item.attribute.CallerType,
                                                                 method = item.attribute.CallerMethod
                                                             },
                                                             target = new
                                                             {
                                                                 assembly = item.attribute.TargetAssembly,
                                                                 type = item.attribute.TargetType,
                                                                 method = item.attribute.TargetMethod ?? item.wrapperMethod.Name,
                                                                 signature = item.attribute.TargetSignature,
                                                                 minimum_major = item.attribute.TargetVersionRange.MinimumMajor,
                                                                 minimum_minor = item.attribute.TargetVersionRange.MinimumMinor,
                                                                 minimum_patch = item.attribute.TargetVersionRange.MinimumPatch,
                                                                 maximum_major = item.attribute.TargetVersionRange.MaximumMajor,
                                                                 maximum_minor = item.attribute.TargetVersionRange.MaximumMinor,
                                                                 maximum_patch = item.attribute.TargetVersionRange.MaximumPatch
                                                             },
                                                             wrapper = new
                                                             {
                                                                 assembly = integrationsAssembly.FullName,
                                                                 type = item.wrapperType.FullName,
                                                                 method = item.wrapperMethod.Name,
                                                                 signature = GetMethodSignature(item.wrapperMethod)
                                                             }
                                                         }
                               };

            var serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            var json = JsonConvert.SerializeObject(integrations, serializerSettings);
            Console.WriteLine(json);

            string filename = "integrations.json";

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                filename = args[0];
            }

            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            File.WriteAllText(filename, json, utf8NoBom);

            try
            {
                string workingDirectory = Environment.CurrentDirectory;
                string projectDirectory = Directory.GetParent(workingDirectory).Parent.Parent.FullName;
                string libsDirectory = Path.Combine(projectDirectory, "supported-libs");

                var integrationLibs = Directory.GetDirectories(libsDirectory);

                var flatIntegrations = integrations.SelectMany(i => i.method_replacements).ToList();
                var librariesWeInstrument = flatIntegrations.Select(i => i.target.assembly).Distinct().ToList();

                var currentResolvingDirectory = string.Empty;

                Func<object, ResolveEventArgs, Assembly> resolver = (obj, eventArgs) =>
                {
                    Assembly myAssembly;

                    var objExecutingAssemblies = Assembly.GetExecutingAssembly();
                    AssemblyName[] referencedNames = objExecutingAssemblies.GetReferencedAssemblies();

                    var requestedAssembly = eventArgs.Name.Split(',').First();
                    var path = Path.Combine(currentResolvingDirectory, requestedAssembly + ".dll");

                    if (File.Exists(path))
                    {
                        myAssembly = Assembly.ReflectionOnlyLoadFrom(path);
                        return myAssembly;
                    }

                    // Might be a GAC lib? Why isn't the GAC working? Probably because 461 vs 472
                    path = Path.Combine(@"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1", requestedAssembly + ".dll");
                    myAssembly = Assembly.ReflectionOnlyLoadFrom(path);
                    return myAssembly;
                };

                AppDomain currentDomain = AppDomain.CurrentDomain;
                currentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(resolver);

                foreach (var integrationDir in integrationLibs)
                {
                    var name = integrationDir.Split('\\').Last();
                    var versions = Directory.GetDirectories(integrationDir);
                    foreach (var versionDir in versions)
                    {
                        var versionText = versionDir.Split('\\').Last();
                        var version = new Version(versionText);
                        currentResolvingDirectory = versionDir;

                        var potentialFiles =
                            Directory.GetFiles(versionDir)
                                     .Where(fn => librariesWeInstrument.Any(lib => fn.Split('\\').Last() == $"{lib}.dll"))
                                     .Where(fn => fn.EndsWith("dll"));

                        var heatmapName = $"heatmap-{name}-{version}.txt";
                        var heatmapFile = Path.Combine(libsDirectory, heatmapName);

                        if (File.Exists(heatmapFile))
                        {
                            File.Delete(heatmapFile); // start fresh
                        }

                        var heatmap = new List<string>();

                        foreach (var libraryFile in potentialFiles)
                        {
                            var assembly = Assembly.ReflectionOnlyLoadFrom(libraryFile);

                            var assemblyName = assembly.GetName().Name;

                            heatmap.Add($"[ASSEMBLY START] {assemblyName}");
                            heatmap.Add($""); // empty line

                            var integrationsToScanFor = flatIntegrations.Where(mr => mr.target.assembly.Equals(assemblyName)).ToList();
                            var typeNamesToScanFor = integrationsToScanFor.Select(i => i.target.type).Distinct();

                            var typesWeExplicitlyInstrument =
                                assembly.DefinedTypes
                                        .Where(dt => typeNamesToScanFor.Any(tn => dt.FullName == tn))
                                        .ToList();

                            var explicitToImplicit = new Dictionary<Type, List<Type>>();
                            typesWeExplicitlyInstrument.ForEach(t => explicitToImplicit.Add(t, new List<Type>()));
                            foreach (var potentialType in assembly.DefinedTypes)
                            {
                                foreach (var explicitType in typesWeExplicitlyInstrument)
                                {
                                    if (explicitType.IsAssignableFrom(potentialType))
                                    {
                                        // Found a candidate!
                                        explicitToImplicit[explicitType].Add(potentialType);
                                    }
                                }
                            }

                            foreach (var key in explicitToImplicit.Keys)
                            {
                                heatmap.Add($"[INSTRUMENTED TYPE] [{GetAccessModifierText(key)}] {key.FullName}");
                                var relevantInstrumentations =
                                    integrationsToScanFor
                                       .Where(i => i.target.assembly == key.Assembly.GetName().Name)
                                       .Where(i => i.target.type == key.FullName)
                                       .ToList();

                                var hits = explicitToImplicit[key];
                                foreach (var type in hits.OrderBy(h => h == key))
                                {
                                    // Get all methods if it's the explicit type we instrument, otherwise, only get overrides
                                    var methods =
                                        type
                                           .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                           .Where(m => (type == key) || (m.GetBaseDefinition() != m));

                                    var methodHits = new List<string>();

                                    foreach (var instrumentation in relevantInstrumentations)
                                    {
                                        var target = instrumentation.target;
                                        foreach (var methodInfo in methods)
                                        {
                                            if (methodInfo.Name != target.method)
                                            {
                                                continue;
                                            }

                                            bool overrides = ((type != key) || (methodInfo.GetBaseDefinition() != methodInfo));

                                            methodHits.Add($"[{type.FullName}]{(overrides ? " [OVERRIDES]" : string.Empty)} {GetAccessModifierText(methodInfo)} {methodInfo}");
                                        }
                                    }

                                    heatmap.AddRange(methodHits);
                                }

                                heatmap.Add($""); // empty line
                            }

                            heatmap.Add($"[ASSEMBLY END] {assemblyName}");

                            heatmap.Add($""); // empty line
                        }

                        File.AppendAllLines(heatmapFile, heatmap);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
            }
        }

        private static string GetAccessModifierText(this Type type)
        {
            if (type.IsPublic)
            {
                return "public";
            }

            if (type.IsNestedFamORAssem)
            {
                return "internal";
            }

            return "private";
        }

        private static string GetAccessModifierText(this MethodInfo methodInfo)
        {
            if (methodInfo.IsPrivate)
            {
                return "private";
            }

            if (methodInfo.IsFamily)
            {
                return "protected";
            }

            if (methodInfo.IsFamilyOrAssembly)
            {
                return "protected internal";
            }

            if (methodInfo.IsAssembly)
            {
                return "internal";
            }

            if (methodInfo.IsPublic)
            {
                return "public";
            }

            throw new ArgumentException("Did not find access modifier", nameof(methodInfo));
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

        private static string GetMethodSignature(MethodInfo method)
        {
            var returnType = method.ReturnType;
            var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();

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
    }
}
