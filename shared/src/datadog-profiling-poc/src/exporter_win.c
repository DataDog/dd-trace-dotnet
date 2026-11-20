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
#include "zstd_compress.h"
#include "tags.h"

#include <windows.h>
#include <winhttp.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>
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
    // Non-NULL when the agent endpoint was a "windows:<pipe path>" URL (the
    // profiler sends e.g. "windows:\\\\./pipe/\\<name>" when a named pipe is
    // configured): the upload then speaks plain HTTP/1.1 over the named pipe
    // instead of WinHTTP-over-TCP. Mirrors libdatadog's named-pipe connector.
    char* pipe_path; // owned, NUL-terminated
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

        // "windows:<pipe path>" named-pipe agent endpoint (the profiler builds
        // "windows:\\\\./pipe/\\<name>" from DD_TRACE_PIPE_NAME). Like
        // libdatadog (which strips the bare "windows:" prefix), everything
        // after the prefix is the pipe path used verbatim; the HTTP request
        // itself targets /profiling/v1/input (see send_over_named_pipe).
        static const char* pipe_prefix = "windows:";
        size_t pipe_prefix_len = strlen(pipe_prefix);
        if (base.len >= pipe_prefix_len && memcmp(base.ptr, pipe_prefix, pipe_prefix_len) == 0)
        {
            size_t path_len = base.len - pipe_prefix_len;
            exp->pipe_path = (char*)malloc(path_len + 1);
            if (exp->pipe_path == NULL)
            {
                ddog_prof_exporter_drop((ddog_prof_exporter*)exp);
                return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy the agent named pipe path");
            }
            if (path_len > 0)
            {
                memcpy(exp->pipe_path, base.ptr + pipe_prefix_len, path_len);
            }
            exp->pipe_path[path_len] = '\0';
        }
        else
        {
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
    free(exp->pipe_path);
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

// Builds the multipart "event" part, with the same fields as libdatadog's
// ProfileExporter::build_event_json (endpoint_counts, process_tags, internal
// and info are appended by ddog__json_append_event_tail, shared with the
// other exporter). internal/info must already have been validated.
static bool build_event_json(const struct ddog_prof_exporter* exp, const struct ddog_prof_encoded_profile* encoded,
                              const ddog_prof_exporter_file* files_to_compress, size_t files_to_compress_len,
                              const ddog_vec_tag* additional_tags, const ddog_charslice* process_tags,
                              const ddog_charslice* internal_json, const ddog_charslice* info_json, ddog__buf* out)
{
    if (!append_lit(out, "{\"attachments\":") ||
        !ddog__json_append_attachments(out, files_to_compress, files_to_compress_len, "auto.pprof") ||
        !append_lit(out, ",\"tags_profiler\":"))
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

    return ddog__json_append_event_tail(out, encoded->endpoint_counts, encoded->endpoint_counts_len, process_tags,
                                        internal_json, info_json);
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

static bool append_file_part(ddog__buf* out, ddog_charslice name, const void* data, size_t len)
{
    // Like libdatadog, an additional file uses its name for both the form
    // field and the filename.
    char name_buf[256];
    size_t name_len = name.len < sizeof(name_buf) - 1 ? name.len : sizeof(name_buf) - 1;
    memcpy(name_buf, name.ptr, name_len);
    name_buf[name_len] = '\0';
    return append_multipart_part_header(out, name_buf, name_buf, "application/octet-stream") &&
           append_multipart_part_data(out, data, len);
}

// Same part order as libdatadog (and exporter.c): event, additional files, profile
static bool build_multipart_body(const ddog__buf* event_json, const struct ddog_prof_encoded_profile* encoded,
                                  const ddog_prof_exporter_file* files_to_compress, const ddog__buf* compressed,
                                  size_t files_to_compress_len, ddog__buf* out)
{
    if (!append_multipart_part_header(out, "event", "event.json", "application/json") ||
        !append_multipart_part_data(out, event_json->data, event_json->len))
    {
        return false;
    }

    for (size_t i = 0; i < files_to_compress_len; i++)
    {
        if (!append_file_part(out, files_to_compress[i].name, compressed[i].data, compressed[i].len))
        {
            return false;
        }
    }

    // "auto.pprof" for both the field name and filename (libdatadog v38 uses
    // "profile.pprof", see README). application/octet-stream matches
    // libcurl's own fallback for a part with a filename but no explicit type
    // (see curl_mime_type docs) - exporter.c never sets one explicitly
    // either, so this keeps both platforms' wire bytes identical.
    if (!append_multipart_part_header(out, "auto.pprof", "auto.pprof", "application/octet-stream") ||
        !append_multipart_part_data(out, encoded->data, encoded->len))
    {
        return false;
    }

    return append_lit(out, "--") && append_lit(out, MULTIPART_BOUNDARY) && append_lit(out, "--\r\n");
}

// Formats a Windows error code into its human-readable text (the profiler's
// smoke tests assert on message strings like "The system cannot find the file
// specified" when the pipe does not exist, not on numeric codes).
static char* win_error_message(DWORD err)
{
    LPWSTR w = NULL;
    DWORD n = FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM |
                                 FORMAT_MESSAGE_IGNORE_INSERTS,
                             NULL, err, 0, (LPWSTR)&w, 0, NULL);
    if (n == 0 || w == NULL)
    {
        return NULL;
    }
    // strip the trailing CR/LF FormatMessage adds
    while (n > 0 && (w[n - 1] == L'\r' || w[n - 1] == L'\n' || w[n - 1] == L' '))
    {
        n--;
    }
    int len = WideCharToMultiByte(CP_UTF8, 0, w, (int)n, NULL, 0, NULL, NULL);
    char* out = NULL;
    if (len > 0)
    {
        out = (char*)malloc((size_t)len + 1);
        if (out != NULL)
        {
            WideCharToMultiByte(CP_UTF8, 0, w, (int)n, out, len, NULL, NULL);
            out[len] = '\0';
        }
    }
    LocalFree(w);
    return out;
}

static ddog_error_code fail_win(const char* what, const char* detail, DWORD err)
{
    char* msg = win_error_message(err);
    ddog_error_code rc = (msg != NULL)
        ? ddog__fail(DDOG_ERR_TRANSPORT, "%s failed for named pipe '%s': %s", what, detail, msg)
        : ddog__fail(DDOG_ERR_TRANSPORT, "%s failed for named pipe '%s': error %lu", what, detail, (unsigned long)err);
    free(msg);
    return rc;
}

// Case-insensitive substring search over the response headers region.
static const char* find_ci(const char* haystack, size_t haystack_len, const char* needle)
{
    size_t needle_len = strlen(needle);
    if (needle_len == 0 || haystack_len < needle_len)
    {
        return NULL;
    }
    for (size_t i = 0; i + needle_len <= haystack_len; i++)
    {
        for (size_t j = 0; j < needle_len; j++)
        {
            if (tolower((unsigned char)haystack[i + j]) != tolower((unsigned char)needle[j]))
            {
                goto next;
            }
        }
        return haystack + i;
    next:;
    }
    return NULL;
}

// Sends one HTTP request over a Windows named pipe, mirroring libdatadog's
// named-pipe connector: the pipe (e.g. \\.\pipe\<name>) is a plain byte
// transport, and the request is the same HTTP/1.1 POST /profiling/v1/input the
// TCP path produces. The pipe is opened with SECURITY_ANONYMOUS QoS (like
// libdatadog, and the managed tracer's TokenImpersonationLevel.Anonymous) so a
// malicious/squatting pipe server cannot impersonate the profiling client.
//
// Known simplification: the exchange is synchronous with no read timeout -
// the smoke-test agent always responds and closes, and a hung agent would
// stall the profiler's (already single-threaded) export loop rather than
// crash anything.
static ddog_error_code send_over_named_pipe(const struct ddog_prof_exporter* exp, const ddog__buf* body,
                                             uint16_t* out_http_status)
{
    wchar_t* pipe_w = narrow_to_wide(exp->pipe_path);
    if (pipe_w == NULL)
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to convert the named pipe path");
    }

    HANDLE pipe = INVALID_HANDLE_VALUE;
    for (int attempt = 0;; attempt++)
    {
        pipe = CreateFileW(pipe_w, GENERIC_READ | GENERIC_WRITE, 0, NULL, OPEN_EXISTING,
                           SECURITY_SQOS_PRESENT | SECURITY_ANONYMOUS, NULL);
        if (pipe != INVALID_HANDLE_VALUE)
        {
            break;
        }
        DWORD err = GetLastError();
        if (err == ERROR_PIPE_BUSY && attempt < 10)
        {
            // every pipe instance is busy - wait for one to free up and retry
            WaitNamedPipeW(pipe_w, 100);
            continue;
        }
        free(pipe_w);
        return fail_win("CreateFile", exp->pipe_path, err);
    }
    free(pipe_w);

    // Build the full request: request line + headers + multipart body.
    ddog__buf req;
    ddog__buf_init(&req);
    char header_line[512];
    bool ok = append_lit(&req, "POST /profiling/v1/input HTTP/1.1\r\n") &&
              append_lit(&req, "Host: localhost\r\n") &&
              append_lit(&req, "Content-Type: multipart/form-data; boundary=") &&
              append_lit(&req, MULTIPART_BOUNDARY) && append_lit(&req, "\r\n") &&
              append_lit(&req, "Connection: close\r\n");
    if (ok)
    {
        snprintf(header_line, sizeof(header_line), "DD-EVP-ORIGIN: %s\r\n", exp->library_name);
        ok = append_lit(&req, header_line);
    }
    if (ok)
    {
        snprintf(header_line, sizeof(header_line), "DD-EVP-ORIGIN-VERSION: %s\r\n", exp->library_version);
        ok = append_lit(&req, header_line);
    }
    if (ok && exp->api_key != NULL)
    {
        snprintf(header_line, sizeof(header_line), "dd-api-key: %s\r\n", exp->api_key);
        ok = append_lit(&req, header_line);
    }
    if (ok)
    {
        snprintf(header_line, sizeof(header_line), "User-Agent: DDProf/%s\r\n", exp->library_version);
        ok = append_lit(&req, header_line);
    }
    if (ok)
    {
        snprintf(header_line, sizeof(header_line), "Content-Length: %zu\r\n\r\n", body->len);
        ok = append_lit(&req, header_line);
    }
    if (ok)
    {
        ok = ddog__buf_append(&req, body->data, body->len);
    }

    ddog_error_code rc = DDOG_OK;
    if (!ok)
    {
        rc = ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to build the named pipe request");
    }
    else
    {
        // WriteFile on a byte-mode pipe can return TRUE having written only
        // part of the buffer (it fills the pipe's buffer), so loop.
        size_t off = 0;
        while (off < req.len)
        {
            DWORD written = 0;
            if (!WriteFile(pipe, req.data + off, (DWORD)(req.len - off), &written, NULL) || written == 0)
            {
                rc = fail_win("WriteFile", exp->pipe_path, GetLastError());
                break;
            }
            off += written;
        }
    }

    // Read the response. Stop as soon as the response is complete - the
    // agent keeps the connection open waiting for the next request (only the
    // client closes, per the "Connection: close" contract), so reading until
    // EOF would deadlock. Content-Length tells us when the body is done; if
    // absent, fall back to reading until the peer closes.
    ddog__buf resp;
    ddog__buf_init(&resp);
    if (rc == DDOG_OK)
    {
        for (;;)
        {
            char chunk[8192];
            DWORD readn = 0;
            if (!ReadFile(pipe, chunk, sizeof(chunk), &readn, NULL))
            {
                DWORD err = GetLastError();
                if (err == ERROR_BROKEN_PIPE || err == ERROR_NO_DATA || err == ERROR_PIPE_NOT_CONNECTED)
                {
                    break; // the peer closed - end of the response (no Content-Length)
                }
                rc = fail_win("ReadFile", exp->pipe_path, err);
                break;
            }
            if (readn == 0)
            {
                break;
            }
            if (!ddog__buf_append(&resp, chunk, readn))
            {
                rc = ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to buffer the named pipe response");
                break;
            }

            // headers complete? then honor Content-Length and stop when satisfied
            const char* hdr_end = NULL;
            const char* base = (const char*)resp.data;
            for (size_t i = 0; i + 3 < resp.len; i++)
            {
                if (base[i] == '\r' && base[i + 1] == '\n' && base[i + 2] == '\r' && base[i + 3] == '\n')
                {
                    hdr_end = base + i + 4;
                    break;
                }
            }
            if (hdr_end == NULL)
            {
                continue; // still reading the response headers
            }
            const char* cl = find_ci(base, (size_t)(hdr_end - base), "\r\ncontent-length:");
            if (cl != NULL)
            {
                unsigned long long clen = strtoull(cl + 17, NULL, 10); // 17 = strlen("\r\ncontent-length:")
                if ((unsigned long long)resp.len >= (unsigned long long)(hdr_end - base) + clen)
                {
                    break; // full response received
                }
            }
        }
    }

    // Parse the status line: "HTTP/1.x NNN ..."
    if (rc == DDOG_OK)
    {
        const char* base = (const char*)resp.data;
        if (resp.len >= 12 && memcmp(base, "HTTP/1.", 7) == 0 && base[8] == ' ')
        {
            long status = strtol(base + 9, NULL, 10);
            if (status >= 100 && status <= 599)
            {
                *out_http_status = (uint16_t)status;
            }
            else
            {
                rc = ddog__fail(DDOG_ERR_TRANSPORT, "unparseable HTTP status from named pipe response");
            }
        }
        else if (resp.len == 0)
        {
            rc = ddog__fail(DDOG_ERR_TRANSPORT, "empty response from named pipe");
        }
        else
        {
            rc = ddog__fail(DDOG_ERR_TRANSPORT, "unrecognized response from named pipe");
        }
    }

    ddog__buf_free(&resp);
    ddog__buf_free(&req);
    CloseHandle(pipe);
    return rc;
}

