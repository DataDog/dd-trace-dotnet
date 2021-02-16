using Datadog.Trace.ClrProfiler.Integrations;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    internal class MongoDbExecuteAttribute : MongoDbInstrumentMethodAttribute
    {
        public MongoDbExecuteAttribute(string typeName, bool isGeneric)
            : base(typeName)
        {
            MinimumVersion = MongoDbIntegration.Major2Minor2;
            MaximumVersion = MongoDbIntegration.Major2;
            MethodName = "Execute";

            if (isGeneric)
            {
                ReturnTypeName = "T";
            }
            else
            {
                ReturnTypeName = ClrNames.Void;
            }
        }
    }
}
