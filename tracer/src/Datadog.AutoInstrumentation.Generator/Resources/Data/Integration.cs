// <copyright file="$(IntegrationClassName)Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.$(Namespace);

/// <summary>
/// $(FullName) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "$(AssemblyName)",
    TypeName = "$(TypeName)",
    MethodName = "$(MethodName)",
    ReturnTypeName = $(ReturnTypeName),
    ParameterTypeNames = $(ParameterTypeNames),
    MinimumVersion = "$(MinimumVersion)",
    MaximumVersion = "$(MaximumVersion)",
    IntegrationName = "$(IntegrationName)")]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class $(IntegrationClassName)Integration
{$(OnMethodBegin)$(OnMethodEnd)$(OnAsyncMethodEnd)}
$(DuckTypeDefinitions)
