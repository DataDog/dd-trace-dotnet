// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <cstdint>

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "IService.h"
#include "StackSnapshotResultFrameInfo.h"

// forward declarations
class StackFrameInfo;


class ISymbolsResolver : public IService
{
public:
    virtual bool ResolveAppDomainInfoSymbols(AppDomainID appDomainId,
                                             const std::uint32_t appDomainNameBuffSize,
                                             std::uint32_t* pActualAppDomainNameLen,
                                             WCHAR* pAppDomainNameBuff,
                                             std::uint64_t* pAppDomainProcessId,
                                             bool offloadToWorkerThread) = 0;

    virtual bool ResolveStackFrameSymbols(const StackSnapshotResultFrameInfo& capturedFrame,
                                          StackFrameInfo** ppResolvedFrame,
                                          bool offloadToWorkerThread) = 0;
};