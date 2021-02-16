using Datadog.Trace.ClrProfiler.Integrations;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    internal abstract class MongoDbInstrumentMethodAttribute : InstrumentMethodAttribute
    {
        protected MongoDbInstrumentMethodAttribute(string typeName)
        {
            AssemblyName = MongoDbIntegration.MongoDbClientAssembly;
            TypeName = typeName;
            IntegrationName = MongoDbIntegration.IntegrationName;
            ParameterTypeNames = new[] { "MongoDB.Driver.Core.Connections.IConnection", ClrNames.CancellationToken };
        }
    }
}
