// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ActivityHelpers.h"

bool ActivityHelpers::IsActivityPath(const GUID* pActivityId, int processID)
{
    const uint32_t* uintPtr = reinterpret_cast<const uint32_t*>(pActivityId);

    uint32_t sum = uintPtr[0] + uintPtr[1] + uintPtr[2] + 0x599D99AD;
    if (processID == 0)
    {
        // We guess that the process ID is < 20 bits and because it was xored
        // with the lower bits, the upper 12 bits should be independent of the
        // particular process, so we can at least confirm that the upper bits
        // match.
        return ((sum & 0xFFF00000) == (uintPtr[3] & 0xFFF00000));
    }

    if ((sum ^ static_cast<uint32_t>(processID)) == uintPtr[3])  // This is the new style
    {
        return true;
    }

    return (sum == uintPtr[3]);         // THis is old style where we don't make the ID unique machine wide.
}

int ActivityHelpers::GetProcessID(const GUID* pActivityId)
{
    const uint32_t* uintPtr = reinterpret_cast<const uint32_t*>(pActivityId);
    uint32_t sum = uintPtr[0] + uintPtr[1] + uintPtr[2] + 0x599D99AD;
    return static_cast<int>(sum ^ uintPtr[3]);
}

uint64_t ActivityHelpers::GetActivityKey(const GUID* pActivityId, int processID)
{
    if (!IsActivityPath(pActivityId, processID))
    {
        return 0;
    }

    uint32_t highPart = 0;
    uint32_t lowPart = 0;

    const uint8_t* bytePtr = reinterpret_cast<const uint8_t*>(pActivityId);
    const uint8_t* endPtr = bytePtr + 12;
    while (bytePtr < endPtr)
    {
        uint32_t nibble = static_cast<uint32_t>(*bytePtr >> 4);
        bool secondNibble = false;              // are we reading the second nibble (low order bits) of the byte.
    NextNibble:
        if (nibble == static_cast<uint32_t>(NumberListCodes::End))
        {
            break;
        }

        if (nibble <= static_cast<uint32_t>(NumberListCodes::LastImmediateValue))
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
                nibble = static_cast<uint32_t>(*bytePtr & 0xF);
                secondNibble = true;
                goto NextNibble;
            }
            // We read the second nibble so we move on to the next byte.
            bytePtr++;
            continue;
        }
        else if (nibble == static_cast<uint32_t>(NumberListCodes::PrefixCode))
        {
            // This are the prefix codes.   If the next nibble is MultiByte, then this is an overflow ID.
            // we we denote with a $ instead of a / separator.

            // Read the next nibble.
            if (!secondNibble)
            {
                nibble = static_cast<uint32_t>(*bytePtr & 0xF);
            }
            else
            {
                bytePtr++;
                if (endPtr <= bytePtr)
                {
                    break;
                }

                nibble = static_cast<uint32_t>(*bytePtr >> 4);
            }

            if (nibble < static_cast<uint32_t>(NumberListCodes::MultiByte1))
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
        uint32_t numBytes = nibble - static_cast<uint32_t>(NumberListCodes::MultiByte1);

        uint32_t value = 0;
        if (!secondNibble)
        {
            value = static_cast<uint32_t>(*bytePtr & 0xF);
        }

        bytePtr++;       // Advance to the value bytes

        numBytes++;     // Now numBytes is 1-4 and represents the number of bytes to read.
        if (endPtr < bytePtr + numBytes)
        {
            break;
        }

        // Compute the number (little endian) (thus backwards).
        for (int i = static_cast<int>(numBytes) - 1; 0 <= i; --i)
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


uint64_t ActivityHelpers::buildKey(uint32_t high, uint32_t low)
{
    return (static_cast<uint64_t>(high) << 32) | static_cast<uint64_t>(low);
}
