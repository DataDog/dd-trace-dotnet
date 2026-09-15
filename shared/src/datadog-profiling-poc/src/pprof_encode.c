// Hand-rolled encoder for Google's pprof profile.proto (see plan doc). Field
// numbers below are taken straight from that schema:
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
// Every string field above is actually an int64 index into Profile.string_table
// (index 0 must be ""). This is a hard wire-format requirement, not a style
// choice - see plan doc.
//
// Deliberately NOT deduplicating Mapping/Function/Location by content (each
// occurrence gets a fresh id) - only string interning is mandatory and is
// done below via a simple linear-scan table (fine at PoC scale; see plan doc).

#include "pprof_encode.h"

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

// ---- string table (linear scan - fine at PoC scale, see file header) ----

typedef struct {
    const char** ptrs; // borrowed - valid strings are owned by `profile` for the duration of this encode() call
    size_t* lens;
    size_t count;
    size_t capacity;
} string_table_t;

static void string_table_init(string_table_t* t)
{
    t->ptrs = NULL;
    t->lens = NULL;
    t->count = 0;
    t->capacity = 0;
}

static void string_table_free(string_table_t* t)
{
    free(t->ptrs);
    free(t->lens);
}

// Returns the index of `s`, inserting it if not already present. Returns
// SIZE_MAX on allocation failure.
static size_t string_table_intern(string_table_t* t, ddog_charslice s)
{
    for (size_t i = 0; i < t->count; i++)
    {
        if (t->lens[i] == s.len && (s.len == 0 || memcmp(t->ptrs[i], s.ptr, s.len) == 0))
        {
            return i;
        }
    }

    if (t->count == t->capacity)
    {
        size_t new_capacity = t->capacity == 0 ? 64 : t->capacity * 2;
        const char** new_ptrs = (const char**)realloc(t->ptrs, new_capacity * sizeof(const char*));
        if (new_ptrs == NULL)
        {
            return SIZE_MAX;
        }
        t->ptrs = new_ptrs;

        size_t* new_lens = (size_t*)realloc(t->lens, new_capacity * sizeof(size_t));
        if (new_lens == NULL)
        {
            return SIZE_MAX;
        }
        t->lens = new_lens;
        t->capacity = new_capacity;
    }

    t->ptrs[t->count] = s.ptr;
    t->lens[t->count] = s.len;
    return t->count++;
}

// ---- message encoders ----

static bool encode_value_type(string_table_t* strs, const ddog_prof_value_type* vt, ddog__buf* out)
{
    size_t type_idx = string_table_intern(strs, vt->type);
    size_t unit_idx = string_table_intern(strs, vt->unit);
    if (type_idx == SIZE_MAX || unit_idx == SIZE_MAX)
    {
        return false;
    }
    return write_varint_field(out, 1, (uint64_t)type_idx) && write_varint_field(out, 2, (uint64_t)unit_idx);
}

static bool encode_function(string_table_t* strs, uint64_t id, const ddog_prof_function* fn, ddog__buf* out)
{
    size_t name_idx = string_table_intern(strs, fn->name);
    size_t system_name_idx = string_table_intern(strs, fn->system_name);
    size_t filename_idx = string_table_intern(strs, fn->filename);
    if (name_idx == SIZE_MAX || system_name_idx == SIZE_MAX || filename_idx == SIZE_MAX)
    {
        return false;
    }

    return write_varint_field(out, 1, id) && write_varint_field(out, 2, (uint64_t)name_idx) &&
           write_varint_field(out, 3, (uint64_t)system_name_idx) && write_varint_field(out, 4, (uint64_t)filename_idx);
}

static bool encode_mapping(string_table_t* strs, uint64_t id, ddog_charslice filename, ddog__buf* out)
{
    size_t filename_idx = string_table_intern(strs, filename);
    if (filename_idx == SIZE_MAX)
    {
        return false;
    }
    return write_varint_field(out, 1, id) && write_varint_field(out, 5, (uint64_t)filename_idx);
}

static bool encode_label(string_table_t* strs, const ddog_prof_label_owned* label, ddog__buf* out)
{
    size_t key_idx = string_table_intern(strs, label->key);
    size_t str_idx = string_table_intern(strs, label->str);
    size_t num_unit_idx = string_table_intern(strs, label->num_unit);
    if (key_idx == SIZE_MAX || str_idx == SIZE_MAX || num_unit_idx == SIZE_MAX)
    {
        return false;
    }

    if (!write_varint_field(out, 1, (uint64_t)key_idx))
    {
        return false;
    }
    if (label->str.len > 0 && !write_varint_field(out, 2, (uint64_t)str_idx))
    {
        return false;
    }
    if (label->num != 0 && !write_varint_field(out, 3, (uint64_t)label->num))
    {
        return false;
    }
    if (label->num_unit.len > 0 && !write_varint_field(out, 4, (uint64_t)num_unit_idx))
    {
        return false;
    }
    return true;
}

