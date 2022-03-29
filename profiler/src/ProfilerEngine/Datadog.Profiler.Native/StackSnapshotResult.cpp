// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackSnapshotResult.h"
#include "StackFrameCodeKind.h"

#include "SymbolsResolver.h"

//  —–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—
// | See memory layout map in the header file. |
//  —–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—

StackSnapshotResult StackSnapshotResult::InvalidSnapshot = StackSnapshotResult(nullptr);

std::uint32_t StackSnapshotResult::GetRequiredBufferBytes(const StackSnapshotResultBuffer* pSnapshotResult)
{
    if (pSnapshotResult == nullptr)
    {
        return 0;
    }
    else
    {
        return GetRequiredBufferBytesForFramesCount(pSnapshotResult->GetFramesCount());
    }
}

StackSnapshotResult::StackSnapshotResult(void* pData) :
    _pData{pData}
{
}

std::uint64_t StackSnapshotResult::GetRepresentedDurationNanoseconds(void) const
{
    if (IsValid())
    {
        return *(static_cast<std::uint64_t*>(GetPointerToOffset(0)));
    }
    else
    {
        return 0;
    }
}

std::uint32_t StackSnapshotResult::GetProfilerThreadInfoId(void) const
{
    if (IsValid())
    {
        return *(static_cast<std::uint32_t*>(GetPointerToOffset(8)));
    }
    else
    {
        return 0;
    }
}

std::uint64_t StackSnapshotResult::GetProfilerAppDomainId(void) const
{
    if (IsValid())
    {
        return *(static_cast<std::uint64_t*>(GetPointerToOffset(16)));
    }
    else
    {
        return 0;
    }
}

std::uint16_t StackSnapshotResult::GetFramesCount(void) const
{
    if (IsValid())
    {
        return *(static_cast<std::uint16_t*>(GetPointerToOffset(24)));
    }
    else
    {
        return 0;
    }
}

bool StackSnapshotResult::TryGetFrameAtIndex(std::uint16_t index, StackFrameCodeKind* pCodeKind, std::uint64_t* pFrameInfoCode) const
{
    if (IsValid() && index < GetFramesCount())
    {
        if (pCodeKind != nullptr)
        {

            *pCodeKind = *(static_cast<StackFrameCodeKind*>(GetPointerToOffset(26 + index * 9)));
        }

        if (pFrameInfoCode != nullptr)
        {

            *pFrameInfoCode = *(static_cast<std::uint64_t*>(GetPointerToOffset(27 + index * 9)));
        }

        return true;
    }
    else
    {
        return false;
    }
}

std::uint32_t StackSnapshotResult::WriteData(const StackSnapshotResultBuffer* pSnapshotResult, const ManagedThreadInfo* pThreadInfo)
{
    if (!IsValid())
    {
        return 0;
    }

    if (pSnapshotResult == nullptr)
    {
        throw std::invalid_argument("\"pSnapshotResult\" may not be nullptr in \"StackSnapshotResult::WriteData(..)\".");
    }

    if (pThreadInfo == nullptr)
    {
        throw std::invalid_argument("\"pThreadInfo\" may not be nullptr in \"StackSnapshotResult::WriteData(..)\".");
    }

    *(static_cast<std::uint64_t*>(GetPointerToOffset(0))) = pSnapshotResult->GetRepresentedDurationNanoseconds();

    // !! @ToDo: for non 64 bit machines we may need to do some IFs here!
    *(static_cast<std::uint64_t*>(GetPointerToOffset(8))) = pThreadInfo->GetProfilerThreadInfoId();

    // !! @ToDo: for non 64 bit machines we may need to do some IFs here!
    *(static_cast<std::uint64_t*>(GetPointerToOffset(16))) = pSnapshotResult->GetAppDomainId();

    *(static_cast<std::uint16_t*>(GetPointerToOffset(24))) = pSnapshotResult->GetFramesCount();

    uint32_t bytesWritten = 26;
    for (int f = 0; f < pSnapshotResult->GetFramesCount(); f++)
    {
        const StackSnapshotResultFrameInfo& frameInfo = pSnapshotResult->GetFrameAtIndex(f);

        //// --- This is a temporary hack/workaroud while we are working on off-thread symbol resolution when called from managed.
        ////     It will perform the resultion so that the result ends up in the cache.
        // StackFrameInfo* unusedResult;
        // SymbolsResolver::GetSingletonInstance()->ResolveStackFrameSymbols(frameInfo, &unusedResult);
        //// ---

        frameInfo.ToCompactRepresentation(static_cast<StackFrameCodeKind*>(GetPointerToOffset(bytesWritten)),
                                          static_cast<std::uint64_t*>(GetPointerToOffset(bytesWritten + 1)));
        bytesWritten += 9;
    }

    return bytesWritten;
}

std::uint32_t StackSnapshotResult::GetUsedBytesCount(void) const
{
    if (IsValid())
    {
        return GetRequiredBufferBytesForFramesCount(GetFramesCount());
    }
    else
    {
        return 0;
    }
}
