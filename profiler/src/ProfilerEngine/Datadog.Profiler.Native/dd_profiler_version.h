// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// Previously declared its own copy of PROFILER_VERSION, which had to be kept in lock-step with the
// tracer's by the version-bump tooling. Both now come from the same generated header.
#include "../../../../shared/src/native-src/version.h"