// Encodes one location (and, as a side effect, exactly one fresh Function and
// at most one fresh Mapping into the Profile-level accumulators), and returns
// the fresh Location id via *out_location_id.
static bool encode_location(string_table_t* strs, uint64_t* next_id, const ddog_prof_location_owned* loc,
                             ddog__buf* mappings_accum, ddog__buf* functions_accum, ddog__buf* locations_accum,
                             uint64_t* out_location_id)
{
    uint64_t location_id = (*next_id)++;
    uint64_t function_id = (*next_id)++;

    ddog__buf fn_buf;
    ddog__buf_init(&fn_buf);
    bool ok = encode_function(strs, function_id, &loc->function, &fn_buf);
    if (ok)
    {
        ok = write_message_field(functions_accum, 5, &fn_buf);
    }
    ddog__buf_free(&fn_buf);
    if (!ok)
    {
        return false;
    }

    uint64_t mapping_id = 0;
    if (loc->mapping_filename.len > 0)
    {
        mapping_id = (*next_id)++;
        ddog__buf map_buf;
        ddog__buf_init(&map_buf);
        ok = encode_mapping(strs, mapping_id, loc->mapping_filename, &map_buf);
        if (ok)
        {
            ok = write_message_field(mappings_accum, 3, &map_buf);
        }
        ddog__buf_free(&map_buf);
        if (!ok)
        {
            return false;
        }
    }

    ddog__buf line_buf;
    ddog__buf_init(&line_buf);
    ok = write_varint_field(&line_buf, 1, function_id) && write_varint_field(&line_buf, 2, (uint64_t)loc->line);

    ddog__buf loc_buf;
    ddog__buf_init(&loc_buf);
    if (ok)
    {
        ok = write_varint_field(&loc_buf, 1, location_id);
    }
    if (ok && mapping_id != 0)
    {
        ok = write_varint_field(&loc_buf, 2, mapping_id);
    }
    if (ok && loc->address != 0)
    {
        ok = write_varint_field(&loc_buf, 3, loc->address);
    }
    if (ok)
    {
        ok = write_message_field(&loc_buf, 4, &line_buf);
    }
    ddog__buf_free(&line_buf);

    if (ok)
    {
        ok = write_message_field(locations_accum, 4, &loc_buf);
    }
    ddog__buf_free(&loc_buf);

    if (!ok)
    {
        return false;
    }

    *out_location_id = location_id;
    return true;
}

static bool encode_packed_uint64(const uint64_t* values, size_t len, ddog__buf* out, uint32_t field_number)
{
    if (len == 0)
    {
        return true;
    }
    ddog__buf packed;
    ddog__buf_init(&packed);
    bool ok = true;
    for (size_t i = 0; i < len && ok; i++)
    {
        ok = write_varint(&packed, values[i]);
    }
    if (ok)
    {
        ok = write_message_field(out, field_number, &packed);
    }
    ddog__buf_free(&packed);
    return ok;
}

static bool encode_sample(string_table_t* strs, uint64_t* next_id, const ddog_prof_sample_owned* sample,
                           ddog__buf* mappings_accum, ddog__buf* functions_accum, ddog__buf* locations_accum,
                           ddog__buf* samples_accum)
{
    uint64_t* location_ids = NULL;
    if (sample->locations_len > 0)
    {
        location_ids = (uint64_t*)malloc(sample->locations_len * sizeof(uint64_t));
        if (location_ids == NULL)
        {
            return false;
        }
    }

    bool ok = true;
    for (size_t i = 0; i < sample->locations_len && ok; i++)
    {
        ok = encode_location(strs, next_id, &sample->locations[i], mappings_accum, functions_accum, locations_accum,
                              &location_ids[i]);
    }

    ddog__buf sample_buf;
    ddog__buf_init(&sample_buf);

    if (ok)
    {
        ok = encode_packed_uint64(location_ids, sample->locations_len, &sample_buf, 1);
    }
    free(location_ids);

    if (ok && sample->values_len > 0)
    {
        ddog__buf packed;
        ddog__buf_init(&packed);
        for (size_t i = 0; i < sample->values_len && ok; i++)
        {
            ok = write_varint(&packed, (uint64_t)sample->values[i]);
        }
        if (ok)
        {
            ok = write_message_field(&sample_buf, 2, &packed);
        }
        ddog__buf_free(&packed);
    }

    for (size_t i = 0; i < sample->labels_len && ok; i++)
    {
        ddog__buf label_buf;
        ddog__buf_init(&label_buf);
        ok = encode_label(strs, &sample->labels[i], &label_buf);
        if (ok)
        {
            ok = write_message_field(&sample_buf, 3, &label_buf);
        }
        ddog__buf_free(&label_buf);
    }

    if (ok)
    {
        ok = write_message_field(samples_accum, 2, &sample_buf);
    }
    ddog__buf_free(&sample_buf);
    return ok;
}

