// <copyright file="CallTargetInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// CallTarget Invoker
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class CallTargetInvoker
{
#if NETFRAMEWORK
    private const string Path = @"C:\ProgramData\Datadog .NET Tracer\logs\CallTargetInvoker.log";
    private const string NamedSlotName = "Datadog_IISPreInitStart";
    private static bool _isIisPreStartInitComplete;

    static CallTargetInvoker()
    {
        // The first time the CallTargetInvoker is called
        // we ensure that the non native parts of the initialization ran
        // This is required for AOT scenarios where there is no clrprofiler
        // to inject and run the loader.
        // TODO: We don't support AOT scenarios yet, so this is disabled for now
        // as it can cause issues by initializing the profiler too early
        // (during IIS pre-init)
        // Instrumentation.InitializeNoNativeParts();

        // Check if IIS automatic instrumentation has set the AppDomain property to indicate the PreStartInit state
        // If the property is not set, we should rely on other heuristics
        var state = AppDomain.CurrentDomain.GetData(NamedSlotName);
        if (state is bool boolState)
        {
            // we know we must be in IIS, so we need to check the app domain state
            _isIisPreStartInitComplete = !boolState;
            System.IO.File.AppendAllText(Path, $"{DateTime.UtcNow:T} CallTargetInvoker..ctor(): {NamedSlotName} was {boolState} for app domain {AppDomain.CurrentDomain.Id}{Environment.NewLine}");
        }
        else
        {
            try
            {
                System.IO.File.AppendAllText(Path, $"{DateTime.UtcNow:T} CallTargetInvoker..ctor(): {NamedSlotName} was not found so fetching process name. Stack trace {new StackTrace()}, for app domain {AppDomain.CurrentDomain.Id}{Environment.NewLine}");

                // We need to check if we're running in IIS, so that we know whether to _expect_
                // the IIS PreStartInit AppDomain property to be set. Outside of IIS, it will never be set.
                // We can't use ProcessHelpers here, because that could cause premature initialization of the
                // tracer, which could cause recursion issues with IIS PreStartInit code execution
                var processName = Process.GetCurrentProcess().ProcessName;

                if (processName.Equals("w3wp", StringComparison.OrdinalIgnoreCase) ||
                    processName.Equals("iisexpress", StringComparison.OrdinalIgnoreCase))
                {
                    // We're in IIS, so we know we need to check the AppDomain property
                    _isIisPreStartInitComplete = false;
                }
                else
                {
                    // If we're not in IIS, we don't need to run these checks
                    _isIisPreStartInitComplete = true;
                }
            }
            catch (Exception)
            {
                // Error getting process name, have to assume we _aren't_ in IIS, and that we don't need to wait for the app domain
                _isIisPreStartInitComplete = true;
            }

            // This is invoked if the CallTargetInvoker is invoked _prior_ to our startup hook.
            // This can happen in scenarios where there are two applications running in an app domain.

            // This _theoretically_ is a problem. In previous workarounds
            // (e.g. https://github.com/DataDog/dd-trace-dotnet/pull/1157) we resorted to
            // checking the process name, and checking the callstack to see if it contained
            // System.Web.Hosting.HostingEnvironment.Initialize(). However, that case should
            // only be hit in manual-only instrumentation scenarios, which by definition are
            // not a problem here because this type is only used by automatic instrumentation
        }
    }

    /// <summary>
    /// Mark tracer initialization as complete, and so we can start running CallTarget integrations
    /// </summary>
    public static void SetIisPreStartInitComplete()
    {
        System.IO.File.AppendAllText(Path, $"{DateTime.UtcNow:T} Setting CallTargetInvoker.IsIisPreStartComplete: from stack {new StackTrace()} for app domain {AppDomain.CurrentDomain.Id}{Environment.NewLine}");
        _isIisPreStartInitComplete = true;
    }
#endif

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget>(TTarget? instance)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget>.Invoke(instance);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1>(TTarget? instance, TArg1? arg1)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1>.Invoke(instance, ref arg1);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2>(TTarget? instance, TArg1? arg1, TArg2? arg2)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2>.Invoke(instance, ref arg1, ref arg2);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3>(TTarget? instance, TArg1? arg1, TArg2? arg2, TArg3? arg3)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3>.Invoke(instance, ref arg1, ref arg2, ref arg3);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <typeparam name="TArg4">Fourth argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <param name="arg4">Fourth argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>(TTarget? instance, TArg1? arg1, TArg2? arg2, TArg3? arg3, TArg4? arg4)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>.Invoke(instance, ref arg1, ref arg2, ref arg3, ref arg4);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <typeparam name="TArg4">Fourth argument type</typeparam>
    /// <typeparam name="TArg5">Fifth argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <param name="arg4">Fourth argument value</param>
    /// <param name="arg5">Fifth argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>(TTarget? instance, TArg1? arg1, TArg2? arg2, TArg3? arg3, TArg4? arg4, TArg5? arg5)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>.Invoke(instance, ref arg1, ref arg2, ref arg3, ref arg4, ref arg5);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <typeparam name="TArg4">Fourth argument type</typeparam>
    /// <typeparam name="TArg5">Fifth argument type</typeparam>
    /// <typeparam name="TArg6">Sixth argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <param name="arg4">Fourth argument value</param>
    /// <param name="arg5">Fifth argument value</param>
    /// <param name="arg6">Sixth argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(TTarget? instance, TArg1? arg1, TArg2? arg2, TArg3? arg3, TArg4? arg4, TArg5? arg5, TArg6? arg6)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>.Invoke(instance, ref arg1, ref arg2, ref arg3, ref arg4, ref arg5, ref arg6);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <typeparam name="TArg4">Fourth argument type</typeparam>
    /// <typeparam name="TArg5">Fifth argument type</typeparam>
    /// <typeparam name="TArg6">Sixth argument type</typeparam>
    /// <typeparam name="TArg7">Seventh argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <param name="arg4">Fourth argument value</param>
    /// <param name="arg5">Fifth argument value</param>
    /// <param name="arg6">Sixth argument value</param>
    /// <param name="arg7">Seventh argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(TTarget? instance, TArg1? arg1, TArg2? arg2, TArg3? arg3, TArg4? arg4, TArg5? arg5, TArg6? arg6, TArg7? arg7)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>.Invoke(instance, ref arg1, ref arg2, ref arg3, ref arg4, ref arg5, ref arg6, ref arg7);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <typeparam name="TArg4">Fourth argument type</typeparam>
    /// <typeparam name="TArg5">Fifth argument type</typeparam>
    /// <typeparam name="TArg6">Sixth argument type</typeparam>
    /// <typeparam name="TArg7">Seventh argument type</typeparam>
    /// <typeparam name="TArg8">Eighth argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <param name="arg4">Fourth argument value</param>
    /// <param name="arg5">Fifth argument value</param>
    /// <param name="arg6">Sixth argument value</param>
    /// <param name="arg7">Seventh argument value</param>
    /// <param name="arg8">Eighth argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(TTarget? instance, TArg1? arg1, TArg2? arg2, TArg3? arg3, TArg4? arg4, TArg5? arg5, TArg6? arg6, TArg7? arg7, TArg8? arg8)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>.Invoke(instance, ref arg1, ref arg2, ref arg3, ref arg4, ref arg5, ref arg6, ref arg7, ref arg8);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1>(TTarget? instance, ref TArg1? arg1)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1>.Invoke(instance, ref arg1);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2>(TTarget? instance, ref TArg1? arg1, ref TArg2? arg2)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2>.Invoke(instance, ref arg1, ref arg2);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3>(TTarget? instance, ref TArg1? arg1, ref TArg2? arg2, ref TArg3? arg3)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3>.Invoke(instance, ref arg1, ref arg2, ref arg3);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <typeparam name="TArg4">Fourth argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <param name="arg4">Fourth argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>(TTarget? instance, ref TArg1? arg1, ref TArg2? arg2, ref TArg3? arg3, ref TArg4? arg4)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>.Invoke(instance, ref arg1, ref arg2, ref arg3, ref arg4);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <typeparam name="TArg4">Fourth argument type</typeparam>
    /// <typeparam name="TArg5">Fifth argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <param name="arg4">Fourth argument value</param>
    /// <param name="arg5">Fifth argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>(TTarget? instance, ref TArg1? arg1, ref TArg2? arg2, ref TArg3? arg3, ref TArg4? arg4, ref TArg5? arg5)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>.Invoke(instance, ref arg1, ref arg2, ref arg3, ref arg4, ref arg5);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <typeparam name="TArg4">Fourth argument type</typeparam>
    /// <typeparam name="TArg5">Fifth argument type</typeparam>
    /// <typeparam name="TArg6">Sixth argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <param name="arg4">Fourth argument value</param>
    /// <param name="arg5">Fifth argument value</param>
    /// <param name="arg6">Sixth argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(TTarget? instance, ref TArg1? arg1, ref TArg2? arg2, ref TArg3? arg3, ref TArg4? arg4, ref TArg5? arg5, ref TArg6? arg6)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>.Invoke(instance, ref arg1, ref arg2, ref arg3, ref arg4, ref arg5, ref arg6);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <typeparam name="TArg4">Fourth argument type</typeparam>
    /// <typeparam name="TArg5">Fifth argument type</typeparam>
    /// <typeparam name="TArg6">Sixth argument type</typeparam>
    /// <typeparam name="TArg7">Seventh argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <param name="arg4">Fourth argument value</param>
    /// <param name="arg5">Fifth argument value</param>
    /// <param name="arg6">Sixth argument value</param>
    /// <param name="arg7">Seventh argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(TTarget? instance, ref TArg1? arg1, ref TArg2? arg2, ref TArg3? arg3, ref TArg4? arg4, ref TArg5? arg5, ref TArg6? arg6, ref TArg7? arg7)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>.Invoke(instance, ref arg1, ref arg2, ref arg3, ref arg4, ref arg5, ref arg6, ref arg7);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TArg1">First argument type</typeparam>
    /// <typeparam name="TArg2">Second argument type</typeparam>
    /// <typeparam name="TArg3">Third argument type</typeparam>
    /// <typeparam name="TArg4">Fourth argument type</typeparam>
    /// <typeparam name="TArg5">Fifth argument type</typeparam>
    /// <typeparam name="TArg6">Sixth argument type</typeparam>
    /// <typeparam name="TArg7">Seventh argument type</typeparam>
    /// <typeparam name="TArg8">Eighth argument type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arg1">First argument value</param>
    /// <param name="arg2">Second argument value</param>
    /// <param name="arg3">Third argument value</param>
    /// <param name="arg4">Fourth argument value</param>
    /// <param name="arg5">Fifth argument value</param>
    /// <param name="arg6">Sixth argument value</param>
    /// <param name="arg7">Seventh argument value</param>
    /// <param name="arg8">Eighth argument value</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(TTarget? instance, ref TArg1? arg1, ref TArg2? arg2, ref TArg3? arg3, ref TArg4? arg4, ref TArg5? arg5, ref TArg6? arg6, ref TArg7? arg7, ref TArg8? arg8)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>.Invoke(instance, ref arg1, ref arg2, ref arg3, ref arg4, ref arg5, ref arg6, ref arg7, ref arg8);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Begin Method Invoker Slow Path
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="arguments">Object arguments array</param>
    /// <returns>Call target state</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetState BeginMethod<TIntegration, TTarget>(TTarget? instance, object[] arguments)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return BeginMethodSlowHandler<TIntegration, TTarget>.Invoke(instance, arguments);
        }

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// End Method with Void return value invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="exception">Exception value</param>
    /// <param name="state">CallTarget state</param>
    /// <returns>CallTarget return structure</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetReturn EndMethod<TIntegration, TTarget>(TTarget? instance, Exception? exception, CallTargetState state)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            EndMethodHandler<TIntegration, TTarget>.Invoke(instance, exception, in state);
        }

        return CallTargetReturn.GetDefault();
    }

    /// <summary>
    /// End Method with Return value invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TReturn">Return type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="returnValue">Return value</param>
    /// <param name="exception">Exception value</param>
    /// <param name="state">CallTarget state</param>
    /// <returns>CallTarget return structure</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetReturn<TReturn> EndMethod<TIntegration, TTarget, TReturn>(TTarget? instance, TReturn? returnValue, Exception? exception, CallTargetState state)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            var result = EndMethodHandler<TIntegration, TTarget, TReturn>.Invoke(instance, returnValue, exception, in state);
            return new CallTargetReturn<TReturn>(result.GetReturnValue());
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }

    /// <summary>
    /// End Method with Void return value invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="exception">Exception value</param>
    /// <param name="state">CallTarget state</param>
    /// <returns>CallTarget return structure</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetReturn EndMethod<TIntegration, TTarget>(TTarget? instance, Exception? exception, in CallTargetState state)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return EndMethodHandler<TIntegration, TTarget>.Invoke(instance, exception, in state);
        }

        return CallTargetReturn.GetDefault();
    }

    /// <summary>
    /// End Method with Return value invoker
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <typeparam name="TReturn">Return type</typeparam>
    /// <param name="instance">Instance value</param>
    /// <param name="returnValue">Return value</param>
    /// <param name="exception">Exception value</param>
    /// <param name="state">CallTarget state</param>
    /// <returns>CallTarget return structure</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallTargetReturn<TReturn> EndMethod<TIntegration, TTarget, TReturn>(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (IsIisPreStartComplete<TIntegration>() && IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
        {
            IntegrationOptions<TIntegration, TTarget>.RecordTelemetry();
            return EndMethodHandler<TIntegration, TTarget, TReturn>.Invoke(instance, returnValue, exception, in state);
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }

    /// <summary>
    /// Log integration exception
    /// </summary>
    /// <typeparam name="TIntegration">Integration type</typeparam>
    /// <typeparam name="TTarget">Target type</typeparam>
    /// <param name="exception">Integration exception instance</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogException<TIntegration, TTarget>(Exception exception)
    {
        IntegrationOptions<TIntegration, TTarget>.LogException(exception);
    }

    /// <summary>
    /// Gets the default value of a type
    /// </summary>
    /// <typeparam name="T">Type to get the default value</typeparam>
    /// <returns>Default value of T</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? GetDefaultValue<T>() => default;

    /// <summary>
    /// Create a new instance of <see cref="CallTargetRefStruct"/>
    /// </summary>
    /// <param name="refStructPointer">Stack pointer of the ref struct instance</param>
    /// <param name="refStructTypeHandle">Runtime type handle of the ref struct</param>
    /// <returns>A new instance of the CallTargetRefStruct container</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe CallTargetRefStruct CreateRefStruct(void* refStructPointer, RuntimeTypeHandle refStructTypeHandle)
    {
        return CallTargetRefStruct.Create(refStructPointer, refStructTypeHandle);
    }

