// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#if __cpp_lib_span

#include <span>

namespace shared
{
using std::span;
}

#else

#define TCB_SPAN_NAMESPACE_NAME shared
#include "tcb-span.hpp"

#endif