ddog_error_code ddog_prof_exporter_send_blocking(ddog_prof_exporter* exporter, ddog_prof_encoded_profile* profile,
                                                  const ddog_prof_exporter_file* files_to_compress, size_t files_to_compress_len,
                                                  const ddog_vec_tag* optional_additional_tags,
                                                  const ddog_charslice* optional_process_tags,
                                                  const ddog_charslice* optional_internal_metadata_json,
                                                  const ddog_charslice* optional_info_json, uint16_t* out_http_status)
{
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
    if (files_to_compress == NULL && files_to_compress_len > 0)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "files_to_compress is NULL but files_to_compress_len > 0");
    }
    // libdatadog parses both documents and fails the send if either is invalid
    if (optional_internal_metadata_json != NULL && !ddog__json_is_valid(*optional_internal_metadata_json))
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "Failed to parse contents of internal_metadata json string");
    }
    if (optional_info_json != NULL && !ddog__json_is_valid(*optional_info_json))
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "Failed to parse contents of info json string");
    }

    ddog__buf event_json;
    ddog__buf_init(&event_json);
    if (!build_event_json(exp, encoded, files_to_compress, files_to_compress_len, optional_additional_tags,
                          optional_process_tags,
                          optional_internal_metadata_json, optional_info_json, &event_json))
    {
        ddog__buf_free(&event_json);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to build event.json");
    }

    ddog__buf* compressed = NULL;
    ddog_error_code compress_rc = ddog__compress_files(files_to_compress, files_to_compress_len, &compressed);
    if (compress_rc != DDOG_OK)
    {
        ddog__buf_free(&event_json);
        return compress_rc;
    }

    ddog__buf body;
    ddog__buf_init(&body);
    bool body_ok = build_multipart_body(&event_json, encoded, files_to_compress, compressed, files_to_compress_len, &body);
    ddog__free_compressed_files(compressed, files_to_compress_len);
    ddog__buf_free(&event_json);
    if (!body_ok)
    {
        ddog__buf_free(&body);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to build multipart body");
    }

    // Named-pipe agent endpoint (windows:<pipe path>): send over the pipe
    // instead of WinHTTP-over-TCP.
    if (exp->pipe_path != NULL)
    {
        ddog_error_code pipe_rc = send_over_named_pipe(exp, &body, out_http_status);
        ddog__buf_free(&body);
        return pipe_rc;
    }

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
    // narrow_to_wide() (MultiByteToWideChar with -1) and WinHttpSendRequest()
    // both expect a NUL-terminated string, but ddog__buf does not terminate
    // its contents - append the terminator explicitly. Without it the headers
    // string runs past the end of the buffer until a stray 0 byte happens to
    // be there, and the resulting garbage/length makes WinHttpSendRequest fail
    // intermittently with ERROR_INVALID_PARAMETER (87).
    if (headers_ok)
    {
        headers_ok = ddog__buf_append_byte(&headers, '\0');
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
