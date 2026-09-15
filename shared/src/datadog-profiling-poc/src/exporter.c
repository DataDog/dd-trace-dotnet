#include "datadog_poc/profiling.h"
#include "encoded_profile.h"
#include "internal.h"
#include "json_min.h"
#include "tags.h"

#include <curl/curl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

struct ddog_prof_exporter {
    char* url;             // owned, NUL-terminated
    char* api_key;         // owned, NUL-terminated; NULL in agent mode
    char* library_name;    // owned, NUL-terminated
    char* library_version; // owned, NUL-terminated
    char* family;           // owned, NUL-terminated
    ddog_vec_tag tags;      // owned deep copy of the tags passed to _new
    uint64_t timeout_ms;
    CURL* curl; // reused across send_blocking calls
};

// libcurl needs curl_global_init() once per process before any use. Not
// thread-safe against concurrent *first* calls from multiple threads - a
// real library would guard this with pthread_once/InitOnceExecuteOnce, but
// exporters are only ever constructed from one thread at a time in the
// wrapper this is built for, so a plain flag is enough for the PoC.
static bool g_curl_global_initialized = false;

static void ensure_curl_global_init(void)
{
    if (!g_curl_global_initialized)
    {
        curl_global_init(CURL_GLOBAL_DEFAULT);
        g_curl_global_initialized = true;
    }
}

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

    ensure_curl_global_init();

    // calloc zero-initializes every field, which already matches what
    // ddog_vec_tag_new()/a NULL url/api_key/curl would set - so partial
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
    exp->curl = curl_easy_init();
    if (exp->curl == NULL)
    {
        ddog_prof_exporter_drop((ddog_prof_exporter*)exp);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "curl_easy_init failed");
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
    if (exp->curl != NULL)
    {
        curl_easy_cleanup(exp->curl);
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

// Not thread-safe (uses plain gmtime()) - fine here since one exporter is
// only ever driven from one thread at a time by the wrapper this targets.
static bool format_rfc3339(const ddog_timespec* ts, ddog__buf* out)
{
    time_t sec = (time_t)ts->seconds;
    struct tm* tm_utc = gmtime(&sec);
    if (tm_utc == NULL)
    {
        return false;
    }

    char buf[40];
    int n = snprintf(buf, sizeof(buf), "%04d-%02d-%02dT%02d:%02d:%02d.%09uZ", tm_utc->tm_year + 1900, tm_utc->tm_mon + 1,
                      tm_utc->tm_mday, tm_utc->tm_hour, tm_utc->tm_min, tm_utc->tm_sec, (unsigned)ts->nanoseconds);
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

    curl_mime* mime = curl_mime_init(exp->curl);
    if (mime == NULL)
    {
        ddog__buf_free(&event_json);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "curl_mime_init failed");
    }

    curl_mimepart* event_part = curl_mime_addpart(mime);
    curl_mimepart* profile_part = curl_mime_addpart(mime);
    if (event_part == NULL || profile_part == NULL)
    {
        curl_mime_free(mime);
        ddog__buf_free(&event_json);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "curl_mime_addpart failed");
    }

    curl_mime_name(event_part, "event");
    curl_mime_filename(event_part, "event.json");
    curl_mime_type(event_part, "application/json");
    curl_mime_data(event_part, (const char*)event_json.data, event_json.len);

    // "auto.pprof" for both the field name and filename - matches this
    // repo's actually-pinned libdatadog version's wire format, see plan doc.
    curl_mime_name(profile_part, "auto.pprof");
    curl_mime_filename(profile_part, "auto.pprof");
    curl_mime_data(profile_part, (const char*)encoded->data, encoded->len);

    // Additional files (metrics.json etc). Sent uncompressed here, unlike
    // real libdatadog which zstd-compresses each individually - no path
    // exercised by M1-M4 sends a non-empty files list (files_len is always 0
    // for a plain wall/cpu-time sample app), so this is untested; flagged as
    // follow-up work rather than silently "supported".
    for (size_t i = 0; i < files_len; i++)
    {
        curl_mimepart* part = curl_mime_addpart(mime);
        if (part == NULL)
        {
            curl_mime_free(mime);
            ddog__buf_free(&event_json);
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "curl_mime_addpart failed for additional file");
        }
        char name_buf[256];
        size_t name_len = files[i].name.len < sizeof(name_buf) - 1 ? files[i].name.len : sizeof(name_buf) - 1;
        memcpy(name_buf, files[i].name.ptr, name_len);
        name_buf[name_len] = '\0';
        curl_mime_name(part, name_buf);
        curl_mime_filename(part, name_buf);
        curl_mime_data(part, (const char*)files[i].file.ptr, files[i].file.len);
    }

    struct curl_slist* headers = NULL;
    headers = curl_slist_append(headers, "Connection: close");

    char header_buf[512];
    snprintf(header_buf, sizeof(header_buf), "DD-EVP-ORIGIN: %s", exp->library_name);
    headers = curl_slist_append(headers, header_buf);
    snprintf(header_buf, sizeof(header_buf), "DD-EVP-ORIGIN-VERSION: %s", exp->library_version);
    headers = curl_slist_append(headers, header_buf);
    if (exp->api_key != NULL)
    {
        snprintf(header_buf, sizeof(header_buf), "dd-api-key: %s", exp->api_key);
        headers = curl_slist_append(headers, header_buf);
    }

    char user_agent[128];
    snprintf(user_agent, sizeof(user_agent), "DDProf/%s", exp->library_version);

    curl_easy_setopt(exp->curl, CURLOPT_URL, exp->url);
    curl_easy_setopt(exp->curl, CURLOPT_MIMEPOST, mime);
    curl_easy_setopt(exp->curl, CURLOPT_HTTPHEADER, headers);
    curl_easy_setopt(exp->curl, CURLOPT_USERAGENT, user_agent);
    curl_easy_setopt(exp->curl, CURLOPT_TIMEOUT_MS, (long)exp->timeout_ms);
    curl_easy_setopt(exp->curl, CURLOPT_NOSIGNAL, 1L);

    CURLcode res = curl_easy_perform(exp->curl);

    ddog_error_code rc = DDOG_OK;
    if (res != CURLE_OK)
    {
        rc = ddog__fail(DDOG_ERR_TRANSPORT, "curl_easy_perform failed: %s", curl_easy_strerror(res));
    }
    else
    {
        long status = 0;
        curl_easy_getinfo(exp->curl, CURLINFO_RESPONSE_CODE, &status);
        *out_http_status = (uint16_t)status;
    }

    curl_slist_free_all(headers);
    curl_mime_free(mime);
    ddog__buf_free(&event_json);

    return rc;
}
