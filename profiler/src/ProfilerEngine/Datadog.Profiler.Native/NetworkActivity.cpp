// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NetworkActivity.h"

NetworkActivity::NetworkActivity()
{
    High = 0;
    Middle = 0;
    Low = 0;
}

// from https://www.boost.org/doc/libs/1_86_0/libs/container_hash/doc/html/hash.html#notes_hash_combine
size_t NetworkActivity::mix32(uint32_t x)
{
    x ^= x >> 16;
    x *= 0x21f0aaad;
    x ^= x >> 15;
    x *= 0x735a2d97;
    x ^= x >> 15;

    return x;
}

void NetworkActivity::hash_combine(size_t& seed, uint32_t v)
{
    seed ^= mix32(static_cast<uint32_t>(seed) + 0x9e3779b9 + v);
}

size_t NetworkActivity::get_hash_code() const
{
    size_t seed = 0;

    hash_combine(seed, High);
    hash_combine(seed, Middle);
    hash_combine(seed, Low);
    return seed;
}


// ----------------------------------------------------------------------------------------------------------------------------------------
// from https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/ActivityTracker.cs
//

enum NumberListCodes : uint8_t
{
    End = 0x0,
    LastImmediateValue = 0xA,
    PrefixCode = 0xB,
    MultiByte1 = 0xC,
};

/// <summary>
/// Write a single Nible 'value' (must be 0-15) to the byte buffer represented by *ptr.
/// Will not go past 'endPtr'.  Also it assumes that we never write 0 so we can detect
/// whether a nibble has already been written to ptr  because it will be nonzero.
/// Thus if it is non-zero it adds to the current byte, otherwise it advances and writes
/// the new byte (in the high bits) of the next byte.
/// </summary>
void NetworkActivity::WriteNibble(uint8_t*& ptr, uint8_t* endPtr, uint32_t value)
{
    if (*ptr != 0)
        *ptr++ |= (uint8_t)value;
    else
        *ptr = (uint8_t)(value << 4);
}

/// Add the activity id 'id' to the output Guid 'outPtr' starting at the offset 'whereToAddId'
/// Thus if this number is 6 that is where 'id' will be added.    This will return 13 (12
/// is the maximum number of bytes that fit in a GUID) if the path did not fit.
/// If 'overflow' is true, then the number is encoded as an 'overflow number (which has a
/// special (longer prefix) that indicates that this ID is allocated differently
int NetworkActivity::AddIdToGuid(uint8_t* outPtr, int whereToAddId, uint32_t id, bool overflow)
{
    uint8_t* ptr = (uint8_t*)outPtr;
    uint8_t* endPtr = ptr + 12;
    ptr += whereToAddId;
    if (endPtr <= ptr)
        return 13;                // 12 means we might exactly fit, 13 means we definitely did not fit

    if (0 < id && id <= (uint32_t)NumberListCodes::LastImmediateValue && !overflow)
    {
        WriteNibble(ptr, endPtr, id);
    }
    else
    {
        uint32_t len = 4;
        if (id <= 0xFF)
            len = 1;
        else if (id <= 0xFFFF)
            len = 2;
        else if (id <= 0xFFFFFF)
            len = 3;

        if (overflow)
        {
            if (endPtr <= ptr + 2)        // I need at least 2 bytes
                return 13;

            // Write out the prefix code nibble and the length nibble
            WriteNibble(ptr, endPtr, (uint32_t)NumberListCodes::PrefixCode);
        }
        // The rest is the same for overflow and non-overflow case
        WriteNibble(ptr, endPtr, (uint32_t)NumberListCodes::MultiByte1 + (len - 1));

        // Do we have an odd nibble?   If so flush it or use it for the 12 byte case.
        if (ptr < endPtr && *ptr != 0)
        {
            // If the value < 4096 we can use the nibble we are otherwise just outputting as padding.
            if (id < 4096)
            {
                // Indicate this is a 1 byte multicode with 4 high order bits in the lower nibble.
                *ptr = (uint8_t)(((uint32_t)NumberListCodes::MultiByte1 << 4) + (id >> 8));

                // FIX: it means that we now just need 1 byte to store the id instead of 2 as computed before
                //      --> the previous line is overwriting the "NumberListCodes.MultiByte1 + (len - 1)" value
                //          with NumberListCodes.MultiByte1 followed by the 4 high order bits of the id
                //      the 00 byte was due to the fact that the "id >>= 8;" line was leading to id = 0
                //      that was stored in the additional unneeded byte
                len = 1;

                id &= 0xFF;     // Now we only want the low order bits.
            }
            ptr++;
        }

        // Write out the bytes.
        while (0 < len)
        {
            if (endPtr <= ptr)
            {
                ptr++;        // Indicate that we have overflowed
                break;
            }
            *ptr++ = (uint8_t)id;
            id >>= 8;
            --len;
        }
    }

    return (int)(ptr - ((uint8_t*)outPtr));
}

#define MAX_NIBBLE_ELEMENTS 24  // we don't expect more than 24 elements in the path (12 bytes = 24 nibbles at most)

bool NetworkActivity::GetRootActivity(LPCGUID pActivityGuid, NetworkActivity& activity, bool isRoot)
{
    // we need to get each sub activity up to the last one that should not be part the root activity
    // ex: 1/2/3 --> 1/2 is the root activity
    // Due to a bug in the encoding, we need to decode and then encode instead of directly copy the 12 bytes
    // of the GUID. The other solution would be to duplicate the encoding bug when the last element is not included.
    uint32_t elements[MAX_NIBBLE_ELEMENTS] = { 0 };  // each element of the path is stored here
    uint8_t currentElement = 0;
    bool isOverflow = false;

    // decode the activity into path elements
    uint8_t* bytePtr = (uint8_t*)pActivityGuid;
    uint8_t* endPtr = bytePtr + 12;
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
            if (currentElement >= MAX_NIBBLE_ELEMENTS)
            {
                break;
            }
            elements[currentElement++] = nibble;

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
                return false;
            }
            // If we get here we have a overflow ID, which is just like a normal ID but the separator is $
            isOverflow = true;
            // Fall into the Multi-byte decode case.
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
            break;  // TODO: should we return false here?
        }

        // Compute the number (little endian) (thus backwards).
        for (int i = (int)numBytes - 1; 0 <= i; --i)
        {
            value = (value << 8) + bytePtr[i];
        }

        if (currentElement >= MAX_NIBBLE_ELEMENTS)
        {
            break;
        }
        elements[currentElement++] = value;

        bytePtr += numBytes;        // Advance past the bytes.

        // FIX: there is a special case for a value < 4096/0xFFF where the encoder made a mistake
        // It is encoded with 1 nibble + 1 byte + 1 byte that contains 0 (hence stopping the parsing)
        if ((value > 0xFF) && (value <= 0xFFF) && (bytePtr + 1 < endPtr) && (bytePtr[0] == 0) && (bytePtr[1] != 0))
        {
            bytePtr++;  // Advance past the 00 byte
        }
    }

    // encode the path into the network activity
    bytePtr = (uint8_t*)&activity;
    int activityPathGuidOffsetStart = 0;

    // skip the last element if needed
    if (!isRoot)
    {
        currentElement--;
    }

    for (size_t i = 0; i < currentElement; i++)
    {
        activityPathGuidOffsetStart = AddIdToGuid(bytePtr, activityPathGuidOffsetStart, elements[i]);
    }

    return true;
}
