// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

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
    static WCHAR* ReadWideString(LPCBYTE pEventData, ULONG cbEventData, ULONG* offset)
    {
        WCHAR* start = (WCHAR*)(pEventData + *offset);
        size_t length = WStrLen(start);

        // Account for the null character
        *offset += (ULONG)((length + 1) * sizeof(WCHAR));

        assert(*offset <= cbEventData);
        return start;
    }

    template <typename T>
    static bool Read(T& value, LPCBYTE pEventData, ULONG cbEventData, ULONG& offset)
    {
        if ((offset + sizeof(T)) > cbEventData)
        {
            return false;
        }

        memcpy(&value, (T*)(pEventData + offset), sizeof(T));
        offset += sizeof(T);
        return true;
    }
};