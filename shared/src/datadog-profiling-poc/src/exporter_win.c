// Windows implementation of the exporter API - see exporter.c for the
// Linux/libcurl implementation these functions mirror. Uses WinHTTP instead
// of libcurl: WinHTTP ships in every Windows install (winhttp.dll/.lib, since
// Windows XP) and is Microsoft's documented recommendation for services and
// other non-interactive processes - https://learn.microsoft.com/en-us/windows/win32/winhttp/winhttp-vs-wininet
// ("use WinINet unless you plan to run within a service or service-like
// process") - which matches how this library gets loaded into arbitrary
// customer processes. This avoids vendoring/building libcurl on Windows
// entirely; see shared/src/datadog-profiling-poc/README.md.
//
// Only this file and exporter.c define `struct ddog_prof_exporter` - it is
// opaque to every caller (see datadog_poc/profiling.h), so the two platforms
// are free to lay it out differently. The CMakeLists.txt here excludes this
// file on Linux; the Windows .vcxproj lists this file instead of exporter.c.
//
// The portable helpers below (event.json building, tag-string building,
// RFC3339 formatting, multipart body framing) are intentionally duplicated
// from exporter.c rather than shared, to avoid touching that already-tested
// file - see README "Known simplifications".

#include "datadog_poc/profiling.h"
#include "encoded_profile.h"
#include "internal.h"
#include "json_min.h"
#include "tags.h"

#include <windows.h>
#include <winhttp.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#pragma comment(lib, "winhttp.lib")

struct ddog_prof_exporter {
    char* url;             // owned, NUL-terminated
    char* api_key;         // owned, NUL-terminated; NULL in agent mode
    char* library_name;    // owned, NUL-terminated
    char* library_version; // owned, NUL-terminated
    char* family;           // owned, NUL-terminated
    ddog_vec_tag tags;      // owned deep copy of the tags passed to _new
    uint64_t timeout_ms;
    HINTERNET session; // reused across send_blocking calls
};

static char* dup_cstr(ddog_charslice s)
{
    char* out = (char*)malloc(s.len + 1);
    if (out == NULL)
    {
        return NULL;
    }
    if (s.len > 0)
    {
        memcpy(out, s.ptr, s.len);
    }
    out[s.len] = '\0';
    return out;
}

// WinHTTP is wide-string only; every other field in this library (and in
// libcurl on the Linux side) is a plain narrow C string, so every call site
// that talks to WinHTTP converts at the boundary rather than carrying wide
// strings through the rest of the struct.
static wchar_t* narrow_to_wide(const char* s)
{
    if (s == NULL)
    {
        return NULL;
    }
    int wlen = MultiByteToWideChar(CP_UTF8, 0, s, -1, NULL, 0);
    if (wlen <= 0)
    {
        return NULL;
    }
    wchar_t* w = (wchar_t*)malloc((size_t)wlen * sizeof(wchar_t));
    if (w == NULL)
    {
        return NULL;
    }
    MultiByteToWideChar(CP_UTF8, 0, s, -1, w, wlen);
    return w;
}

