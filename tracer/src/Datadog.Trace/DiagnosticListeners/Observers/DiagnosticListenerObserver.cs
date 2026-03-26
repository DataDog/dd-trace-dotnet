// <copyright file="DiagnosticListenerObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Diagnostics;
using Datadog.Trace.DiagnosticListeners.DuckTypes;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners
{
    internal sealed class DiagnosticListenerObserver : IObserver<DiagnosticListener>
    {
        private DiagnosticManager _diagnosticManager;

        public DiagnosticListenerObserver(DiagnosticManager diagnosticManager)
        {
            _diagnosticManager = diagnosticManager;
        }

        public void OnCompleted()
        {
            return;
        }

        public void OnError(Exception error)
        {
            return;
        }

        public void OnNext(DiagnosticListener value)
        {
            _diagnosticManager.OnNext(value.DuckCast<IDiagnosticListener>());
        }
    }
}

#endif
