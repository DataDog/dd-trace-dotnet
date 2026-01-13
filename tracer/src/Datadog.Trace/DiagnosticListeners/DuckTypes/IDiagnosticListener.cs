// <copyright file="IDiagnosticListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners.DuckTypes;

#nullable enable
/// <summary>
/// Ducktyping for DiagnosticListener
/// </summary>
public interface IDiagnosticListener : IDuckType
{
     /// <summary>
     /// Gets a value of DiagnosticListener.Name
     /// </summary>
     string Name { get; }

     /// <summary>
     /// Ducktype for Subscribe
     /// </summary>
     IDisposable Subscribe(IObserver<KeyValuePair<string, object?>> observer, Predicate<string>? isEnabled);
}
