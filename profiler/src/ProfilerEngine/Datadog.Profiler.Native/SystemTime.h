// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// Windows only helpers to deal with time

#ifdef _WINDOWS
#include "stdint.h"
#include "Windows.h"

uint64_t GetTotalMilliseconds(SYSTEMTIME time);
uint64_t GetTotalMilliseconds(FILETIME fileTime);

#endif