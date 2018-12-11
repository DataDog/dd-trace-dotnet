using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace.ClrProfiler;
using Mono.Cecil;

namespace GenerateIntegrationDefinitions
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var integrationsAssembly = typeof(Instrumentation).Assembly;

            var fileName = integrationsAssembly.CodeBase.Substring("file:///".Length);
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(fileName);
            //assemblyDefinition.MainModule.GetTypes().FirstOrDefault().Methods.FirstOrDefault().Body.GetILProcessor().

            var integrations = from wrapperType in integrationsAssembly.GetTypes()
                               let interceptTypeAttribute = wrapperType.GetCustomAttribute<InterceptTypeAttribute>(inherit: false)
                               where interceptTypeAttribute != null
                               from wrapperMethod in wrapperType.GetRuntimeMethods()
                               let interceptMethodAttribute = wrapperMethod.GetCustomAttribute<InterceptMethodAttribute>(inherit: false)
                               where interceptMethodAttribute != null
                               select new
                               {
                                   target = new
                                   {
                                       assembly = interceptTypeAttribute.AssemblyName,
                                       type = interceptTypeAttribute.TypeName,
                                       method = interceptMethodAttribute.MethodName ?? wrapperMethod.Name
                                   },
                                   wrapper = new
                                   {
                                       assembly = integrationsAssembly.FullName,
                                       type = wrapperType.FullName,
                                       method = wrapperMethod.Name,
                                       signature = GetMethodSignature(wrapperMethod)
                                   }
                               };

            var list = integrations.ToList();
        }

        private static string GetMethodSignature(MethodInfo method)
        {
            var callingConvention = method.CallingConvention;
            var returnType = method.ReturnType.MetadataToken;
            var parameters = method.GetParameters().Select(p => p.ParameterType.MetadataToken).ToList();
            return "";
        }
    }
}
