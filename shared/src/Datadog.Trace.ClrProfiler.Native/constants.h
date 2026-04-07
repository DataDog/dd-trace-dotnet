// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "../../../shared/src/native-src/string.h"

inline static const std::string conf_filename = "loader.conf";
inline static const ::shared::WSTRING datadog_logs_folder_path = WStr(R"(Datadog .NET Tracer\logs)");

// Note that you should also consider adding to the SSI tracer/build/artifacts/requirements.json file
// FIXME: this should also take into account case insensitivity, but that is not yet supported
// https://devblogs.microsoft.com/oldnewthing/20241007-00/?p=110345
const shared::WSTRING default_exclude_processes[]{
    WStr("aspnet_compiler.exe"),
    WStr("aspnet_state.exe"),
    WStr("CollectGuestLogs.exe"), // https://github.com/Azure/WindowsVMAgent
    WStr("createdump"),
    WStr("csc.exe"),
    WStr("dd-trace"),
    WStr("dd-trace.exe"),
    WStr("devenv.exe"),
    WStr("iisexpresstray.exe"),
    WStr("InetMgr.exe"),
    WStr("Microsoft.ServiceHub.Controller.exe"),
    WStr("MSBuild.exe"),
    WStr("MsDtsSrvr.exe"),
    WStr("MsSense.exe"), // Defender: https://learn.microsoft.com/en-us/defender-endpoint/switch-to-mde-troubleshooting
    WStr("msvsmon.exe"),
    WStr("PerfWatson2.exe"),
    WStr("powershell.exe"),
    WStr("pwsh.exe"),
    WStr("pwsh"),
    WStr("SenseCE.exe"),             // 
    WStr("SenseCM.exe"),             // Defender processes
    WStr("SenseCnCProxy.exe"),       // https://learn.microsoft.com/en-us/defender-endpoint/switch-to-mde-troubleshooting
    WStr("SenseIR.exe"),             // 
    WStr("SenseSampleUploader.exe"), // 
    WStr("ServiceHub.DataWarehouseHost.exe"),
    WStr("ServiceHub.Host.CLR.exe"),
    WStr("ServiceHub.Host.CLR.x86.exe"),
    WStr("ServiceHub.IdentityHost.exe"),
    WStr("ServiceHub.RoslynCodeAnalysisService32.exe"),
    WStr("ServiceHub.SettingsHost.exe"),
    WStr("ServiceHub.TestWindowStoreHost.exe"),
    WStr("ServiceHub.ThreadedWaitDialog.exe"),
    WStr("ServiceHub.VSDetouredHost.exe"),
    WStr("sqlagent.exe"),
    WStr("sqlbrowser.exe"),
    WStr("sqlservr.exe"),
    WStr("VBCSCompiler"),
    WStr("VBCSCompiler.exe"),
    WStr("vsdbg"),
    WStr("vsdbg.exe"),
    WStr("WaAppAgent.exe"),            // https://github.com/Azure/WindowsVMAgent
    WStr("WerFault.exe"),              // WER = Windows Error Reporting - can kick in when a process crashes
    WStr("WindowsAzureGuestAgent.exe") // https://github.com/Azure/WindowsVMAgent
};
