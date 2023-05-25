// <copyright file="FireOnStartCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

#if !NETFRAMEWORK

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;

/// <summary>
/// FireOnStartCommon integration
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Server.Kestrel.Core",
    TypeName = "Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol",
    MethodName = "FireOnStarting",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "" },
    MinimumVersion = "2.0.0.0",
    MaximumVersion = "7.*.*.*.*",
    IntegrationName = IntegrationName,
    InstrumentationCategory = InstrumentationCategory.AppSec)]
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Server.IIS",
    TypeName = "Microsoft.AspNetCore.Server.IIS.Core.IISHttpContext",
    MethodName = "FireOnStarting",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "" },
    MinimumVersion = "2.0.0.0",
    MaximumVersion = "7.*.*.*.*",
    IntegrationName = IntegrationName,
    InstrumentationCategory = InstrumentationCategory.AppSec)]

[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class FireOnStartCommon
{
    private const string IntegrationName = nameof(IntegrationId.AspNetCore);

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, System.Exception exception, in CallTargetState state)
    {
        var security = Security.Instance;
        if (security.Enabled)
        {

        }

        return CallTargetReturn.GetDefault();
    }
}

#endif
