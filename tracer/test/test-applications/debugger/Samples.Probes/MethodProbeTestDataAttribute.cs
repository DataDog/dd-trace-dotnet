using System;

namespace Samples.Probes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MethodProbeTestDataAttribute : ProbeAttributeBase
    {
        public MethodProbeTestDataAttribute(string returnTypeName, string[] parametersTypeName, bool skip = false, int phase = 1, bool unlisted = false, params string[] skipOnFramework) 
            : base(skip, phase, unlisted, skipOnFramework)
        {
            
            ReturnTypeName = returnTypeName;
            ParametersTypeName = parametersTypeName;
        }

        public string ReturnTypeName { get; }
        public string[] ParametersTypeName { get; }
    }
}
