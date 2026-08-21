// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string_view>

// Static synthetic frame strings for the memory-breakdown flamegraph. Same "|lm: |ns: |ct: |cg:
// |fn:<name> |fg: |sg:" encoding as GCBaseRawSample. Frames are emitted leaf-first
// (leaf -> group -> Root), matching the pprof/libdatadog convention where locations[0] is the leaf
// and the last location is the root.
//
// Dynamic frames (per-module / per-file) are built at runtime into the provider's backing store; only
// the fixed levels live here.
namespace membreakdown
{
// Module tag shown as the pseudo-"module" column of every synthetic frame.
inline constexpr std::string_view Module = "Memory";

// root
inline constexpr std::string_view Root = "|lm: |ns: |ct: |cg: |fn:Process Memory |fg: |sg:";

// groups
inline constexpr std::string_view Managed = "|lm: |ns: |ct: |cg: |fn:Managed Heap (GC) |fg: |sg:";
inline constexpr std::string_view ClrNative = "|lm: |ns: |ct: |cg: |fn:CLR Native |fg: |sg:";
inline constexpr std::string_view Modules = "|lm: |ns: |ct: |cg: |fn:Modules (Images) |fg: |sg:";
inline constexpr std::string_view MappedFiles = "|lm: |ns: |ct: |cg: |fn:Mapped Files |fg: |sg:";
inline constexpr std::string_view PrivateMem = "|lm: |ns: |ct: |cg: |fn:Native Heap / Private |fg: |sg:";
inline constexpr std::string_view Stacks = "|lm: |ns: |ct: |cg: |fn:Thread Stacks |fg: |sg:";
inline constexpr std::string_view ReservedMem = "|lm: |ns: |ct: |cg: |fn:Reserved / Free |fg: |sg:";

// managed leaves (by generation / kind group)
inline constexpr std::string_view Gen0 = "|lm: |ns: |ct: |cg: |fn:gen0 |fg: |sg:";
inline constexpr std::string_view Gen1 = "|lm: |ns: |ct: |cg: |fn:gen1 |fg: |sg:";
inline constexpr std::string_view Gen2 = "|lm: |ns: |ct: |cg: |fn:gen2 |fg: |sg:";
inline constexpr std::string_view Loh = "|lm: |ns: |ct: |cg: |fn:LOH |fg: |sg:";
inline constexpr std::string_view Poh = "|lm: |ns: |ct: |cg: |fn:POH |fg: |sg:";
inline constexpr std::string_view GcHeap = "|lm: |ns: |ct: |cg: |fn:GC Heap |fg: |sg:";
inline constexpr std::string_view NonGc = "|lm: |ns: |ct: |cg: |fn:Frozen / NonGC |fg: |sg:";
inline constexpr std::string_view GcFree = "|lm: |ns: |ct: |cg: |fn:Free / Reserve |fg: |sg:";
inline constexpr std::string_view GcBook = "|lm: |ns: |ct: |cg: |fn:Bookkeeping & Handles |fg: |sg:";

// clr-native leaves (by group)
inline constexpr std::string_view Code = "|lm: |ns: |ct: |cg: |fn:Code (JIT) |fg: |sg:";
inline constexpr std::string_view Loader = "|lm: |ns: |ct: |cg: |fn:Loader |fg: |sg:";
inline constexpr std::string_view Vsd = "|lm: |ns: |ct: |cg: |fn:Virtual Stub Dispatch |fg: |sg:";
} // namespace membreakdown
