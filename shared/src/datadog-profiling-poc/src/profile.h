// Private internal representation of ddog_prof_profile (opaque in the public
// header). Visible to pprof_encode.c so it can walk accumulated samples.
#pragma once

#include "datadog_poc/profiling.h"

// Owned (deep-copied) variants of the borrowed public structs. The wrapper's
// charslices typically point at transient strings (e.g. a std::string's
// buffer) that don't outlive a single ddog_prof_profile_add call, so
// everything gets copied on the way in.
//
// Deliberately NOT deduplicated - see plan doc "Deliberate PoC simplification":
// every occurrence gets its own copy and, at encode time, its own fresh pprof
// id. Costs output size, not correctness.
typedef struct {
    ddog_prof_function function; // owned strings inside
    ddog_charslice mapping_filename; // owned
    uint64_t address;
    int64_t line;
} ddog_prof_location_owned;

typedef struct {
    ddog_charslice key;      // owned
    ddog_charslice str;      // owned, {NULL,0} if this is a numeric label
    int64_t num;
    ddog_charslice num_unit; // owned, {NULL,0} if unused
} ddog_prof_label_owned;

typedef struct {
    ddog_prof_location_owned* locations;
    size_t locations_len;
    int64_t* values;
    size_t values_len;
    ddog_prof_label_owned* labels;
    size_t labels_len;
    int64_t timestamp;
} ddog_prof_sample_owned;

typedef struct {
    ddog_charslice endpoint; // owned
    int64_t value;
} ddog_prof_endpoint_count_owned;

// NOTE on scope: ddog_prof_profile_set_endpoint/add_endpoint_count validate
// their arguments and are stored below (endpoint_mappings/endpoint_counts).
// add_upscaling_rule_proportional/poisson only validate their arguments and
// are NOT stored or applied anywhere. Unlike everything else in this file,
// none of the three are reflected in the encoded pprof output yet: real
// libdatadog injects a derived label onto matching samples (set_endpoint)
// and rescales sample values (upscaling rules) at serialize time; replicating
// that exactly wasn't needed to prove the core create/add/serialize/export
// path, so it's left as documented follow-up work rather than silently
// dropped.
typedef struct {
    uint64_t local_root_span_id;
    ddog_charslice endpoint; // owned
} ddog_prof_endpoint_mapping_owned;

struct ddog_prof_profile {
    ddog_prof_value_type* sample_types; // owned array; owned strings inside each entry
    size_t sample_types_len;
    ddog_prof_period period; // owned strings inside period.type

    ddog_prof_sample_owned* samples;
    size_t samples_len;
    size_t samples_capacity;

    ddog_prof_endpoint_count_owned* endpoint_counts;
    size_t endpoint_counts_len;
    size_t endpoint_counts_capacity;

    ddog_prof_endpoint_mapping_owned* endpoint_mappings;
    size_t endpoint_mappings_len;
    size_t endpoint_mappings_capacity;

    ddog_timespec start_time; // captured at ddog_prof_profile_new / each reset
};
