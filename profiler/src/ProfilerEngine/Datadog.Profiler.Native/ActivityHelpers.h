// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
// end


/// <summary>
/// The encoding for a list of numbers used to make Activity  Guids.   Basically
/// we operate on nibbles (which are nice because they show up as hex digits).  The
/// list is ended with a end nibble (0) and depending on the nibble value (Below)
/// the value is either encoded into nibble itself or it can spill over into the
/// bytes that follow.
/// </summary>
enum class NumberListCodes : uint8_t
{
    End = 0x0,             // ends the list.   No valid value has this prefix.
    LastImmediateValue = 0xA,
    PrefixCode = 0xB,
    MultiByte1 = 0xC,   // 1 byte follows.  If this Nibble is in the high bits, it the high bits of the number are stored in the low nibble.
    // commented out because the code does not explicitly reference the names (but they are logically defined).
    // MultiByte2 = 0xD,   // 2 bytes follow (we don't bother with the nibble optimzation
    // MultiByte3 = 0xE,   // 3 bytes follow (we don't bother with the nibble optimzation
    // MultiByte4 = 0xF,   // 4 bytes follow (we don't bother with the nibble optimzation
};

// Look at StartStopActivityComputer.cs in Perfview for the original managed implementation
class ActivityHelpers
{
public:
    static bool IsActivityPath(const GUID* pActivityId, int processID);

    // could be used in log / checks
    static int GetProcessID(const GUID* pActivityId);

    // The ActivityID GUID encodes the process ID, the root and request ID
    // The root is stored in the high 4 bytes and the request ID in the lower 4 bytes
    // That way, it could be used as a key in a per request hash table
    static uint64_t GetActivityKey(const GUID* pActivityId, int processID);

private:
    static uint64_t buildKey(uint32_t high, uint32_t low);
};