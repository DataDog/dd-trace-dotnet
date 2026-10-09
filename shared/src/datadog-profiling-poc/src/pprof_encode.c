// Hand-rolled encoder for Google's pprof profile.proto. Field numbers below
// are taken straight from that schema:
//
//   Profile:   sample_type=1 (repeated ValueType), sample=2 (repeated Sample),
//              mapping=3 (repeated Mapping), location=4 (repeated Location),
//              function=5 (repeated Function), string_table=6 (repeated string),
//              time_nanos=9, duration_nanos=10, period_type=11 (ValueType), period=12
//   ValueType: type=1 (string idx), unit=2 (string idx)
//   Sample:    location_id=1 (packed repeated uint64), value=2 (packed repeated int64), label=3 (repeated Label)
//   Label:     key=1 (string idx), str=2 (string idx), num=3, num_unit=4 (string idx)
//   Mapping:   id=1, filename=5 (string idx)                 [memory_start/limit/file_offset/build_id: unused by this wrapper, omitted]
//   Location:  id=1, mapping_id=2, address=3, line=4 (repeated Line)
//   Line:      function_id=1, line=2
//   Function:  id=1, name=2 (string idx), system_name=3 (string idx), filename=4 (string idx)
//
// Everything is already interned in the profile (see profile.h), so this is
// a walk over its tables: each Mapping, Location and Function is written
// once, samples reference them by id (interner id + 1), and the string table
// is the interned strings in id order (id 0 is "").

#include "pprof_encode.h"

#include <math.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

// ---- varint / tag primitives ----

#define WIRE_VARINT 0
#define WIRE_LENGTH_DELIMITED 2

static bool write_varint(ddog__buf* out, uint64_t value)
{
    uint8_t bytes[10];
    size_t n = 0;
    do
    {
        uint8_t b = (uint8_t)(value & 0x7F);
        value >>= 7;
        if (value != 0)
        {
            b |= 0x80;
        }
        bytes[n++] = b;
    } while (value != 0);
    return ddog__buf_append(out, bytes, n);
}

static bool write_tag(ddog__buf* out, uint32_t field_number, uint32_t wire_type)
{
    return write_varint(out, ((uint64_t)field_number << 3) | wire_type);
}

static bool write_varint_field(ddog__buf* out, uint32_t field_number, uint64_t value)
{
    return write_tag(out, field_number, WIRE_VARINT) && write_varint(out, value);
}

// protobuf int64 (not sint64) fields encode the two's-complement bit pattern
// as a plain (unsigned) varint - same convention libdatadog's own encoder uses.
static bool write_int64_field(ddog__buf* out, uint32_t field_number, int64_t value)
{
    return write_varint_field(out, field_number, (uint64_t)value);
}

static bool write_bytes_field(ddog__buf* out, uint32_t field_number, const void* data, size_t len)
{
    if (!write_tag(out, field_number, WIRE_LENGTH_DELIMITED))
    {
        return false;
    }
    if (!write_varint(out, (uint64_t)len))
    {
        return false;
    }
    return ddog__buf_append(out, data, len);
}

// Used both for embedded submessages and for packed-repeated scalars - at the
// wire level both are just "length-delimited field", the schema (not the
// bytes) is what tells a decoder which one it's looking at.
static bool write_message_field(ddog__buf* out, uint32_t field_number, const ddog__buf* submessage)
{
    return write_bytes_field(out, field_number, submessage->data, submessage->len);
}

// ---- message encoders ----

static bool encode_value_type(struct ddog_prof_profile* profile, const ddog_prof_value_type* vt, ddog__buf* out)
{
    uint32_t type_id;
    uint32_t unit_id;
    if (!ddog__intern_string(profile, vt->type, &type_id) || !ddog__intern_string(profile, vt->unit, &unit_id))
    {
        return false;
    }
    return write_varint_field(out, 1, type_id) && write_varint_field(out, 2, unit_id);
}

