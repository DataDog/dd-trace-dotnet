using System;

namespace Samples.Probes
{
    public class MethodProbeTestDataAttribute : Attribute
    {
        public MethodProbeTestDataAttribute(string returnTypeName, string[] parametersTypeName, bool skip = false, params string[] skipOnFramework)
        {
            ReturnTypeName = returnTypeName;
            ParametersTypeName = parametersTypeName;
            Skip = skip;
            SkipOnFrameworks = skipOnFramework;
        }

        public string ReturnTypeName { get; }
        public string[] ParametersTypeName { get; }
        public bool Skip { get; }
        public string[] SkipOnFrameworks { get; }
    }
}
