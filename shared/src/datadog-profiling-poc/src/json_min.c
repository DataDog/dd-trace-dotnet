#include "json_min.h"

#include <stdio.h>

bool ddog__json_append_escaped_string(ddog__buf* buf, ddog_charslice value)
{
    if (!ddog__buf_append_byte(buf, '"'))
    {
        return false;
    }

    for (size_t i = 0; i < value.len; i++)
    {
        unsigned char c = (unsigned char)value.ptr[i];
        switch (c)
        {
            case '"':
                if (!ddog__buf_append(buf, "\\\"", 2))
                {
                    return false;
                }
                break;
            case '\\':
                if (!ddog__buf_append(buf, "\\\\", 2))
                {
                    return false;
                }
                break;
            case '\n':
                if (!ddog__buf_append(buf, "\\n", 2))
                {
                    return false;
                }
                break;
            case '\r':
                if (!ddog__buf_append(buf, "\\r", 2))
                {
                    return false;
                }
                break;
            case '\t':
                if (!ddog__buf_append(buf, "\\t", 2))
                {
                    return false;
                }
                break;
            default:
                if (c < 0x20)
                {
                    char esc[8];
                    int n = snprintf(esc, sizeof(esc), "\\u%04x", c);
                    if (n < 0 || !ddog__buf_append(buf, esc, (size_t)n))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!ddog__buf_append_byte(buf, c))
                    {
                        return false;
                    }
                }
        }
    }

    return ddog__buf_append_byte(buf, '"');
}
