// <copyright file="WebDriverExecuteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Selenium;

#pragma warning disable SA1201

/// <summary>
/// OpenQA.Selenium.Response OpenQA.Selenium.WebDriver::Execute(System.String,System.Collections.Generic.Dictionary`2[System.String,System.Object]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "WebDriver",
    TypeName = "OpenQA.Selenium.WebDriver",
    MethodName = "Execute",
    ReturnTypeName = "OpenQA.Selenium.Response",
    ParameterTypeNames = [ClrNames.String, "System.Collections.Generic.Dictionary`2[System.String,System.Object]"],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = SeleniumCommon.IntegrationName)]
[InstrumentMethod(
    AssemblyName = "WebDriver",
    TypeName = "OpenQA.Selenium.WebDriver",
    MethodName = "Execute",
    ReturnTypeName = "OpenQA.Selenium.Response",
    ParameterTypeNames = [ClrNames.String, "System.Collections.Generic.Dictionary`2[System.String,System.Object]"],
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    CallTargetIntegrationKind = CallTargetKind.Derived,
    IntegrationName = SeleniumCommon.IntegrationName)]
[InstrumentMethod(
    AssemblyName = "WebDriver",
    TypeName = "OpenQA.Selenium.Remote.RemoteWebDriver",
    MethodName = "Execute",
    ReturnTypeName = "OpenQA.Selenium.Remote.Response",
    ParameterTypeNames = [ClrNames.String, "System.Collections.Generic.Dictionary`2[System.String,System.Object]"],
    MinimumVersion = "3.0.0",
    MaximumVersion = "3.*.*",
    IntegrationName = SeleniumCommon.IntegrationName)]
[InstrumentMethod(
    AssemblyName = "WebDriver",
    TypeName = "OpenQA.Selenium.Remote.RemoteWebDriver",
    MethodName = "Execute",
    ReturnTypeName = "OpenQA.Selenium.Remote.Response",
    ParameterTypeNames = [ClrNames.String, "System.Collections.Generic.Dictionary`2[System.String,System.Object]"],
    MinimumVersion = "3.0.0",
    MaximumVersion = "3.*.*",
    CallTargetIntegrationKind = CallTargetKind.Derived,
    IntegrationName = SeleniumCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class WebDriverExecuteIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref string? driverCommandToExecute, ref Dictionary<string, object>? parameters)
        where TTarget : IWebDriverProxy
    {
        if (!SeleniumCommon.IsEnabled)
        {
            return CallTargetState.GetDefault();
        }

        switch (driverCommandToExecute)
        {
            case SeleniumCommon.CommandGet:
                SeleniumCommon.OnBeforePageLoad(instance, parameters);
                break;
            case SeleniumCommon.CommandClose:
                SeleniumCommon.OnPageClose(instance);
                break;
            case SeleniumCommon.CommandQuit:
                SeleniumCommon.OnQuit(instance);
                break;
        }

        return new CallTargetState(null, new IntegrationState(driverCommandToExecute, parameters));
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
        where TTarget : IWebDriverProxy
    {
        if (state.State is IntegrationState integrationState)
        {
            switch (integrationState.DriverCommandToExecute)
            {
                case SeleniumCommon.CommandGet:
                    SeleniumCommon.OnAfterPageLoad(instance, integrationState.Parameters);
                    break;
            }
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }

    private readonly struct IntegrationState
    {
        public readonly string? DriverCommandToExecute;
        public readonly Dictionary<string, object>? Parameters;

        public IntegrationState(string? driverCommandToExecute, Dictionary<string, object>? parameters)
        {
            DriverCommandToExecute = driverCommandToExecute;
            Parameters = parameters;
        }
    }
}
