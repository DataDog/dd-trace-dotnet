// <copyright file="ShadowStackHolder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ShadowStackHolder
    {
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<ShadowStackHolder>();
        private static readonly AsyncLocal<ShadowStackTree?> ShadowStackTree = new();
        [ThreadStatic]
        private static ShadowStackTree? _lastShadowStackTreeOnThisThread;

        public static ShadowStackTree? ShadowStack
        {
            get => ShadowStackTree.Value ?? _lastShadowStackTreeOnThisThread;
            set => ShadowStackTree.Value = value!;
        }

        public static bool IsShadowStackTrackingEnabled => ShadowStack != null;

        public static ShadowStackTree EnsureShadowStackEnabled()
        {
            ShadowStackTree.Value ??= new ShadowStackTree();
            _lastShadowStackTreeOnThisThread = ShadowStackTree.Value;
            return _lastShadowStackTreeOnThisThread;
        }

        public static void DisableShadowStackTracking()
        {
            Logger.Information("DisableShadowStackTracking called on threadID {ManagedThreadId} and taskId {CurrentId}.", Thread.CurrentThread.ManagedThreadId, Task.CurrentId);
            ShadowStackTree.Value = null;
            _lastShadowStackTreeOnThisThread = null;
        }
    }
}
