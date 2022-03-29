using System;

namespace Samples.Probes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MethodProbeTestDataAttribute : ProbeAttributeBase
    {
        public MethodProbeTestDataAttribute(string returnTypeName, string[] parametersTypeName, bool skip = false, params string[] skipOnFramework) :base(skip, skipOnFramework)
        {
            
            ReturnTypeName = returnTypeName;
            ParametersTypeName = parametersTypeName;
        }

        public string ReturnTypeName { get; }
        public string[] ParametersTypeName { get; }
    }
}
