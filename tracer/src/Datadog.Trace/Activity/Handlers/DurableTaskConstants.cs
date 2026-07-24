// <copyright file="DurableTaskConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// Constants for Azure Functions Durable Task host-side distributed tracing.
    /// Mirrors <c>Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation.Schema</c>.
    /// </summary>
    internal static class DurableTaskConstants
    {
        internal const string ActivitySourceName = "WebJobs.Extensions.DurableTask";

        internal static class Tags
        {
            internal const string Type = "durabletask.type";
            internal const string Name = "durabletask.task.name";
            internal const string Version = "durabletask.task.version";
            internal const string InstanceId = "durabletask.task.instance_id";
            internal const string ExecutionId = "durabletask.task.execution_id";
            internal const string TaskId = "durabletask.task.task_id";
            internal const string Operation = "durabletask.task.operation";
        }

        internal static class TaskTypes
        {
            internal const string Orchestration = "orchestration";
            internal const string CreateOrchestration = "create_orchestration";
            internal const string Activity = "activity";
            internal const string Entity = "entity";
            internal const string Event = "event";
            internal const string Timer = "timer";
            internal const string Client = "client";
        }
    }
}