ddog_error_code ddog_prof_exporter_new(ddog_charslice profiling_library_name, ddog_charslice profiling_library_version,
                                        ddog_charslice family, const ddog_vec_tag* tags, const ddog_prof_endpoint* endpoint,
                                        ddog_prof_exporter** out_exporter)
{
    if (endpoint == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "endpoint is NULL");
    }
    if (out_exporter == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "out_exporter is NULL");
    }

    // calloc zero-initializes every field, which already matches what
    // ddog_vec_tag_new()/a NULL url/api_key/session would set - so partial
    // failures below can always be cleaned up by just calling
    // ddog_prof_exporter_drop() on this, even before every field is filled in.
    struct ddog_prof_exporter* exp = (struct ddog_prof_exporter*)calloc(1, sizeof(struct ddog_prof_exporter));
    if (exp == NULL)
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to allocate exporter");
    }

    if (endpoint->kind == DDOG_POC_PROF_ENDPOINT_AGENT)
    {
        ddog_charslice base = endpoint->url_or_site;
        size_t base_len = base.len;
        if (base_len > 0 && base.ptr[base_len - 1] == '/')
        {
            base_len -= 1;
        }

        static const char* suffix = "/profiling/v1/input";
        size_t suffix_len = strlen(suffix);
        exp->url = (char*)malloc(base_len + suffix_len + 1);
        if (exp->url == NULL)
        {
            ddog_prof_exporter_drop((ddog_prof_exporter*)exp);
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to build agent url");
        }
        memcpy(exp->url, base.ptr, base_len);
        memcpy(exp->url + base_len, suffix, suffix_len);
        exp->url[base_len + suffix_len] = '\0';
    }
    else // DDOG_POC_PROF_ENDPOINT_AGENTLESS
    {
        static const char* prefix = "https://intake.profile.";
        static const char* suffix = "/api/v2/profile";
        size_t prefix_len = strlen(prefix);
        size_t suffix_len = strlen(suffix);
        ddog_charslice site = endpoint->url_or_site;
        exp->url = (char*)malloc(prefix_len + site.len + suffix_len + 1);
        if (exp->url == NULL)
        {
            ddog_prof_exporter_drop((ddog_prof_exporter*)exp);
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to build agentless url");
        }
        memcpy(exp->url, prefix, prefix_len);
        memcpy(exp->url + prefix_len, site.ptr, site.len);
        memcpy(exp->url + prefix_len + site.len, suffix, suffix_len);
        exp->url[prefix_len + site.len + suffix_len] = '\0';

        exp->api_key = dup_cstr(endpoint->api_key);
        if (exp->api_key == NULL)
        {
            ddog_prof_exporter_drop((ddog_prof_exporter*)exp);
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy api key");
        }
    }

    exp->library_name = dup_cstr(profiling_library_name);
    exp->library_version = dup_cstr(profiling_library_version);
    exp->family = dup_cstr(family);
    if (exp->library_name == NULL || exp->library_version == NULL || exp->family == NULL)
    {
        ddog_prof_exporter_drop((ddog_prof_exporter*)exp);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy exporter metadata");
    }

    if (tags != NULL)
    {
        for (size_t i = 0; i < tags->len; i++)
        {
            ddog_error_code rc = ddog_vec_tag_push(&exp->tags, tags->ptr[i].key, tags->ptr[i].value);
            if (rc != DDOG_OK)
            {
                ddog_prof_exporter_drop((ddog_prof_exporter*)exp);
                return rc;
            }
        }
    }

    exp->timeout_ms = endpoint->timeout_ms != 0 ? endpoint->timeout_ms : 3000; // matches libdatadog's Endpoint::DEFAULT_TIMEOUT

    wchar_t* agent_w = narrow_to_wide(exp->library_name);
    exp->session = WinHttpOpen(agent_w, WINHTTP_ACCESS_TYPE_DEFAULT_PROXY, WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
    free(agent_w);
    if (exp->session == NULL)
    {
        ddog_prof_exporter_drop((ddog_prof_exporter*)exp);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "WinHttpOpen failed: %lu", (unsigned long)GetLastError());
    }

    *out_exporter = (ddog_prof_exporter*)exp;
    return DDOG_OK;
}

void ddog_prof_exporter_drop(ddog_prof_exporter* exporter)
{
    struct ddog_prof_exporter* exp = (struct ddog_prof_exporter*)exporter;
    if (exp == NULL)
    {
        return;
    }
    if (exp->session != NULL)
    {
        WinHttpCloseHandle(exp->session);
    }
    free(exp->url);
    free(exp->api_key);
    free(exp->library_name);
    free(exp->library_version);
    free(exp->family);
    ddog_vec_tag_drop(&exp->tags);
    free(exp);
}

static bool append_lit(ddog__buf* out, const char* s)
{
    return ddog__buf_append(out, s, strlen(s));
}

static bool append_tag_list(const ddog_vec_tag* tags, ddog__buf* out, bool* first)
{
    for (size_t i = 0; i < tags->len; i++)
    {
        if (!*first && !ddog__buf_append_byte(out, ','))
        {
            return false;
        }
        *first = false;
        if (!ddog__buf_append(out, tags->ptr[i].key.ptr, tags->ptr[i].key.len))
        {
            return false;
        }
        if (!ddog__buf_append_byte(out, ':'))
        {
            return false;
        }
        if (!ddog__buf_append(out, tags->ptr[i].value.ptr, tags->ptr[i].value.len))
        {
            return false;
        }
    }
    return true;
}

static bool build_tags_profiler_string(const struct ddog_prof_exporter* exp, const ddog_vec_tag* additional, ddog__buf* out)
{
    bool first = true;
    if (!append_tag_list(&exp->tags, out, &first))
    {
        return false;
    }
    if (additional != NULL && !append_tag_list(additional, out, &first))
    {
        return false;
    }
    return true;
}

