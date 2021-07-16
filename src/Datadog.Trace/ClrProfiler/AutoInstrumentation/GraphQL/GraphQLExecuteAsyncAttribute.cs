// <copyright file="GraphQLExecuteAsyncAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    internal class GraphQLExecuteAsyncAttribute : InstrumentMethodAttribute
    {
        public GraphQLExecuteAsyncAttribute()
        {
            IntegrationName = GraphQLCommon.IntegrationName;
            MethodName = "ExecuteAsync";
            ReturnTypeName = "System.Threading.Tasks.Task`1<GraphQL.ExecutionResult>";
            ParameterTypeNames = new[] { "GraphQL.Execution.ExecutionContext" };
        }
    }
}
