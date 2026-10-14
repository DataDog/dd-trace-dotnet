#pragma once

#include "dd_version.g.h"

// The three-part numeric version, e.g. "3.56.0".
//
// This deliberately never carries the -dev suffix. Two things depend on that:
//   - Instrumentation.cs compares GetNativeTracerVersion() (which the native profiler IL-rewrites
//     to this value) against TracerConstants.ThreePartVersion for an exact string match.
//   - generated_calltargets.g.cpp builds the managed assembly's strong name by concatenating
//     "Datadog.Trace, Version=" + PROFILER_VERSION + ".0, Culture=...", which only produces a
//     well-formed AssemblyRef for a bare numeric version.
constexpr auto PROFILER_VERSION = DD_THREE_PART_VERSION_STR;

// The full semantic version, e.g. "3.56.0" or "3.56.0-dev.g1a2b3c4d". Use this anywhere the
// version is reported rather than compared - logs, telemetry payloads and version resources - so
// that a dev build can be identified from a customer's logs or from a DLL's file properties.
constexpr auto PROFILER_SEMVER = DD_SEMVER_STR;
