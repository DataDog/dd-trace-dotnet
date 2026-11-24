#pragma once

#include "dd_version.h"

// The semver (e.g. "1.2.3" or "1.2.3-prerelease"): the version to report in logs, telemetry, profiles and
// crash reports. Instrumentation.cs compares GetNativeTracerVersion() (which the native tracer IL-rewrites
// to this value) against TracerConstants.SemanticVersion, so a prerelease and the release with the same X.Y.Z are
// detected as a mismatch.
//
// Never use it to build Datadog.Trace's strong name, or to parse a numeric version: use
// DD_FOUR_PART_VERSION_STR or DD_THREE_PART_VERSION_STR instead.
constexpr auto PROFILER_VERSION = DD_SEMANTIC_VERSION_STR;
