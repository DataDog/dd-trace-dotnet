// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "unknwn.h"
#include <cstdint>
#include <mutex>
#include <winerror.h>

/*
   TL;DR When returning a boolean value to the managed part, we must use a C BOOL type instead of C++ bool type.

   Boolean type has different size/representation in C (4 bytes), C++ (1 byte) and C# (1 byte).

   For example:
   [DllImport(dllName: NativeProfilerEngineLibName_x86, EntryPoint = "StackSnapshotsBufferManager_TryCompleteCurrentWriteSegment",
              CallingConvention = CallingConvention.StdCall)]
   private static extern bool StackSnapshotsBufferManager_TryCompleteCurrentWriteSegment_x86();

   If not specified otherwise, the marshaller will consider the boolean type as a Win32 BOOL type (4 bytes). It will read the register RAX/EAX
   and convert its content to the appropriate C# bool value (false == 0, true otherwise).

   On the native side:
   extern "C" bool __stdcall StackSnapshotsBufferManager_TryCompleteCurrentWriteSegment();

   In this case, the function return a C++ bool value (1 byte). So only the AL register will be set as the return value. However, he AL
   is lowest byte of the RAX/EAX register.
   This means that the RAX/EAX register will have only its first byte changed: the other bytes will remain unchanged with probably
   garbage definitely not 0.

   In this case, if StackSnapshotsBufferManager_TryCompleteCurrentWriteSegment returns false, the managed part may see the return value
   as true.

 */

extern "C" void* __stdcall GetNativeProfilerIsReadyPtr();

extern "C" void* __stdcall GetPointerToNativeTraceContext();

extern "C" void __stdcall SetApplicationInfoForAppDomain(const char* runtimeId, const char* serviceName, const char* environment, const char* version);