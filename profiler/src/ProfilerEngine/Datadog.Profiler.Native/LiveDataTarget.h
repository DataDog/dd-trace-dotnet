// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

// Forward declaration (defined by clrdata.h, which is isolated inside LiveDataTarget.cpp).
struct ICLRDataTarget;

namespace dac
{
// Creates an ICLRDataTarget over the CURRENT live process for use with CLRDataCreateInstance.
// ReadVirtual reads this process' own memory under the same SEH/SIGSEGV fault guard as
// InProcessMemoryReader; thread/TLS/context methods return E_NOTIMPL (not needed for heap
// enumeration). runtimeModuleBase is the load address of the runtime module (coreclr/clr), returned
// from GetImageBase so the DAC can locate its data table.
//
// The returned object starts with a refcount of 1; the caller owns it and must Release() it
// (the DAC also AddRef/Releases it). Returns nullptr on failure.
ICLRDataTarget* CreateLiveDataTarget(uint64_t runtimeModuleBase);
} // namespace dac
