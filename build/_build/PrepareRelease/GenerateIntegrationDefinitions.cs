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

        public static void Run(ICollection<string> assemblyPaths, params string[] outputDirectories)
        {
            Console.WriteLine("Updating the integrations definitions");

            var callTargetIntegrations = Enumerable.Empty<Integration>();
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

            var serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };

            // remove duplicates
            callTargetIntegrations = callTargetIntegrations
                                    .GroupBy(x => x.Name)
                                    .Select(x => new Integration()
                                     {
                                         Name = x.Key,
                                         MethodReplacements = x
                                                             .SelectMany(y => y.MethodReplacements)
                                                             .Distinct()
                                                             .ToArray(),
                                     });

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

            var json = JsonConvert.SerializeObject(callTargetIntegrations.Concat(callSiteIntegrations), serializerSettings);
            Console.WriteLine($"Writing integration.json...");

            foreach (var outputDirectory in outputDirectories)
            {
                var filename = Path.Combine(outputDirectory, "integrations.json");
                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(filename, json, utf8NoBom);
            }
        }

        static IEnumerable<Integration> GetCallTargetIntegrations(ICollection<Assembly> assemblies)
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
                                                    from wrapperType in assembly.GetTypes()
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
                                         orderby integrationName
                                         group new
                                             {
                                                 assembly,
                                                 wrapperType,
                                                 attribute
                                             }
                                             by integrationName into g
                                         select new Integration
                                         {
                                             Name = g.Key,
                                             MethodReplacements = (from item in g
                                                                 from assembly in GetPropertyValue<string[]>(item.attribute, "AssemblyNames")
                                                                 let version = GetPropertyValue<object>(item.attribute, "VersionRange")
                                                                 select new Integration.MethodReplacement
                                                                 {
                                                                     Caller = new Integration.CallerDetail(),
                                                                     Target = new Integration.TargetDetail
                                                                     {
                                                                         Assembly = assembly,
                                                                         Type = GetPropertyValue<string>(item.attribute, "TypeName"),
                                                                         Method = GetPropertyValue<string>(item.attribute, "MethodName"),
                                                                         SignatureTypes = new string[] { GetPropertyValue<string>(item.attribute, "ReturnTypeName") }
                                                                                          .Concat(GetPropertyValue<string[]>(item.attribute, "ParameterTypeNames") ?? Enumerable.Empty<string>())
                                                                                          .ToArray(),
                                                                         MinimumMajor = GetPropertyValue<ushort>(version, "MinimumMajor"),
                                                                         MinimumMinor = GetPropertyValue<ushort>(version, "MinimumMinor"),
                                                                         MinimumPatch = GetPropertyValue<ushort>(version, "MinimumPatch"),
                                                                         MaximumMajor = GetPropertyValue<ushort>(version, "MaximumMajor"),
                                                                         MaximumMinor = GetPropertyValue<ushort>(version, "MaximumMinor"),
                                                                         MaximumPatch = GetPropertyValue<ushort>(version, "MaximumPatch"),
                                                                     },
                                                                     Wrapper = new Integration.WrapperDetail()
                                                                     {
                                                                         Assembly = item.assembly.FullName,
                                                                         Type = item.wrapperType.FullName,
                                                                         Action = "CallTargetModification"
                                                                     }
                                                                 }).ToArray()
                                         };
            return callTargetIntegrations.ToList();
        }



        static IEnumerable<Integration> GetCallSiteIntegrations(ICollection<Assembly> assemblies)
        {
            // find all methods in Datadog.Trace.ClrProfiler.Managed.dll with [InterceptMethod]
            // and create objects that will generate correct JSON schema
            var integrations = from assembly in assemblies
                               from wrapperType in assembly.GetTypes()
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

        private class Integration
        {
            public string Name { get; init; }

            public MethodReplacement[] MethodReplacements { get; init; }

            public class MethodReplacement
            {
                public CallerDetail Caller { get; init; }

                public TargetDetail Target { get; init; }

                public WrapperDetail Wrapper { get; init; }

                protected bool Equals(MethodReplacement other) =>
                    Equals(Caller, other.Caller) &&
                    Equals(Target, other.Target) &&
                    Equals(Wrapper, other.Wrapper);

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj))
                    {
                        return false;
                    }

                    if (ReferenceEquals(this, obj))
                    {
                        return true;
                    }

                    if (obj.GetType() != this.GetType())
                    {
                        return false;
                    }

                    return Equals((MethodReplacement)obj);
                }

                public override int GetHashCode() => HashCode.Combine(Caller, Target, Wrapper);
            }

            public record CallerDetail
            {
                public string Assembly { get; init; }

                public string Type { get; init; }

                public string Method { get; init; }

            }

            public class TargetDetail
            {
                public string Assembly { get; init; }

                public string Type { get; init; }

                public string Method { get; init; }

                public string Signature { get; init; }

                public string[] SignatureTypes { get; init; }

                public ushort MinimumMajor {get;init;}

                public ushort MinimumMinor {get;init;}

                public ushort MinimumPatch {get;init;}

                public ushort MaximumMajor {get;init;}

                public ushort MaximumMinor {get;init;}

                public ushort MaximumPatch {get;init;}

                private bool Equals(TargetDetail other) =>
                    Assembly == other.Assembly &&
                    Type == other.Type &&
                    Method == other.Method &&
                    Signature == other.Signature &&
                    ((SignatureTypes is null && other.SignatureTypes is null) ||
                     (SignatureTypes is not null && other.SignatureTypes is not null &&
                      string.Join(",", SignatureTypes) == string.Join(",", other.SignatureTypes))) &&
                    MinimumMajor == other.MinimumMajor &&
                    MinimumMinor == other.MinimumMinor &&
                    MinimumPatch == other.MinimumPatch &&
                    MaximumMajor == other.MaximumMajor &&
                    MaximumMinor == other.MaximumMinor &&
                    MaximumPatch == other.MaximumPatch;

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj))
                    {
                        return false;
                    }

                    if (ReferenceEquals(this, obj))
                    {
                        return true;
                    }

                    if (obj.GetType() != this.GetType())
                    {
                        return false;
                    }

                    return Equals((TargetDetail)obj);
                }

                public override int GetHashCode()
                {
                    var hashCode = new HashCode();
                    hashCode.Add(Assembly);
                    hashCode.Add(Type);
                    hashCode.Add(Method);
                    hashCode.Add(Signature);
                    hashCode.Add(SignatureTypes?.Length > 0 ? string.Join(",", SignatureTypes) : null);
                    hashCode.Add(MinimumMajor);
                    hashCode.Add(MinimumMinor);
                    hashCode.Add(MinimumPatch);
                    hashCode.Add(MaximumMajor);
                    hashCode.Add(MaximumMinor);
                    hashCode.Add(MaximumPatch);
                    return hashCode.ToHashCode();
                }
            }

            public record WrapperDetail
            {
                public string Assembly { get; init; }

                public string Type { get; init; }

                public string Method { get; init; }

                public string Signature { get; init; }

                public string Action { get; init; }
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
                if(File.Exists(assemblyPath))
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }

                return null;
            }

        }
    }
}