static bool encode_label(ddog__buf* out, uint32_t key, uint32_t str, int64_t num, uint32_t num_unit)
{
    return write_varint_field(out, 1, key) && (str == 0 || write_varint_field(out, 2, str)) &&
           (num == 0 || write_varint_field(out, 3, (uint64_t)num)) && (num_unit == 0 || write_varint_field(out, 4, num_unit));
}

static const ddog_label_key* labelset_labels(const struct ddog_prof_profile* profile, uint32_t id, size_t* out_len)
{
    *out_len = profile->labelsets.key_lens[id] / sizeof(ddog_label_key);
    return (const ddog_label_key*)profile->labelsets.keys[id]; // malloc'd, so suitably aligned
}

// ---- upscaling (port of libdatadog's UpscalingRules::upscale_values) ----

static bool slice_eq(ddog_charslice a, ddog_charslice b)
{
    return a.len == b.len && (a.len == 0 || memcmp(a.ptr, b.ptr, a.len) == 0);
}

// Rust's `f64 as i64`: saturating, NaN -> 0
static int64_t f64_to_i64_saturating(double value)
{
    if (isnan(value))
    {
        return 0;
    }
    if (value >= 9223372036854775807.0)
    {
        return INT64_MAX;
    }
    if (value <= -9223372036854775808.0)
    {
        return INT64_MIN;
    }
    return (int64_t)value;
}

static double compute_scale(const ddog_upscaling_rule_owned* rule, const int64_t* values)
{
    if (rule->kind == DDOG_UPSCALING_PROPORTIONAL)
    {
        return rule->scale;
    }
    int64_t sum = values[rule->sum_value_offset];
    int64_t count = values[rule->count_value_offset];
    if (sum == 0 || count == 0)
    {
        return 1.0;
    }
    double avg = (double)sum / (double)count;
    return 1.0 / (1.0 - exp(-avg / (double)rule->sampling_distance));
}

static void apply_rule(const ddog_upscaling_rule_owned* rule, int64_t* values)
{
    double scale = compute_scale(rule, values);
    for (size_t i = 0; i < rule->offsets_len; i++)
    {
        size_t offset = rule->offsets[i];
        // round() rounds half away from zero, like Rust's f64::round
        values[offset] = f64_to_i64_saturating(round((double)values[offset] * scale));
    }
}

// Applies the rules matching (label key, label value) for each label in
// order - a numeric label matches with an empty value - then the by-value
// rules (empty name and value). Within a group, rules apply in insertion
// order, each computing its scale from the values as modified so far.
static void apply_rules_for_label(const struct ddog_prof_profile* profile, ddog_charslice key, ddog_charslice value,
                                  int64_t* values)
{
    for (size_t r = 0; r < profile->upscaling_rules_len; r++)
    {
        const ddog_upscaling_rule_owned* rule = &profile->upscaling_rules[r];
        if (slice_eq(rule->label_name, key) && slice_eq(rule->label_value, value))
        {
            apply_rule(rule, values);
        }
    }
}

static void upscale_values(const struct ddog_prof_profile* profile, const ddog_label_key* labels, size_t labels_len,
                           const ddog_prof_endpoint_mapping_owned* endpoint, int64_t* values)
{
    if (profile->upscaling_rules_len == 0)
    {
        return;
    }
    for (size_t i = 0; i < labels_len; i++)
    {
        // a numeric label has str id 0, i.e. "": it matches a rule with an empty value
        apply_rules_for_label(profile, ddog__string(profile, labels[i].key), ddog__string(profile, labels[i].str), values);
    }
    if (endpoint != NULL)
    {
        ddog_charslice key = {"trace endpoint", sizeof("trace endpoint") - 1};
        apply_rules_for_label(profile, key, endpoint->endpoint, values);
    }
    ddog_charslice empty = {NULL, 0};
    apply_rules_for_label(profile, empty, empty, values);
}

