using System;
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
                               let attribute = wrapperMethod.GetCustomAttribute<InterceptMethodAttribute>(inherit: false)
                               where attribute != null
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
                                                                 signature = item.attribute.TargetSignature
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
        }

        private static string GetIntegrationName(Type wrapperType)
        {
            const string integrations = "Integration";
            var typeName = wrapperType.Name;

            if (typeName.EndsWith(integrations, StringComparison.InvariantCultureIgnoreCase))
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
