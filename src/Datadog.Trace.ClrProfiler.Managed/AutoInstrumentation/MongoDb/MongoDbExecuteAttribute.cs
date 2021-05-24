// <copyright file="MongoDbExecuteAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
            ParameterTypeNames = new[] { "MongoDB.Driver.Core.Connections.IConnection", ClrNames.CancellationToken };

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