bool ddog__pprof_encode(const struct ddog_prof_profile* profile, const ddog_timespec* start_time,
                         const ddog_timespec* end_time, ddog__buf* out)
{
    string_table_t strs;
    string_table_init(&strs);
    ddog_charslice empty_string;
    empty_string.ptr = NULL;
    empty_string.len = 0;
    if (string_table_intern(&strs, empty_string) != 0) // index 0 must be ""
    {
        string_table_free(&strs);
        return false;
    }

    ddog__buf sample_types_accum, mappings_accum, functions_accum, locations_accum, samples_accum, period_type_buf;
    ddog__buf_init(&sample_types_accum);
    ddog__buf_init(&mappings_accum);
    ddog__buf_init(&functions_accum);
    ddog__buf_init(&locations_accum);
    ddog__buf_init(&samples_accum);
    ddog__buf_init(&period_type_buf);

    uint64_t next_id = 1; // shared counter across Mapping/Location/Function ids - simple and still valid, see file header

    bool ok = true;
    for (size_t i = 0; i < profile->sample_types_len && ok; i++)
    {
        ddog__buf vt_buf;
        ddog__buf_init(&vt_buf);
        ok = encode_value_type(&strs, &profile->sample_types[i], &vt_buf);
        if (ok)
        {
            ok = write_message_field(&sample_types_accum, 1, &vt_buf);
        }
        ddog__buf_free(&vt_buf);
    }

    for (size_t i = 0; i < profile->samples_len && ok; i++)
    {
        ok = encode_sample(&strs, &next_id, &profile->samples[i], &mappings_accum, &functions_accum, &locations_accum,
                            &samples_accum);
    }

    // Must happen before we serialize the string table below - it interns
    // period.type's strings too.
    if (ok)
    {
        ok = encode_value_type(&strs, &profile->period.type, &period_type_buf);
    }

    // All interning above must be finished before this point - the table
    // written here has to be complete, see file header comment.
    ddog__buf string_table_accum;
    ddog__buf_init(&string_table_accum);
    for (size_t i = 0; i < strs.count && ok; i++)
    {
        ok = write_bytes_field(&string_table_accum, 6, strs.ptrs[i], strs.lens[i]);
    }

    if (ok)
    {
        ok = ddog__buf_append(out, sample_types_accum.data, sample_types_accum.len);
    }
    if (ok)
    {
        ok = ddog__buf_append(out, samples_accum.data, samples_accum.len);
    }
    if (ok)
    {
        ok = ddog__buf_append(out, mappings_accum.data, mappings_accum.len);
    }
    if (ok)
    {
        ok = ddog__buf_append(out, locations_accum.data, locations_accum.len);
    }
    if (ok)
    {
        ok = ddog__buf_append(out, functions_accum.data, functions_accum.len);
    }
    if (ok)
    {
        ok = ddog__buf_append(out, string_table_accum.data, string_table_accum.len);
    }
    if (ok)
    {
        int64_t time_nanos = start_time->seconds * 1000000000LL + (int64_t)start_time->nanoseconds;
        int64_t end_nanos = end_time->seconds * 1000000000LL + (int64_t)end_time->nanoseconds;
        ok = write_int64_field(out, 9, time_nanos) && write_int64_field(out, 10, end_nanos - time_nanos);
    }
    if (ok)
    {
        ok = write_message_field(out, 11, &period_type_buf);
    }
    if (ok)
    {
        ok = write_int64_field(out, 12, profile->period.value);
    }

    ddog__buf_free(&sample_types_accum);
    ddog__buf_free(&mappings_accum);
    ddog__buf_free(&functions_accum);
    ddog__buf_free(&locations_accum);
    ddog__buf_free(&samples_accum);
    ddog__buf_free(&period_type_buf);
    ddog__buf_free(&string_table_accum);
    string_table_free(&strs);

    return ok;
}
