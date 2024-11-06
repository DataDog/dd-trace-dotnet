#pragma once

// from dotnet coreclr includes
#include "cor.h"
// end

#include "../../../../shared/src/native-src/string.h"
#include "assert.h"


class EventsParserHelper
{
public:
    // Points to the UTF16, null terminated string from the given event data buffer
    // and update the offset accordingly
    static WCHAR* ReadWideString(LPCBYTE eventData, ULONG cbEventData, ULONG* offset)
    {
        WCHAR* start = (WCHAR*)(eventData + *offset);
        size_t length = WStrLen(start);

        // Account for the null character
        *offset += (ULONG)((length + 1) * sizeof(WCHAR));

        assert(*offset <= cbEventData);
        return start;
    }

    template <typename T>
    static bool Read(T& value, LPCBYTE eventData, ULONG cbEventData, ULONG& offset)
    {
        if ((offset + sizeof(T)) > cbEventData)
        {
            return false;
        }

        memcpy(&value, (T*)(eventData + offset), sizeof(T));
        offset += sizeof(T);
        return true;
    }
};