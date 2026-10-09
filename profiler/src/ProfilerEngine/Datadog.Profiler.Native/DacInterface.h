// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

// Forward declarations so this header does not leak any DAC/coreclr-internal headers
// (sospriv.h / clrdata.h / dacprivate.h / xclrdata.h). All of those are isolated inside the
// Dac*.cpp translation units to contain header friction (this is the first DAC consumer in the
// profiler).
struct ISOSDacInterface;
struct IXCLRDataProcess;
struct ICLRDataTarget;

class IRuntimeInfo;

// Loads the build-matched DAC next to the runtime module (mscordaccore[.dll/.so] for modern .NET,
// mscordacwks.dll for .NET Framework) and exposes the ISOSDacInterface used by the SOS-style
// !eeheap enumeration. The DAC is the same component SOS and ClrMD use: a consumer never reads
// runtime memory itself - it implements ICLRDataTarget::ReadVirtual (LiveDataTarget) and the DAC
// reconstructs CLR structures from it.
//
// All initialization is best-effort: any failure (DAC missing, version mismatch, QI failure) leaves
// the object unavailable and the backend silently no-ops.
class DacInterface
{
public:
    DacInterface() = default;
    ~DacInterface();

    DacInterface(const DacInterface&) = delete;
    DacInterface& operator=(const DacInterface&) = delete;

    // Locates + loads the DAC and creates the IXCLRDataProcess/ISOSDacInterface. Returns false on
    // any failure. pRuntimeInfo selects the modern-.NET vs .NET Framework DAC name.
    bool TryLoad(IRuntimeInfo* pRuntimeInfo);

    bool IsAvailable() const
    {
        return _sos != nullptr;
    }

    ISOSDacInterface* GetSos() const
    {
        return _sos;
    }

    // Invalidates the DAC's cached view of the (live, running) target so the next enumeration
    // reflects current memory. Best-effort: failures are ignored. See NativeHeapReporting.md
    // ("DAC usage model and caching") for the rationale.
    void Flush();

private:
    void Reset();

    // Native module handle of the loaded DAC (HMODULE on Windows, void* from dlopen on Linux).
    void* _dacModule = nullptr;
    IXCLRDataProcess* _process = nullptr;
    ISOSDacInterface* _sos = nullptr;
    ICLRDataTarget* _dataTarget = nullptr;
};
