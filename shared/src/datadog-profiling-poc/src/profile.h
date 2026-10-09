// Private internal representation of ddog_prof_profile (opaque in the public
// header). Visible to pprof_encode.c so it can walk the accumulated data.
#pragma once

#include "datadog_poc/profiling.h"

// ---- interner ----
//
// Maps a byte key to a dense id (0, 1, 2, ... in insertion order), so that
// every distinct string, function, mapping, location, stack and label set is
// stored once - like libdatadog's interned sets (libdd-profiling
// internal/profile). The pprof ids are then just id + 1, and the string
// table is the interned strings in id order.
typedef struct {
    uint8_t** keys;     // keys[id]: owned copy of the key bytes
    uint32_t* key_lens;
    uint64_t* hashes;
    uint32_t len;       // number of entries
    uint32_t capacity;  // of keys/key_lens/hashes
    uint32_t* slots;    // open addressing, linear probing: id + 1, 0 = empty
    uint32_t slots_len; // power of two, or 0
} ddog_interner;

// Interned key layouts. Every field is filled (padding included) before
// interning so that equal values have equal bytes.
typedef struct {
    uint32_t name;        // string ids
    uint32_t system_name;
    uint32_t filename;
} ddog_function_key;

typedef struct {
    uint32_t mapping;  // mapping id + 1, 0 = no mapping
    uint32_t function; // function id
    uint64_t address;
    int64_t line;
} ddog_location_key;

// A string label is (key, str); a numeric one is (key, num, num_unit), like
// libdatadog's interned Label. str is string id 0 ("") for a numeric label.
typedef struct {
    uint32_t key;
    uint32_t str;
    uint32_t num_unit;
    uint32_t padding;
    int64_t num;
} ddog_label_key;

// A stack is a ddog_interner key made of uint32 location ids (leaf first);
// a label set is a key made of ddog_label_key; a mapping is a key made of the
// uint32 string id of its filename.

// Samples are fixed-size records: this header followed by
// sample_types_len int64 values.
typedef struct {
    uint32_t stack;
    uint32_t labelset;
    int64_t timestamp; // 0 = no timestamp, like libdatadog's Option<NonZeroI64>
} ddog_sample_header;

typedef struct {
    uint8_t* data;
    size_t len;      // number of records
    size_t capacity; // in records
} ddog_sample_array;

// Endpoint counts are summed per endpoint name (libdatadog's
// ProfiledEndpointsStats) and travel with the encoded profile to the
// exporter, which writes them as the event's "endpoint_counts" object.
typedef struct {
    ddog_charslice endpoint; // owned
    int64_t value;
} ddog_prof_endpoint_count_owned;

// local root span id -> endpoint, used at encode time to add a "trace
// endpoint" label to samples carrying a matching "local root span id" label.
// A later mapping for the same span id replaces the earlier one (libdatadog
// uses a HashMap insert).
typedef struct {
    uint64_t local_root_span_id;
    ddog_charslice endpoint; // owned
} ddog_prof_endpoint_mapping_owned;

typedef enum {
    DDOG_UPSCALING_PROPORTIONAL,
    DDOG_UPSCALING_POISSON
} ddog_upscaling_kind;

// Mirrors libdatadog's UpscalingRule (libdd-profiling/src/internal/upscaling.rs).
// Applied to each sample's values at encode time.
typedef struct {
    size_t* offsets; // owned, sorted ascending
    size_t offsets_len;
    ddog_charslice label_name;  // owned, {NULL,0} for a by-value rule
    ddog_charslice label_value; // owned, {NULL,0} for a by-value rule
    ddog_upscaling_kind kind;
    double scale;              // PROPORTIONAL: total_real / total_sampled
    size_t sum_value_offset;   // POISSON
    size_t count_value_offset; // POISSON
    uint64_t sampling_distance; // POISSON
} ddog_upscaling_rule_owned;

struct ddog_prof_profile {
    ddog_prof_value_type* sample_types; // owned array; owned strings inside each entry
    size_t sample_types_len;
    ddog_prof_period period; // owned strings inside period.type

    ddog_interner strings; // id 0 is always ""
    ddog_interner functions;
    ddog_interner mappings;
    ddog_interner locations;
    ddog_interner stacks;
    ddog_interner labelsets;

    // Input-frame cache: the frames as passed to add (string pointers and
    // lengths, address, line) -> stack id, so a repeated callstack skips the
    // per-frame interning. Hits are verified against the interned content,
    // so a caller buffer reused with different bytes is never a problem.
    ddog_interner stack_cache;
    uint32_t* stack_cache_ids; // stack_cache id -> stack id
    size_t stack_cache_ids_capacity;
    uint8_t* scratch; // reusable buffer to build keys
    size_t scratch_capacity;

    // Like libdatadog: samples with a timestamp are all kept (and written
    // first), samples without one are aggregated per (stack, label set) -
    // `aggregation` maps that pair to the record index in `aggregated`.
    ddog_sample_array timestamped;
    ddog_sample_array aggregated;
    ddog_interner aggregation;

    ddog_prof_endpoint_count_owned* endpoint_counts;
    size_t endpoint_counts_len;
    size_t endpoint_counts_capacity;

    ddog_prof_endpoint_mapping_owned* endpoint_mappings;
    size_t endpoint_mappings_len;
    size_t endpoint_mappings_capacity;

    ddog_upscaling_rule_owned* upscaling_rules;
    size_t upscaling_rules_len;

    ddog_timespec start_time; // captured at ddog_prof_profile_new / each reset
};

// Size in bytes of one sample record (header + values).
static inline size_t ddog__sample_record_size(const struct ddog_prof_profile* profile)
{
    return sizeof(ddog_sample_header) + profile->sample_types_len * sizeof(int64_t);
}

// Interns `s` into the profile's string table (used by the encoder for the
// strings it adds: sample types, "trace endpoint", endpoint names, ...).
bool ddog__intern_string(struct ddog_prof_profile* profile, ddog_charslice s, uint32_t* out_id);

// Looks `s` up without inserting it. Returns false if absent.
bool ddog__find_string(const struct ddog_prof_profile* profile, ddog_charslice s, uint32_t* out_id);

static inline ddog_charslice ddog__string(const struct ddog_prof_profile* profile, uint32_t id)
{
    ddog_charslice s;
    s.ptr = (const char*)profile->strings.keys[id];
    s.len = profile->strings.key_lens[id];
    return s;
}
