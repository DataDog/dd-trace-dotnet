// <copyright file="MongoDbExecuteAsyncAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.Integrations;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    internal class MongoDbExecuteAsyncAttribute : MongoDbInstrumentMethodAttribute
    {
        public MongoDbExecuteAsyncAttribute(string typeName, bool isGeneric)
            : base(typeName)
        {
            MinimumVersion = MongoDbIntegration.Major2Minor1;
            MaximumVersion = MongoDbIntegration.Major2;
            MethodName = "ExecuteAsync";
            ParameterTypeNames = new[] { "MongoDB.Driver.Core.Connections.IConnection", ClrNames.CancellationToken };

            if (isGeneric)
            {
                ReturnTypeName = "System.Threading.Tasks.Task`1<T>";
            }
            else
            {
                ReturnTypeName = "System.Threading.Tasks.Task";
            }
        }
    }
}