// Not thread-safe callers would matter (gmtime_s is), but kept as a plain
// wrapper for parity with exporter.c's format_rfc3339 - one exporter is only
// ever driven from one thread at a time by the wrapper this targets.
static bool format_rfc3339(const ddog_timespec* ts, ddog__buf* out)
{
    time_t sec = (time_t)ts->seconds;
    struct tm tm_utc;
    if (gmtime_s(&tm_utc, &sec) != 0)
    {
        return false;
    }

    char buf[40];
    int n = snprintf(buf, sizeof(buf), "%04d-%02d-%02dT%02d:%02d:%02d.%09uZ", tm_utc.tm_year + 1900, tm_utc.tm_mon + 1,
                      tm_utc.tm_mday, tm_utc.tm_hour, tm_utc.tm_min, tm_utc.tm_sec, (unsigned)ts->nanoseconds);
    if (n < 0)
    {
        return false;
    }
    return ddog__buf_append(out, buf, (size_t)n);
}

static bool append_json_string_from_buf(ddog__buf* out, const ddog__buf* value)
{
    ddog_charslice s;
    s.ptr = (const char*)value->data;
    s.len = value->len;
    return ddog__json_append_escaped_string(out, s);
}

// Builds the multipart "event" part - see plan doc "Exporter (libcurl)" for
// the field list. internal/info JSON are intentionally omitted (matching
// what the real wrapper does when it has nothing to say - see AgentProxy.hpp)
// since no reachable test asserts on their structure.
static bool build_event_json(const struct ddog_prof_exporter* exp, const struct ddog_prof_encoded_profile* encoded,
                              const ddog_vec_tag* additional_tags, const ddog_charslice* process_tags, ddog__buf* out)
{
    if (!append_lit(out, "{\"attachments\":[\"auto.pprof\"],\"tags_profiler\":"))
    {
        return false;
    }

    ddog__buf tags_str;
    ddog__buf_init(&tags_str);
    bool ok = build_tags_profiler_string(exp, additional_tags, &tags_str);
    if (ok)
    {
        ok = append_json_string_from_buf(out, &tags_str);
    }
    ddog__buf_free(&tags_str);
    if (!ok)
    {
        return false;
    }

    if (!append_lit(out, ",\"start\":"))
    {
        return false;
    }
    ddog__buf start_str;
    ddog__buf_init(&start_str);
    ok = format_rfc3339(&encoded->start_time, &start_str);
    if (ok)
    {
        ok = append_json_string_from_buf(out, &start_str);
    }
    ddog__buf_free(&start_str);
    if (!ok)
    {
        return false;
    }

    if (!append_lit(out, ",\"end\":"))
    {
        return false;
    }
    ddog__buf end_str;
    ddog__buf_init(&end_str);
    ok = format_rfc3339(&encoded->end_time, &end_str);
    if (ok)
    {
        ok = append_json_string_from_buf(out, &end_str);
    }
    ddog__buf_free(&end_str);
    if (!ok)
    {
        return false;
    }

    if (!append_lit(out, ",\"family\":"))
    {
        return false;
    }
    ddog_charslice family_slice;
    family_slice.ptr = exp->family;
    family_slice.len = strlen(exp->family);
    if (!ddog__json_append_escaped_string(out, family_slice))
    {
        return false;
    }

    if (!append_lit(out, ",\"version\":\"4\""))
    {
        return false;
    }

    if (process_tags != NULL && process_tags->len > 0)
    {
        if (!append_lit(out, ",\"process_tags\":"))
        {
            return false;
        }
        if (!ddog__json_append_escaped_string(out, *process_tags))
        {
            return false;
        }
    }

    return append_lit(out, "}");
}

// Hand-rolled multipart/form-data framing (RFC 7578) - WinHTTP has no
// mime-building convenience API like libcurl's curl_mime_*, so this replaces
// that piece specifically. Field/filename/ordering matches exporter.c's use
// of curl_mime_* exactly, so the Agent/MockDatadogAgent sees identical wire
// bytes regardless of platform. The boundary is a fixed literal, not
// randomly generated per request (unlike libcurl's default): collision with
// our own generated JSON/pprof content is not a realistic concern, and a
// fixed value keeps this file dependency-free.
static const char* const MULTIPART_BOUNDARY = "----DatadogProfilingPocBoundary7c3f9a2e1d";

static bool append_multipart_part_header(ddog__buf* out, const char* name, const char* filename, const char* content_type)
{
    return append_lit(out, "--") && append_lit(out, MULTIPART_BOUNDARY) && append_lit(out, "\r\n") &&
           append_lit(out, "Content-Disposition: form-data; name=\"") && append_lit(out, name) &&
           append_lit(out, "\"; filename=\"") && append_lit(out, filename) && append_lit(out, "\"\r\n") &&
           append_lit(out, "Content-Type: ") && append_lit(out, content_type) && append_lit(out, "\r\n\r\n");
}