// ---- endpoints ("local root span id" -> "trace endpoint" label) ----

typedef struct {
    uint64_t local_root_span_id;
    size_t mapping_index; // into profile->endpoint_mappings
} endpoint_lookup_entry;

static int compare_endpoint_lookup(const void* a, const void* b)
{
    const endpoint_lookup_entry* x = (const endpoint_lookup_entry*)a;
    const endpoint_lookup_entry* y = (const endpoint_lookup_entry*)b;
    if (x->local_root_span_id != y->local_root_span_id)
    {
        return x->local_root_span_id < y->local_root_span_id ? -1 : 1;
    }
    // keep insertion order among duplicates, so the last one can win
    return x->mapping_index < y->mapping_index ? -1 : (x->mapping_index > y->mapping_index ? 1 : 0);
}

// Sorted by span id with only the latest mapping kept per span id (a later
// set_endpoint replaces an earlier one, like libdatadog's HashMap insert).
static bool build_endpoint_lookup(const struct ddog_prof_profile* profile, endpoint_lookup_entry** out, size_t* out_len)
{
    *out = NULL;
    *out_len = 0;
    if (profile->endpoint_mappings_len == 0)
    {
        return true;
    }
    endpoint_lookup_entry* entries =
        (endpoint_lookup_entry*)malloc(profile->endpoint_mappings_len * sizeof(endpoint_lookup_entry));
    if (entries == NULL)
    {
        return false;
    }
    for (size_t i = 0; i < profile->endpoint_mappings_len; i++)
    {
        entries[i].local_root_span_id = profile->endpoint_mappings[i].local_root_span_id;
        entries[i].mapping_index = i;
    }
    qsort(entries, profile->endpoint_mappings_len, sizeof(endpoint_lookup_entry), compare_endpoint_lookup);
    size_t len = 0;
    for (size_t i = 0; i < profile->endpoint_mappings_len; i++)
    {
        if (len > 0 && entries[len - 1].local_root_span_id == entries[i].local_root_span_id)
        {
            entries[len - 1] = entries[i];
        }
        else
        {
            entries[len++] = entries[i];
        }
    }
    *out = entries;
    *out_len = len;
    return true;
}

