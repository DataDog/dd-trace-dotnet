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