static bool append_multipart_part_data(ddog__buf* out, const void* data, size_t len)
{
    return ddog__buf_append(out, data, len) && append_lit(out, "\r\n");
}

static bool build_multipart_body(const ddog__buf* event_json, const struct ddog_prof_encoded_profile* encoded,
                                  const ddog_prof_exporter_file* files, size_t files_len, ddog__buf* out)
{
    if (!append_multipart_part_header(out, "event", "event.json", "application/json") ||
        !append_multipart_part_data(out, event_json->data, event_json->len))
    {
        return false;
    }

    // "auto.pprof" for both the field name and filename - matches this
    // repo's actually-pinned libdatadog version's wire format, see plan doc.
    // application/octet-stream matches libcurl's own fallback for a part
    // with a filename but no explicit type and no recognized extension
    // (see curl_mime_type docs) - exporter.c never sets one explicitly
    // either, so this keeps both platforms' wire bytes identical.
    if (!append_multipart_part_header(out, "auto.pprof", "auto.pprof", "application/octet-stream") ||
        !append_multipart_part_data(out, encoded->data, encoded->len))
    {
        return false;
    }

    // Additional files (metrics.json etc). Sent uncompressed here, unlike
    // real libdatadog which zstd-compresses each individually - no path
    // exercised by M1-M4 sends a non-empty files list (files_len is always 0
    // for a plain wall/cpu-time sample app), so this is untested; flagged as
    // follow-up work rather than silently "supported" (see README).
    for (size_t i = 0; i < files_len; i++)
    {
        char name_buf[256];
        size_t name_len = files[i].name.len < sizeof(name_buf) - 1 ? files[i].name.len : sizeof(name_buf) - 1;
        memcpy(name_buf, files[i].name.ptr, name_len);
        name_buf[name_len] = '\0';
        if (!append_multipart_part_header(out, name_buf, name_buf, "application/octet-stream") ||
            !append_multipart_part_data(out, files[i].file.ptr, files[i].file.len))
        {
            return false;
        }
    }

    return append_lit(out, "--") && append_lit(out, MULTIPART_BOUNDARY) && append_lit(out, "--\r\n");
}