#if NETFRAMEWORK
    private static bool IsIisPreStartComplete<TIntegration>([CallerMemberName] string callerName = null!)
    {
        if (_isIisPreStartInitComplete)
        {
            System.IO.File.AppendAllText(Path, $"{DateTime.UtcNow:T} CallTargetInvoker.IsIisPreStartComplete<{typeof(TIntegration).Name}>({callerName}): was already true  for app domain {AppDomain.CurrentDomain.Id}{Environment.NewLine}");
            return true;
        }

        var boolState = AppDomain.CurrentDomain.GetData(NamedSlotName);
        System.IO.File.AppendAllText(Path, $"{DateTime.UtcNow:T} CallTargetInvoker.IsIisPreStartComplete<{typeof(TIntegration).Name}>({callerName}): checked pre-start complete and got {boolState} for app domain {AppDomain.CurrentDomain.Id}{Environment.NewLine}");
        _isIisPreStartInitComplete = boolState is false;

        // We _have_ to allow the HttpModule_Integration invocation through, even if we're in the Iis PreStart phase.
        // That integration is specifically designed to run in this phase. We considered other options
        // such as moving it to Instrumentation.Initialise, or rewriting directly with the profiling API
        // but this was the simplest, easiest, and safest approach we could see generally.
        var returnValue = _isIisPreStartInitComplete || typeof(TIntegration) == typeof(HttpModule_Integration);
        System.IO.File.AppendAllText(Path, $"{DateTime.UtcNow:T} CallTargetInvoker.IsIisPreStartComplete<{typeof(TIntegration).Name}>({callerName}): returned {returnValue} for app domain {AppDomain.CurrentDomain.Id}{Environment.NewLine}");
        return returnValue;
    }

#else
    // Compiler should inline this out of the condition checks completely
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIisPreStartComplete<TIntegration>() => true;
#endif
}
