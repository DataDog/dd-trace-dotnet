// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

/// <summary>
/// Very important: These values MUST be kept in sync with the managed definition!
/// </summary>
enum class StackFrameCodeKind : std::uint8_t
{
    Unknown = 0,
    NotDetermined = 2,
    ClrManaged = 3,
    ClrNative = 4,
    UserNative = 5,
    UnknownNative = 6,
    Kernel = 7,
    MultipleMixed = 8,
    Dummy = 9,
};