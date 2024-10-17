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
enum NumberListCodes : byte
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
class ActivityIdHelpers
{
public:
    static bool IsActivityPath(const GUID* pActivityId, int processID)
    {
        uint32_t* uintPtr = (uint32_t*)pActivityId;

        uint32_t sum = uintPtr[0] + uintPtr[1] + uintPtr[2] + 0x599D99AD;
        if (processID == 0)
        {
            // We guess that the process ID is < 20 bits and because it was xored
            // with the lower bits, the upper 12 bits should be independent of the
            // particular process, so we can at least confirm that the upper bits
            // match.
            return ((sum & 0xFFF00000) == (uintPtr[3] & 0xFFF00000));
        }

        if ((sum ^ (uint32_t)processID) == uintPtr[3])  // This is the new style
        {
            return true;
        }

        return (sum == uintPtr[3]);         // THis is old style where we don't make the ID unique machine wide.
    }

    // could be used in log / checks
    static int ActivityPathProcessID(const GUID* pActivityId)
    {
        uint32_t* uintPtr = (uint32_t*)pActivityId;
        uint32_t sum = uintPtr[0] + uintPtr[1] + uintPtr[2] + 0x599D99AD;
        return (int)(sum ^ uintPtr[3]);
    }

    // The ActivityID GUID encodes the process ID, the root and request ID
    // The root is stored in the high 4 bytes and the request ID in the lower 4 bytes
    // That way, it could be used as a key in a per request hash table
    static uint64_t GetActivityKey(const GUID* pActivityId, int processID)
    {
        if (!IsActivityPath(pActivityId, processID))
        {
            return 0;
        }

        uint32_t highPart = 0;
        uint32_t lowPart = 0;

        byte* bytePtr = (byte*)pActivityId;
        byte* endPtr = bytePtr + 12;
        while (bytePtr < endPtr)
        {
            uint32_t nibble = (uint32_t)(*bytePtr >> 4);
            bool secondNibble = false;              // are we reading the second nibble (low order bits) of the byte.
        NextNibble:
            if (nibble == (uint32_t)NumberListCodes::End)
            {
                break;
            }

            if (nibble <= (uint32_t)NumberListCodes::LastImmediateValue)
            {
                if (highPart == 0)
                {
                    highPart = nibble;
                }
                else if (lowPart == 0)
                {
                    lowPart = nibble;
                    return buildKey(highPart, lowPart);
                }
                else
                {
                }

                if (!secondNibble)
                {
                    nibble = (uint32_t)(*bytePtr & 0xF);
                    secondNibble = true;
                    goto NextNibble;
                }
                // We read the second nibble so we move on to the next byte.
                bytePtr++;
                continue;
            }
            else if (nibble == (uint32_t)NumberListCodes::PrefixCode)
            {
                // This are the prefix codes.   If the next nibble is MultiByte, then this is an overflow ID.
                // we we denote with a $ instead of a / separator.

                // Read the next nibble.
                if (!secondNibble)
                {
                    nibble = (uint32_t)(*bytePtr & 0xF);
                }
                else
                {
                    bytePtr++;
                    if (endPtr <= bytePtr)
                    {
                        break;
                    }

                    nibble = (uint32_t)(*bytePtr >> 4);
                }

                if (nibble < (uint32_t)NumberListCodes::MultiByte1)
                {
                    // If the nibble is less than MultiByte we have not defined what that means
                    // For now we simply give up, and stop parsing.  We could add more cases here...
                    return 0;
                }
                // If we get here we have a overflow ID, which is just like a normal ID
                // Fall into the Multi-byte decode case.
            }

            if ((uint32_t)NumberListCodes::MultiByte1 > nibble)
            {
                return 0;
            }

            // At this point we are decoding a multi-byte number, either a normal number or a
            // At this point we are byte oriented, we are fetching the number as a stream of bytes.
            uint32_t numBytes = nibble - (uint32_t)NumberListCodes::MultiByte1;

            uint32_t value = 0;
            if (!secondNibble)
            {
                value = (uint32_t)(*bytePtr & 0xF);
            }

            bytePtr++;       // Advance to the value bytes

            numBytes++;     // Now numBytes is 1-4 and represents the number of bytes to read.
            if (endPtr < bytePtr + numBytes)
            {
                break;
            }

            // Compute the number (little endian) (thus backwards).
            for (int i = (int)numBytes - 1; 0 <= i; --i)
            {
                value = (value << 8) + bytePtr[i];
            }

            if (highPart == 0)
            {
                highPart = nibble;
            }
            else if (lowPart == 0)
            {
                lowPart = nibble;
                return buildKey(highPart, lowPart);
            }

            bytePtr += numBytes;        // Advance past the bytes.
        }

        return 0;
    }

private:
    static inline uint64_t buildKey(uint32_t high, uint32_t low)
    {
        return (static_cast<uint64_t>(high) << 32) | static_cast<uint64_t>(low);
    }
};

