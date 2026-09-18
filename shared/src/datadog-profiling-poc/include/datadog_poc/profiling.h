// PoC C replacement for libdatadog - see shared/src/datadog-profiling-poc/README.md
// This is NOT production code. Own, hand-designed API: every fallible function
// returns a ddog_error_code; input params first, output params (via pointer)
// last. Not ABI-compatible with the real libdatadog cbindgen headers - see the
// plan doc for why, and for the pprof wire-format requirements this library
// still has to satisfy regardless (those are unrelated to this C-level API
// shape and are not up for simplification).
#pragma once

#include "datadog_poc/common.h"

#ifdef __cplusplus
extern "C" {
#endif

// ---- Value types / period ----
typedef struct {
    ddog_charslice type;
    ddog_charslice unit;
} ddog_prof_value_type;

typedef struct {
    ddog_prof_value_type type;
    int64_t value;
} ddog_prof_period;

// ---- Sample data (plain, borrowed - no ManagedStringId-style dead fields) ----
typedef struct {
    ddog_charslice name;
    ddog_charslice system_name;
    ddog_charslice filename;
} ddog_prof_function;

typedef struct {
    ddog_prof_function function;
    ddog_charslice mapping_filename; // owning module/assembly name (pprof Mapping.filename)
    uint64_t address;
    int64_t line;
} ddog_prof_location;

typedef struct {
    ddog_charslice key;
    ddog_charslice str;      // set when this is a string label
    int64_t num;             // set when this is a numeric label
    ddog_charslice num_unit; // optional, only meaningful alongside num
} ddog_prof_label;

typedef struct {
    const ddog_prof_location* locations;
    size_t locations_len;
    const int64_t* values;
    size_t values_len;
    const ddog_prof_label* labels;
    size_t labels_len;
} ddog_prof_sample;

// ---- Profile handle (opaque - definition private to profile.c) ----
typedef struct ddog_prof_profile ddog_prof_profile;

DDOG_API ddog_error_code ddog_prof_profile_new(const ddog_prof_value_type* sample_types, size_t sample_types_len,
                                                const ddog_prof_period* period,
                                                ddog_prof_profile** out_profile);
DDOG_API ddog_error_code ddog_prof_profile_add(ddog_prof_profile* profile, const ddog_prof_sample* sample, int64_t timestamp);
DDOG_API ddog_error_code ddog_prof_profile_set_endpoint(ddog_prof_profile* profile, uint64_t local_root_span_id, ddog_charslice endpoint);
DDOG_API ddog_error_code ddog_prof_profile_add_endpoint_count(ddog_prof_profile* profile, ddog_charslice endpoint, int64_t value);
DDOG_API ddog_error_code ddog_prof_profile_add_upscaling_rule_proportional(ddog_prof_profile* profile,
                                                                            const size_t* offset_values, size_t offset_values_len,
                                                                            ddog_charslice label_name, ddog_charslice label_value,
                                                                            uint64_t total_sampled, uint64_t total_real);
DDOG_API ddog_error_code ddog_prof_profile_add_upscaling_rule_poisson(ddog_prof_profile* profile,
                                                                       const size_t* offset_values, size_t offset_values_len,
                                                                       ddog_charslice label_name, ddog_charslice label_value,
                                                                       size_t sum_value_offset, size_t count_value_offset,
                                                                       uint64_t sampling_distance);

// ---- Encoded (serialized + compressed) profile handle ----
typedef struct ddog_prof_encoded_profile ddog_prof_encoded_profile;

typedef struct {
    int64_t seconds;
    uint32_t nanoseconds;
} ddog_timespec;

DDOG_API ddog_error_code ddog_prof_profile_serialize(ddog_prof_profile* profile,
                                                      const ddog_timespec* start_time, const ddog_timespec* end_time,
                                                      ddog_prof_encoded_profile** out_encoded);
DDOG_API void ddog_prof_profile_drop(ddog_prof_profile* profile);

DDOG_API ddog_error_code ddog_prof_encoded_profile_bytes(ddog_prof_encoded_profile* encoded, const uint8_t** out_ptr, size_t* out_len);
DDOG_API void ddog_prof_encoded_profile_drop(ddog_prof_encoded_profile* encoded);

// ---- Endpoint (plain value type - caller constructs it, we don't own it) ----
typedef enum {
    DDOG_POC_PROF_ENDPOINT_AGENT,
    DDOG_POC_PROF_ENDPOINT_AGENTLESS
} ddog_prof_endpoint_kind;

typedef struct {
    ddog_prof_endpoint_kind kind;
    ddog_charslice url_or_site; // base_url for AGENT, site for AGENTLESS
    ddog_charslice api_key;     // only meaningful for AGENTLESS
    uint64_t timeout_ms;
    bool use_system_resolver;
} ddog_prof_endpoint;

DDOG_API ddog_error_code ddog_prof_endpoint_agent(ddog_charslice base_url, uint64_t timeout_ms, bool use_system_resolver,
                                                   ddog_prof_endpoint* out_endpoint);
DDOG_API ddog_error_code ddog_prof_endpoint_agentless(ddog_charslice site, ddog_charslice api_key, uint64_t timeout_ms, bool use_system_resolver,
                                                       ddog_prof_endpoint* out_endpoint);

// ---- Exporter (opaque handle) ----
typedef struct ddog_prof_exporter ddog_prof_exporter;

typedef struct {
    ddog_charslice name;
    ddog_byteslice file;
} ddog_prof_exporter_file;

DDOG_API ddog_error_code ddog_prof_exporter_new(ddog_charslice profiling_library_name, ddog_charslice profiling_library_version,
                                                 ddog_charslice family, const ddog_vec_tag* tags, const ddog_prof_endpoint* endpoint,
                                                 ddog_prof_exporter** out_exporter);
DDOG_API ddog_error_code ddog_prof_exporter_send_blocking(ddog_prof_exporter* exporter, ddog_prof_encoded_profile* profile,
                                                           const ddog_prof_exporter_file* files, size_t files_len,
                                                           const ddog_vec_tag* optional_additional_tags,
                                                           const ddog_charslice* optional_process_tags,
                                                           const ddog_charslice* optional_internal_metadata_json,
                                                           const ddog_charslice* optional_info_json,
                                                           uint16_t* out_http_status);
DDOG_API void ddog_prof_exporter_drop(ddog_prof_exporter* exporter);

#ifdef __cplusplus
}
#endif