static const ddog_prof_endpoint_mapping_owned* find_endpoint(const struct ddog_prof_profile* profile,
                                                             const endpoint_lookup_entry* lookup, size_t lookup_len,
                                                             const ddog_label_key* labels, size_t labels_len,
                                                             uint32_t local_root_span_id_key)
{
    if (lookup_len == 0)
    {
        return NULL;
    }
    for (size_t i = 0; i < labels_len; i++)
    {
        if (labels[i].key != local_root_span_id_key)
        {
            continue;
        }
        // numeric (validated on add); the backend reads the i64 bits as u64
        uint64_t span_id = (uint64_t)labels[i].num;
        size_t lo = 0;
        size_t hi = lookup_len;
        while (lo < hi)
        {
            size_t mid = lo + (hi - lo) / 2;
            if (lookup[mid].local_root_span_id < span_id)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        if (lo < lookup_len && lookup[lo].local_root_span_id == span_id)
        {
            return &profile->endpoint_mappings[lookup[lo].mapping_index];
        }
        return NULL;
    }
    return NULL;
}

// Reusable buffers, so that encoding a message does not allocate.
typedef struct {
    ddog__buf message;
    ddog__buf packed;
    ddog__buf field;
    int64_t* values;
    uint32_t trace_endpoint_key;
    uint32_t timestamp_key;
    uint32_t local_root_span_id_key;
    endpoint_lookup_entry* endpoint_lookup;
    size_t endpoint_lookup_len;
} encode_state;

// Labels are written like libdatadog: the sample's own labels, then the
// "trace endpoint" label (if the sample's local root span id has an
// endpoint), then the "end_timestamp_ns" label (if the sample has a timestamp).
static bool encode_sample(struct ddog_prof_profile* profile, encode_state* st, const uint8_t* record, ddog__buf* out)
{
    ddog_sample_header header;
    memcpy(&header, record, sizeof(header));
    size_t labels_len;
    const ddog_label_key* labels = labelset_labels(profile, header.labelset, &labels_len);
    const ddog_prof_endpoint_mapping_owned* endpoint =
        find_endpoint(profile, st->endpoint_lookup, st->endpoint_lookup_len, labels, labels_len, st->local_root_span_id_key);

    st->message.len = 0;

    // location ids (packed)
    size_t frames = profile->stacks.key_lens[header.stack] / sizeof(uint32_t);
    const uint8_t* stack = profile->stacks.keys[header.stack];
    if (frames > 0)
    {
        st->packed.len = 0;
        for (size_t i = 0; i < frames; i++)
        {
            uint32_t location_id;
            memcpy(&location_id, stack + i * sizeof(uint32_t), sizeof(location_id));
            if (!write_varint(&st->packed, (uint64_t)location_id + 1))
            {
                return false;
            }
        }
        if (!write_message_field(&st->message, 1, &st->packed))
        {
            return false;
        }
    }

    // values (packed), upscaled
    size_t values_len = profile->sample_types_len;
    memcpy(st->values, record + sizeof(header), values_len * sizeof(int64_t));
    upscale_values(profile, labels, labels_len, endpoint, st->values);
    st->packed.len = 0;
    for (size_t i = 0; i < values_len; i++)
    {
        if (!write_varint(&st->packed, (uint64_t)st->values[i]))
        {
            return false;
        }
    }
    if (!write_message_field(&st->message, 2, &st->packed))
    {
        return false;
    }

    // labels
    for (size_t i = 0; i < labels_len; i++)
    {
        st->field.len = 0;
        if (!encode_label(&st->field, labels[i].key, labels[i].str, labels[i].num, labels[i].num_unit) ||
            !write_message_field(&st->message, 3, &st->field))
        {
            return false;
        }
    }
    if (endpoint != NULL)
    {
        uint32_t endpoint_id;
        st->field.len = 0;
        if (!ddog__intern_string(profile, endpoint->endpoint, &endpoint_id) ||
            !encode_label(&st->field, st->trace_endpoint_key, endpoint_id, 0, 0) ||
            !write_message_field(&st->message, 3, &st->field))
        {
            return false;
        }
    }
    if (header.timestamp != 0)
    {
        st->field.len = 0;
        if (!encode_label(&st->field, st->timestamp_key, 0, header.timestamp, 0) ||
            !write_message_field(&st->message, 3, &st->field))
        {
            return false;
        }
    }

    return write_message_field(out, 2, &st->message);
}

static bool encode_samples(struct ddog_prof_profile* profile, encode_state* st, const ddog_sample_array* samples,
                           ddog__buf* out)
{
    size_t record_size = ddog__sample_record_size(profile);
    for (size_t i = 0; i < samples->len; i++)
    {
        if (!encode_sample(profile, st, samples->data + i * record_size, out))
        {
            return false;
        }
    }
    return true;
}

static bool encode_tables(const struct ddog_prof_profile* profile, encode_state* st, ddog__buf* out)
{
    for (uint32_t id = 0; id < profile->mappings.len; id++)
    {
        uint32_t filename_id;
        memcpy(&filename_id, profile->mappings.keys[id], sizeof(filename_id));
        st->message.len = 0;
        if (!write_varint_field(&st->message, 1, (uint64_t)id + 1) || !write_varint_field(&st->message, 5, filename_id) ||
            !write_message_field(out, 3, &st->message))
        {
            return false;
        }
    }

    for (uint32_t id = 0; id < profile->locations.len; id++)
    {
        ddog_location_key lk;
        memcpy(&lk, profile->locations.keys[id], sizeof(lk));
        st->field.len = 0;
        st->message.len = 0;
        if (!write_varint_field(&st->field, 1, (uint64_t)lk.function + 1) ||
            !write_varint_field(&st->field, 2, (uint64_t)lk.line) || !write_varint_field(&st->message, 1, (uint64_t)id + 1) ||
            (lk.mapping != 0 && !write_varint_field(&st->message, 2, lk.mapping)) ||
            (lk.address != 0 && !write_varint_field(&st->message, 3, lk.address)) ||
            !write_message_field(&st->message, 4, &st->field) || !write_message_field(out, 4, &st->message))
        {
            return false;
        }
    }

    for (uint32_t id = 0; id < profile->functions.len; id++)
    {
        ddog_function_key fk;
        memcpy(&fk, profile->functions.keys[id], sizeof(fk));
        st->message.len = 0;
        if (!write_varint_field(&st->message, 1, (uint64_t)id + 1) || !write_varint_field(&st->message, 2, fk.name) ||
            !write_varint_field(&st->message, 3, fk.system_name) || !write_varint_field(&st->message, 4, fk.filename) ||
            !write_message_field(out, 5, &st->message))
        {
            return false;
        }
    }
    return true;
}

bool ddog__pprof_encode(struct ddog_prof_profile* profile, const ddog_timespec* start_time,
                        const ddog_timespec* end_time, ddog__buf* out)
{
    encode_state st;
    memset(&st, 0, sizeof(st));
    ddog__buf_init(&st.message);
    ddog__buf_init(&st.packed);
    ddog__buf_init(&st.field);

    // libdatadog interns these three when the profile is created
    ddog_charslice trace_endpoint = {"trace endpoint", sizeof("trace endpoint") - 1};
    ddog_charslice timestamp = {"end_timestamp_ns", sizeof("end_timestamp_ns") - 1};
    ddog_charslice local_root_span_id = {"local root span id", sizeof("local root span id") - 1};
    st.values = (int64_t*)malloc(profile->sample_types_len * sizeof(int64_t));
    bool ok = st.values != NULL && ddog__intern_string(profile, trace_endpoint, &st.trace_endpoint_key) &&
              ddog__intern_string(profile, timestamp, &st.timestamp_key) &&
              ddog__intern_string(profile, local_root_span_id, &st.local_root_span_id_key) &&
              build_endpoint_lookup(profile, &st.endpoint_lookup, &st.endpoint_lookup_len);

    for (size_t i = 0; i < profile->sample_types_len && ok; i++)
    {
        st.message.len = 0;
        ok = encode_value_type(profile, &profile->sample_types[i], &st.message) && write_message_field(out, 1, &st.message);
    }

    // libdatadog writes the timestamped samples first, then the aggregated ones
    ok = ok && encode_samples(profile, &st, &profile->timestamped, out) &&
         encode_samples(profile, &st, &profile->aggregated, out) && encode_tables(profile, &st, out);

    // period type interns its strings: must happen before the string table is written
    ddog__buf period_type;
    ddog__buf_init(&period_type);
    ok = ok && encode_value_type(profile, &profile->period.type, &period_type);

    for (uint32_t id = 0; id < profile->strings.len && ok; id++)
    {
        ok = write_bytes_field(out, 6, profile->strings.keys[id], profile->strings.key_lens[id]);
    }
    if (ok)
    {
        int64_t time_nanos = start_time->seconds * 1000000000LL + (int64_t)start_time->nanoseconds;
        int64_t end_nanos = end_time->seconds * 1000000000LL + (int64_t)end_time->nanoseconds;
        ok = write_int64_field(out, 9, time_nanos) && write_int64_field(out, 10, end_nanos - time_nanos) &&
             write_message_field(out, 11, &period_type) && write_int64_field(out, 12, profile->period.value);
    }

    ddog__buf_free(&period_type);
    ddog__buf_free(&st.message);
    ddog__buf_free(&st.packed);
    ddog__buf_free(&st.field);
    free(st.values);
    free(st.endpoint_lookup);
    return ok;
}
