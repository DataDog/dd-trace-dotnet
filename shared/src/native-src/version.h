#pragma once

#include "dd_version.h"

// This must stay a bare numeric version (no suffix):
//   - Instrumentation.cs compares GetNativeTracerVersion() (which the native profiler IL-rewrites
//     to this value) against TracerConstants.ThreePartVersion for an exact string match.
//   - generated_calltargets.g.cpp builds the managed assembly's strong name by concatenating
//     "Datadog.Trace, Version=" + PROFILER_VERSION + ".0, Culture=...".
constexpr auto PROFILER_VERSION = DD_THREE_PART_VERSION_STR;