ddog_error_code ddog_prof_exporter_send_blocking(ddog_prof_exporter* exporter, ddog_prof_encoded_profile* profile,
                                                  const ddog_prof_exporter_file* files, size_t files_len,
                                                  const ddog_vec_tag* optional_additional_tags,
                                                  const ddog_charslice* optional_process_tags,
                                                  const ddog_charslice* optional_internal_metadata_json,
                                                  const ddog_charslice* optional_info_json, uint16_t* out_http_status)
{
    // Not required by any reachable test - see plan doc "Exporter (libcurl)".
    (void)optional_internal_metadata_json;
    (void)optional_info_json;

    struct ddog_prof_exporter* exp = (struct ddog_prof_exporter*)exporter;
    struct ddog_prof_encoded_profile* encoded = (struct ddog_prof_encoded_profile*)profile;

    if (exp == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "exporter is NULL");
    }
    if (encoded == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "profile is NULL");
    }
    if (out_http_status == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "out_http_status is NULL");
    }
    if (files == NULL && files_len > 0)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "files is NULL but files_len > 0");
    }

    ddog__buf event_json;
    ddog__buf_init(&event_json);
    if (!build_event_json(exp, encoded, optional_additional_tags, optional_process_tags, &event_json))
    {
        ddog__buf_free(&event_json);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to build event.json");
    }

    ddog__buf body;
    ddog__buf_init(&body);
    if (!build_multipart_body(&event_json, encoded, files, files_len, &body))
    {
        ddog__buf_free(&event_json);
        ddog__buf_free(&body);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to build multipart body");
    }
    ddog__buf_free(&event_json);

    wchar_t* url_w = narrow_to_wide(exp->url);
    if (url_w == NULL)
    {
        ddog__buf_free(&body);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to convert url");
    }

    wchar_t hostname[256] = {0};
    wchar_t urlpath[2048] = {0};
    URL_COMPONENTS comp;
    memset(&comp, 0, sizeof(comp));
    comp.dwStructSize = sizeof(comp);
    comp.lpszHostName = hostname;
    comp.dwHostNameLength = sizeof(hostname) / sizeof(wchar_t);
    comp.lpszUrlPath = urlpath;
    comp.dwUrlPathLength = sizeof(urlpath) / sizeof(wchar_t);
    comp.dwSchemeLength = (DWORD)-1; // don't care about the copied value, only nScheme below
    comp.dwExtraInfoLength = (DWORD)-1; // fold any query string into urlpath instead of erroring

    if (!WinHttpCrackUrl(url_w, 0, 0, &comp))
    {
        DWORD err = GetLastError();
        free(url_w);
        ddog__buf_free(&body);
        return ddog__fail(DDOG_ERR_TRANSPORT, "WinHttpCrackUrl failed for '%s': %lu", exp->url, (unsigned long)err);
    }
    free(url_w);

    bool is_https = comp.nScheme == INTERNET_SCHEME_HTTPS;

    HINTERNET hConnect = WinHttpConnect(exp->session, hostname, comp.nPort, 0);
    if (hConnect == NULL)
    {
        DWORD err = GetLastError();
        ddog__buf_free(&body);
        return ddog__fail(DDOG_ERR_TRANSPORT, "WinHttpConnect failed: %lu", (unsigned long)err);
    }

    HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"POST", urlpath, NULL, WINHTTP_NO_REFERER,
                                             WINHTTP_DEFAULT_ACCEPT_TYPES, is_https ? WINHTTP_FLAG_SECURE : 0);
    if (hRequest == NULL)
    {
        DWORD err = GetLastError();
        WinHttpCloseHandle(hConnect);
        ddog__buf_free(&body);
        return ddog__fail(DDOG_ERR_TRANSPORT, "WinHttpOpenRequest failed: %lu", (unsigned long)err);
    }

    DWORD timeout = (DWORD)exp->timeout_ms;
    WinHttpSetTimeouts(hRequest, (int)timeout, (int)timeout, (int)timeout, (int)timeout);

    // Built as one CRLF-joined blob (rather than one WinHttpAddRequestHeaders
    // call per header) to mirror libcurl's curl_slist_append list closely.
    ddog__buf headers;
    ddog__buf_init(&headers);
    bool headers_ok = append_lit(&headers, "Content-Type: multipart/form-data; boundary=") &&
                       append_lit(&headers, MULTIPART_BOUNDARY) && append_lit(&headers, "\r\n") &&
                       append_lit(&headers, "Connection: close\r\n");

    char header_line[512];
    if (headers_ok)
    {
        snprintf(header_line, sizeof(header_line), "DD-EVP-ORIGIN: %s\r\n", exp->library_name);
        headers_ok = append_lit(&headers, header_line);
    }
    if (headers_ok)
    {
        snprintf(header_line, sizeof(header_line), "DD-EVP-ORIGIN-VERSION: %s\r\n", exp->library_version);
        headers_ok = append_lit(&headers, header_line);
    }
    if (headers_ok && exp->api_key != NULL)
    {
        snprintf(header_line, sizeof(header_line), "dd-api-key: %s\r\n", exp->api_key);
        headers_ok = append_lit(&headers, header_line);
    }
    if (headers_ok)
    {
        snprintf(header_line, sizeof(header_line), "User-Agent: DDProf/%s\r\n", exp->library_version);
        headers_ok = append_lit(&headers, header_line);
    }

    ddog_error_code rc = DDOG_OK;
    if (!headers_ok)
    {
        rc = ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to build request headers");
    }
    else
    {
        wchar_t* headers_w = narrow_to_wide((const char*)headers.data);
        if (headers_w == NULL)
        {
            rc = ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to convert headers");
        }
        else
        {
            BOOL sent = WinHttpSendRequest(hRequest, headers_w, (DWORD)-1L, body.data, (DWORD)body.len, (DWORD)body.len, 0);
            free(headers_w);

            if (!sent)
            {
                rc = ddog__fail(DDOG_ERR_TRANSPORT, "WinHttpSendRequest failed: %lu", (unsigned long)GetLastError());
            }
            else if (!WinHttpReceiveResponse(hRequest, NULL))
            {
                rc = ddog__fail(DDOG_ERR_TRANSPORT, "WinHttpReceiveResponse failed: %lu", (unsigned long)GetLastError());
            }
            else
            {
                DWORD status = 0;
                DWORD status_size = sizeof(status);
                if (!WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                                          WINHTTP_HEADER_NAME_BY_INDEX, &status, &status_size, WINHTTP_NO_HEADER_INDEX))
                {
                    rc = ddog__fail(DDOG_ERR_TRANSPORT, "WinHttpQueryHeaders failed: %lu", (unsigned long)GetLastError());
                }
                else
                {
                    *out_http_status = (uint16_t)status;
                }
            }
        }
    }

    ddog__buf_free(&headers);
    ddog__buf_free(&body);
    WinHttpCloseHandle(hRequest);
    WinHttpCloseHandle(hConnect);

    return rc;
}
