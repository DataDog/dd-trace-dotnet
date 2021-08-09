// <copyright file="MongoDbInstrumentMethodAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
        }
    }
}
