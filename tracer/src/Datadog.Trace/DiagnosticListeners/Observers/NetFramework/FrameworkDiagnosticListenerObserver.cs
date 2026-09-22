// <copyright file="FrameworkDiagnosticListenerObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK

using System;
using Datadog.Trace.DiagnosticListeners.DuckTypes;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners
{
    internal sealed class FrameworkDiagnosticListenerObserver
    {
        private readonly DiagnosticManager _diagnosticManager;

        public FrameworkDiagnosticListenerObserver(DiagnosticManager diagnosticManager)
        {
            _diagnosticManager = diagnosticManager;
        }

        [DuckReverseMethod]
        public void OnCompleted()
        {
        }

        [DuckReverseMethod]
        public void OnError(Exception error)
        {
        }

        [DuckReverseMethod]
        public void OnNext(IDiagnosticListener value)
        {
            _diagnosticManager.OnNext(value);
        }
    }
}

#endif
