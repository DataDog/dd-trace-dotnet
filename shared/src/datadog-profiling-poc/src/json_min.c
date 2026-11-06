#include "json_min.h"

#include <stdio.h>
#include <string.h>

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

// ---- validator (recursive descent, no allocation) ----

#define JSON_MAX_DEPTH 128

typedef struct {
    const char* p;
    const char* end;
} json_cursor;

static void skip_ws(json_cursor* c)
{
    while (c->p < c->end && (*c->p == ' ' || *c->p == '\t' || *c->p == '\n' || *c->p == '\r'))
    {
        c->p++;
    }
}

static bool parse_value(json_cursor* c, int depth);

static bool parse_literal(json_cursor* c, const char* lit)
{
    size_t len = strlen(lit);
    if ((size_t)(c->end - c->p) < len || memcmp(c->p, lit, len) != 0)
    {
        return false;
    }
    c->p += len;
    return true;
}

static bool is_hex(char ch)
{
    return (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
}

static bool parse_string(json_cursor* c)
{
    if (c->p >= c->end || *c->p != '"')
    {
        return false;
    }
    c->p++;
    while (c->p < c->end)
    {
        unsigned char ch = (unsigned char)*c->p;
        if (ch == '"')
        {
            c->p++;
            return true;
        }
        if (ch < 0x20)
        {
            return false;
        }
        if (ch == '\\')
        {
            c->p++;
            if (c->p >= c->end)
            {
                return false;
            }
            switch (*c->p)
            {
                case '"':
                case '\\':
                case '/':
                case 'b':
                case 'f':
                case 'n':
                case 'r':
                case 't':
                    c->p++;
                    break;
                case 'u':
                    c->p++;
                    for (int i = 0; i < 4; i++)
                    {
                        if (c->p >= c->end || !is_hex(*c->p))
                        {
                            return false;
                        }
                        c->p++;
                    }
                    break;
                default:
                    return false;
            }
            continue;
        }
        c->p++;
    }
    return false;
}

static bool parse_digits(json_cursor* c)
{
    const char* start = c->p;
    while (c->p < c->end && *c->p >= '0' && *c->p <= '9')
    {
        c->p++;
    }
    return c->p > start;
}

static bool parse_number(json_cursor* c)
{
    if (c->p < c->end && *c->p == '-')
    {
        c->p++;
    }
    if (c->p < c->end && *c->p == '0')
    {
        c->p++;
    }
    else if (!parse_digits(c))
    {
        return false;
    }
    if (c->p < c->end && *c->p == '.')
    {
        c->p++;
        if (!parse_digits(c))
        {
            return false;
        }
    }
    if (c->p < c->end && (*c->p == 'e' || *c->p == 'E'))
    {
        c->p++;
        if (c->p < c->end && (*c->p == '+' || *c->p == '-'))
        {
            c->p++;
        }
        if (!parse_digits(c))
        {
            return false;
        }
    }
    return true;
}

static bool parse_container(json_cursor* c, int depth, char close, bool is_object)
{
    c->p++; // '{' or '['
    skip_ws(c);
    if (c->p < c->end && *c->p == close)
    {
        c->p++;
        return true;
    }
    for (;;)
    {
        if (is_object)
        {
            skip_ws(c);
            if (!parse_string(c))
            {
                return false;
            }
            skip_ws(c);
            if (c->p >= c->end || *c->p != ':')
            {
                return false;
            }
            c->p++;
        }
        if (!parse_value(c, depth + 1))
        {
            return false;
        }
        skip_ws(c);
        if (c->p >= c->end)
        {
            return false;
        }
        if (*c->p == ',')
        {
            c->p++;
            continue;
        }
        if (*c->p == close)
        {
            c->p++;
            return true;
        }
        return false;
    }
}

static bool parse_value(json_cursor* c, int depth)
{
    if (depth > JSON_MAX_DEPTH)
    {
        return false;
    }
    skip_ws(c);
    if (c->p >= c->end)
    {
        return false;
    }
    switch (*c->p)
    {
        case '{':
            return parse_container(c, depth, '}', true);
        case '[':
            return parse_container(c, depth, ']', false);
        case '"':
            return parse_string(c);
        case 't':
            return parse_literal(c, "true");
        case 'f':
            return parse_literal(c, "false");
        case 'n':
            return parse_literal(c, "null");
        default:
            return parse_number(c);
    }
}

bool ddog__json_is_valid(ddog_charslice value)
{
    if (value.len == 0 || value.ptr == NULL)
    {
        return false;
    }
    json_cursor c;
    c.p = value.ptr;
    c.end = value.ptr + value.len;
    if (!parse_value(&c, 0))
    {
        return false;
    }
    skip_ws(&c);
    return c.p == c.end;
}

// ---- event tail ----

static bool append_cstr(ddog__buf* buf, const char* s)
{
    return ddog__buf_append(buf, s, strlen(s));
}

bool ddog__json_append_event_tail(ddog__buf* buf, const ddog_prof_endpoint_count_owned* endpoint_counts,
                                  size_t endpoint_counts_len, const ddog_charslice* process_tags,
                                  const ddog_charslice* internal_json, const ddog_charslice* info_json)
{
    // "endpoint_counts": map of endpoint -> count, or null when empty
    if (!append_cstr(buf, ",\"endpoint_counts\":"))
    {
        return false;
    }
    if (endpoint_counts_len == 0)
    {
        if (!append_cstr(buf, "null"))
        {
            return false;
        }
    }
    else
    {
        if (!ddog__buf_append_byte(buf, '{'))
        {
            return false;
        }
        for (size_t i = 0; i < endpoint_counts_len; i++)
        {
            char number[32];
            int n = snprintf(number, sizeof(number), "%lld", (long long)endpoint_counts[i].value);
            if ((i > 0 && !ddog__buf_append_byte(buf, ',')) ||
                !ddog__json_append_escaped_string(buf, endpoint_counts[i].endpoint) ||
                !ddog__buf_append_byte(buf, ':') || n < 0 || !ddog__buf_append(buf, number, (size_t)n))
            {
                return false;
            }
        }
        if (!ddog__buf_append_byte(buf, '}'))
        {
            return false;
        }
    }

    // "process_tags": string, or null when absent/empty
    if (!append_cstr(buf, ",\"process_tags\":"))
    {
        return false;
    }
    if (process_tags != NULL && process_tags->len > 0)
    {
        if (!ddog__json_append_escaped_string(buf, *process_tags))
        {
            return false;
        }
    }
    else if (!append_cstr(buf, "null"))
    {
        return false;
    }

    // "internal" / "info": the caller's documents verbatim, {} when absent
    if (!append_cstr(buf, ",\"internal\":"))
    {
        return false;
    }
    if (internal_json != NULL ? !ddog__buf_append(buf, internal_json->ptr, internal_json->len) : !append_cstr(buf, "{}"))
    {
        return false;
    }
    if (!append_cstr(buf, ",\"info\":"))
    {
        return false;
    }
    if (info_json != NULL ? !ddog__buf_append(buf, info_json->ptr, info_json->len) : !append_cstr(buf, "{}"))
    {
        return false;
    }

    return ddog__buf_append_byte(buf, '}');
}

bool ddog__json_append_attachments(ddog__buf* buf, const ddog_prof_exporter_file* files_to_compress,
                                   size_t files_to_compress_len, const char* profile_name)
{
    if (!ddog__buf_append_byte(buf, '['))
    {
        return false;
    }
    for (size_t i = 0; i < files_to_compress_len; i++)
    {
        if (!ddog__json_append_escaped_string(buf, files_to_compress[i].name) || !ddog__buf_append_byte(buf, ','))
        {
            return false;
        }
    }
    ddog_charslice profile = {profile_name, strlen(profile_name)};
    return ddog__json_append_escaped_string(buf, profile) && ddog__buf_append_byte(buf, ']');
}
