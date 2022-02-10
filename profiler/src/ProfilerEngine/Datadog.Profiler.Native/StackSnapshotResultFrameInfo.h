// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "StackFrameCodeKind.h"
#include "StackFrameInfo.h"

struct StackSnapshotResultFrameInfo
{
public:
    inline StackSnapshotResultFrameInfo() :
        StackSnapshotResultFrameInfo(StackFrameCodeKind::Unknown, static_cast<FunctionID>(0), static_cast<UINT_PTR>(0), 0)
    {
    }

    inline StackSnapshotResultFrameInfo(const StackSnapshotResultFrameInfo& otherFrame) :
        StackSnapshotResultFrameInfo(otherFrame.GetCodeKind(), otherFrame.GetClrFunctionId(), otherFrame.GetNativeIP(), otherFrame.GetModuleHandle())
    {
    }

    // frameInfoCode is the  ClrFunctionId, the NativeIP OR the ModuleHandle, depending on codeKind
    inline StackSnapshotResultFrameInfo(StackFrameCodeKind codeKind, std::uint64_t frameInfoCode) :
        StackSnapshotResultFrameInfo()
    {
        FromCompactRepresentation(codeKind, frameInfoCode);
    }

    inline StackSnapshotResultFrameInfo(StackFrameCodeKind codeKind, FunctionID clrFunctionId, UINT_PTR nativeIP, std::uint64_t moduleHandle) :
        _codeKind{codeKind},
        _clrFunctionId{clrFunctionId},
        _nativeIP{nativeIP},
        _moduleHandle{moduleHandle}
    {
    }

    inline StackSnapshotResultFrameInfo& operator=(const StackSnapshotResultFrameInfo& otherFrame)
    {
        _codeKind = otherFrame.GetCodeKind();
        _clrFunctionId = otherFrame.GetClrFunctionId();
        _nativeIP = otherFrame.GetNativeIP();
        _moduleHandle = otherFrame.GetModuleHandle();
        return *this;
    }

    inline void Set(StackFrameCodeKind codeKind, FunctionID clrFunctionId, UINT_PTR nativeIP, std::uint64_t moduleHandle)
    {
        _codeKind = codeKind;
        _clrFunctionId = clrFunctionId;
        _nativeIP = nativeIP;
        _moduleHandle = moduleHandle;
    }

    inline void Reset(void)
    {
        _codeKind = StackFrameCodeKind::Unknown;
        _clrFunctionId = static_cast<FunctionID>(0);
        _nativeIP = static_cast<UINT_PTR>(0);
        _moduleHandle = 0;
    }

    inline StackFrameCodeKind GetCodeKind() const
    {
        return _codeKind;
    }
    inline FunctionID GetClrFunctionId() const
    {
        return _clrFunctionId;
    }
    inline UINT_PTR GetNativeIP() const
    {
        return _nativeIP;
    }
    inline std::uint64_t GetModuleHandle() const
    {
        return _moduleHandle;
    }

    /// <summary> We store ClrFunctionId, NativeIP and ModuleHandle, because we foresee scenarios where any combination are relevant.
    /// However, until that occurs, we will only marshal one of those three values for use by the managed engine. </summary>
    /// <param name="codeKind">CodeKind</param>
    /// <param name="frameInfoCode">ClrFunctionId OR the NativeIP OR the ModuleHandle, depending on CodeKind</param>
    inline void ToCompactRepresentation(StackFrameCodeKind* pCodeKind, std::uint64_t* pFrameInfoCode) const
    {
        if (pCodeKind != nullptr)
        {
            *pCodeKind = _codeKind;
        }

        if (pFrameInfoCode != nullptr)
        {
            switch (_codeKind)
            {
                case StackFrameCodeKind::NotDetermined:
                    *pFrameInfoCode = _nativeIP;
                    break;

                case StackFrameCodeKind::ClrManaged:
                    *pFrameInfoCode = _clrFunctionId;
                    break;

                case StackFrameCodeKind::ClrNative:
                case StackFrameCodeKind::UserNative:
                case StackFrameCodeKind::Kernel:
                    *pFrameInfoCode = _nativeIP;
                    break;

                case StackFrameCodeKind::UnknownNative:
                    *pFrameInfoCode = _moduleHandle;
                    break;

                case StackFrameCodeKind::MultipleMixed:
                case StackFrameCodeKind::Dummy:
                case StackFrameCodeKind::Unknown:
                default:
                    *pFrameInfoCode = 0;
                    break;
            }
        }
    }

private:
    /// <summary>See ToCompactRepresentation() for detail.</summary>
    /// <param name="codeKind">The <c>StackFrameCodeKind</c> for this frame.</param>
    /// <param name="frameInfoCode">The ClrFunctionId, the NativeIP OR the ModuleHandle, depending on <c>codeKind</c>.</param>
    inline void FromCompactRepresentation(StackFrameCodeKind codeKind, std::uint64_t frameInfoCode)
    {
        _codeKind = codeKind;

        switch (_codeKind)
        {
            case StackFrameCodeKind::NotDetermined:
                _clrFunctionId = static_cast<FunctionID>(0);
                _nativeIP = static_cast<UINT_PTR>(frameInfoCode);
                _moduleHandle = static_cast<std::uint64_t>(0);
                break;

            case StackFrameCodeKind::ClrManaged:
                _clrFunctionId = static_cast<FunctionID>(frameInfoCode);
                _nativeIP = static_cast<UINT_PTR>(0);
                _moduleHandle = static_cast<std::uint64_t>(0);
                break;

            case StackFrameCodeKind::ClrNative:
            case StackFrameCodeKind::UserNative:
            case StackFrameCodeKind::Kernel:
                _clrFunctionId = static_cast<FunctionID>(0);
                _nativeIP = static_cast<UINT_PTR>(frameInfoCode);
                _moduleHandle = static_cast<std::uint64_t>(0);
                break;

            case StackFrameCodeKind::UnknownNative:
                _clrFunctionId = static_cast<FunctionID>(0);
                _nativeIP = static_cast<UINT_PTR>(0);
                _moduleHandle = static_cast<std::uint64_t>(frameInfoCode);
                break;

            case StackFrameCodeKind::MultipleMixed:
            case StackFrameCodeKind::Dummy:
            case StackFrameCodeKind::Unknown:
            default:
                _clrFunctionId = static_cast<FunctionID>(0);
                _nativeIP = static_cast<UINT_PTR>(0);
                _moduleHandle = static_cast<std::uint64_t>(0);
                break;
        }
    }

private:
    // We store the ClrFunctionId, the NativeIP, and the Handle to the Native Module containing the code pointed to by the IP,
    // because we foresee scenarios where some combination of any of those is relevant.
    // However, until that occurs, we will only marshal one of those three values for use by the managed engine (depending on CodeKind).
    StackFrameCodeKind _codeKind; // always 8 bits
    FunctionID _clrFunctionId;    // 64 bits on Win64
    UINT_PTR _nativeIP;           // 64 bits on Win64
    std::uint64_t _moduleHandle;  // always 64 bits (handle itself may be 32 bits only)
};