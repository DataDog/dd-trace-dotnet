#pragma once

#include "dd_version.h"

// The bare numeric three-part version (no suffix, no revision), for logs, telemetry, and IAST's filtering of
// aspects by their (three-part) minimum version. Anything that builds the managed assembly's strong name, or
// that is compared against the managed assembly's version (such as Instrumentation.GetNativeTracerVersion()),
// must use DD_FOUR_PART_VERSION_STR instead.
constexpr auto PROFILER_VERSION = DD_THREE_PART_VERSION_STR;
