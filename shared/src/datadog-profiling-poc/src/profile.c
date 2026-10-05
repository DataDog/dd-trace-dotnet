#include "datadog_poc/profiling.h"
#include "encoded_profile.h"
#include "internal.h"
#include "pprof_encode.h"
#include "profile.h"
#include "zstd_compress.h"

#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#if defined(_WIN32)
#include <windows.h>
#else
#include <time.h>
#endif

// Not using C11's timespec_get() here: MSVC's UCRT support for it is
// inconsistent depending on the exact Windows SDK/VS version pairing (still
// unsupported as of VS2019 16.10; even where present, its linkage changed
// between SDK revisions) - this repo pins WindowsTargetPlatformVersion
// 10.0.19041.0, an SDK old enough that it isn't a safe bet. Using the native
// Win32 API there instead; GetSystemTimePreciseAsFileTime has been available
// since Windows 8 / Server 2012, well below anything this repo targets.
static ddog_timespec ddog__now(void)
{
    ddog_timespec out;
#if defined(_WIN32)
    FILETIME ft;
    GetSystemTimePreciseAsFileTime(&ft);
    uint64_t ticks = ((uint64_t)ft.dwHighDateTime << 32) | ft.dwLowDateTime; // 100ns intervals since 1601-01-01
    static const uint64_t EPOCH_DIFF_100NS = 116444736000000000ULL;          // 1601-01-01 -> 1970-01-01
    uint64_t unix_100ns = ticks - EPOCH_DIFF_100NS;
    out.seconds = (int64_t)(unix_100ns / 10000000ULL);
    out.nanoseconds = (uint32_t)((unix_100ns % 10000000ULL) * 100);
#else
    struct timespec ts;
    timespec_get(&ts, TIME_UTC);
    out.seconds = (int64_t)ts.tv_sec;
    out.nanoseconds = (uint32_t)ts.tv_nsec;
#endif
    return out;
}

static bool dup_value_type(const ddog_prof_value_type* in, ddog_prof_value_type* out)
{
    if (!ddog__charslice_dup(in->type, &out->type))
    {
        return false;
    }
    if (!ddog__charslice_dup(in->unit, &out->unit))
    {
        ddog__charslice_free(&out->type);
        return false;
    }
    return true;
}

static void free_value_type(ddog_prof_value_type* vt)
{
    ddog__charslice_free(&vt->type);
    ddog__charslice_free(&vt->unit);
}

// ---- interner ----

static uint64_t rotl64(uint64_t x, int r)
{
    return (x << r) | (x >> (64 - r));
}

// FxHash (rustc-hash, what libdatadog's FxIndexMap uses), 8 bytes at a time,
// with a final mix so the low bits used as the slot index are well spread.
static uint64_t fx_hash(const void* data, size_t len)
{
    static const uint64_t K = 0x517cc1b727220a95ULL;
    const uint8_t* p = (const uint8_t*)data;
    uint64_t h = (uint64_t)len * K;
    while (len >= 8)
    {
        uint64_t w;
        memcpy(&w, p, 8);
        h = (rotl64(h, 5) ^ w) * K;
        p += 8;
        len -= 8;
    }
    if (len >= 4)
    {
        uint32_t w;
        memcpy(&w, p, 4);
        h = (rotl64(h, 5) ^ w) * K;
        p += 4;
        len -= 4;
    }
    while (len > 0)
    {
        h = (rotl64(h, 5) ^ *p) * K;
        p++;
        len--;
    }
    h ^= h >> 31;
    h *= 0xbf58476d1ce4e5b9ULL;
    h ^= h >> 29;
    return h;
}

static void interner_clear(ddog_interner* t)
{
    for (uint32_t i = 0; i < t->len; i++)
    {
        free(t->keys[i]);
    }
    t->len = 0;
    if (t->slots != NULL)
    {
        memset(t->slots, 0, t->slots_len * sizeof(uint32_t));
    }
}

static void interner_free(ddog_interner* t)
{
    interner_clear(t);
    free(t->keys);
    free(t->key_lens);
    free(t->hashes);
    free(t->slots);
    memset(t, 0, sizeof(*t));
}

// Looks `key` up. On a miss, *out_slot is the empty slot where it would go.
static bool interner_lookup(const ddog_interner* t, const void* key, size_t len, uint64_t hash, uint32_t* out_id,
                            uint32_t* out_slot)
{
    if (t->slots_len == 0)
    {
        return false;
    }
    uint32_t mask = t->slots_len - 1;
    uint32_t slot = (uint32_t)hash & mask;
    while (t->slots[slot] != 0)
    {
        uint32_t id = t->slots[slot] - 1;
        if (t->hashes[id] == hash && t->key_lens[id] == len && (len == 0 || memcmp(t->keys[id], key, len) == 0))
        {
            *out_id = id;
            return true;
        }
        slot = (slot + 1) & mask;
    }
    *out_slot = slot;
    return false;
}

// Keeps the slot table at most half full.
static bool interner_reserve_slot(ddog_interner* t)
{
    if (t->slots_len != 0 && ((size_t)t->len + 1) * 2 <= t->slots_len)
    {
        return true;
    }
    uint32_t new_len = t->slots_len == 0 ? 64 : t->slots_len * 2;
    uint32_t* new_slots = (uint32_t*)calloc(new_len, sizeof(uint32_t));
    if (new_slots == NULL)
    {
        return false;
    }
    uint32_t mask = new_len - 1;
    for (uint32_t id = 0; id < t->len; id++)
    {
        uint32_t slot = (uint32_t)t->hashes[id] & mask;
        while (new_slots[slot] != 0)
        {
            slot = (slot + 1) & mask;
        }
        new_slots[slot] = id + 1;
    }
    free(t->slots);
    t->slots = new_slots;
    t->slots_len = new_len;
    return true;
}

// Returns the id of `key`, inserting a copy of it if it's new.
static bool interner_intern(ddog_interner* t, const void* key, size_t len, uint32_t* out_id, bool* out_inserted)
{
    if (len > UINT32_MAX || t->len == UINT32_MAX - 1 || !interner_reserve_slot(t))
    {
        return false;
    }
    uint64_t hash = fx_hash(key, len);
    uint32_t slot = 0;
    if (interner_lookup(t, key, len, hash, out_id, &slot))
    {
        if (out_inserted != NULL)
        {
            *out_inserted = false;
        }
        return true;
    }

    if (t->len == t->capacity)
    {
        uint32_t new_capacity = t->capacity == 0 ? 64 : t->capacity * 2;
        uint8_t** keys = (uint8_t**)realloc(t->keys, new_capacity * sizeof(uint8_t*));
        if (keys == NULL)
        {
            return false;
        }
        t->keys = keys;
        uint32_t* key_lens = (uint32_t*)realloc(t->key_lens, new_capacity * sizeof(uint32_t));
        if (key_lens == NULL)
        {
            return false;
        }
        t->key_lens = key_lens;
        uint64_t* hashes = (uint64_t*)realloc(t->hashes, new_capacity * sizeof(uint64_t));
        if (hashes == NULL)
        {
            return false;
        }
        t->hashes = hashes;
        t->capacity = new_capacity;
    }

    uint8_t* copy = (uint8_t*)malloc(len == 0 ? 1 : len);
    if (copy == NULL)
    {
        return false;
    }
    if (len > 0)
    {
        memcpy(copy, key, len);
    }
    uint32_t id = t->len++;
    t->keys[id] = copy;
    t->key_lens[id] = (uint32_t)len;
    t->hashes[id] = hash;
    t->slots[slot] = id + 1;
    *out_id = id;
    if (out_inserted != NULL)
    {
        *out_inserted = true;
    }
    return true;
}

bool ddog__intern_string(struct ddog_prof_profile* profile, ddog_charslice s, uint32_t* out_id)
{
    return interner_intern(&profile->strings, s.ptr, s.len, out_id, NULL);
}

bool ddog__find_string(const struct ddog_prof_profile* profile, ddog_charslice s, uint32_t* out_id)
{
    uint32_t slot;
    return interner_lookup(&profile->strings, s.ptr, s.len, fx_hash(s.ptr, s.len), out_id, &slot);
}

static void free_interners(struct ddog_prof_profile* profile)
{
    interner_free(&profile->strings);
    interner_free(&profile->functions);
    interner_free(&profile->mappings);
    interner_free(&profile->locations);
    interner_free(&profile->stacks);
    interner_free(&profile->labelsets);
    interner_free(&profile->stack_cache);
    interner_free(&profile->aggregation);
}

// Empties every interner (keeping their capacity) and re-adds "" as string 0.
static bool clear_interners(struct ddog_prof_profile* profile)
{
    interner_clear(&profile->strings);
    interner_clear(&profile->functions);
    interner_clear(&profile->mappings);
    interner_clear(&profile->locations);
    interner_clear(&profile->stacks);
    interner_clear(&profile->labelsets);
    interner_clear(&profile->stack_cache);
    interner_clear(&profile->aggregation);
    ddog_charslice empty = {NULL, 0};
    uint32_t id;
    return ddog__intern_string(profile, empty, &id) && id == 0; // pprof requires string_table[0] == ""
}

static bool ensure_scratch(struct ddog_prof_profile* profile, size_t size)
{
    if (size <= profile->scratch_capacity)
    {
        return true;
    }
    size_t new_capacity = profile->scratch_capacity == 0 ? 4096 : profile->scratch_capacity;
    while (new_capacity < size)
    {
        new_capacity *= 2;
    }
    uint8_t* p = (uint8_t*)realloc(profile->scratch, new_capacity);
    if (p == NULL)
    {
        return false;
    }
    profile->scratch = p;
    profile->scratch_capacity = new_capacity;
    return true;
}

// ---- stacks ----

static bool slice_eq(ddog_charslice a, ddog_charslice b)
{
    return a.len == b.len && (a.len == 0 || memcmp(a.ptr, b.ptr, a.len) == 0);
}

static bool slice_eq_cstr(ddog_charslice a, const char* s)
{
    size_t len = strlen(s);
    return a.len == len && (len == 0 || memcmp(a.ptr, s, len) == 0);
}

static bool intern_location(struct ddog_prof_profile* profile, const ddog_prof_location* in, uint32_t* out_id)
{
    ddog_function_key fk;
    memset(&fk, 0, sizeof(fk));
    if (!ddog__intern_string(profile, in->function.name, &fk.name) ||
        !ddog__intern_string(profile, in->function.system_name, &fk.system_name) ||
        !ddog__intern_string(profile, in->function.filename, &fk.filename))
    {
        return false;
    }

    ddog_location_key lk;
    memset(&lk, 0, sizeof(lk));
    if (!interner_intern(&profile->functions, &fk, sizeof(fk), &lk.function, NULL))
    {
        return false;
    }
    if (in->mapping_filename.len > 0)
    {
        uint32_t filename_id;
        uint32_t mapping_id;
        if (!ddog__intern_string(profile, in->mapping_filename, &filename_id) ||
            !interner_intern(&profile->mappings, &filename_id, sizeof(filename_id), &mapping_id, NULL))
        {
            return false;
        }
        lk.mapping = mapping_id + 1;
    }
    lk.address = in->address;
    lk.line = in->line;
    return interner_intern(&profile->locations, &lk, sizeof(lk), out_id, NULL);
}

// One input frame, as the stack cache sees it: where its strings are, not
// what they contain.
typedef struct {
    const char* ptr[4]; // name, system_name, filename, mapping_filename
    uint64_t len[4];
    uint64_t address;
    int64_t line;
} frame_cache_key;

// True if the interned stack `stack_id` has exactly the content of `frames`.
static bool stack_matches(const struct ddog_prof_profile* profile, uint32_t stack_id, const ddog_prof_location* frames,
                          size_t frames_len)
{
    if (profile->stacks.key_lens[stack_id] != frames_len * sizeof(uint32_t))
    {
        return false;
    }
    const uint8_t* stack = profile->stacks.keys[stack_id];
    for (size_t i = 0; i < frames_len; i++)
    {
        const ddog_prof_location* in = &frames[i];
        uint32_t location_id;
        memcpy(&location_id, stack + i * sizeof(uint32_t), sizeof(uint32_t));
        ddog_location_key lk;
        memcpy(&lk, profile->locations.keys[location_id], sizeof(lk));
        if (lk.address != in->address || lk.line != in->line)
        {
            return false;
        }
        ddog_function_key fk;
        memcpy(&fk, profile->functions.keys[lk.function], sizeof(fk));
        if (!slice_eq(ddog__string(profile, fk.name), in->function.name) ||
            !slice_eq(ddog__string(profile, fk.system_name), in->function.system_name) ||
            !slice_eq(ddog__string(profile, fk.filename), in->function.filename))
        {
            return false;
        }
        if (lk.mapping == 0)
        {
            if (in->mapping_filename.len != 0)
            {
                return false;
            }
        }
        else
        {
            uint32_t filename_id;
            memcpy(&filename_id, profile->mappings.keys[lk.mapping - 1], sizeof(filename_id));
            if (!slice_eq(ddog__string(profile, filename_id), in->mapping_filename))
            {
                return false;
            }
        }
    }
    return true;
}

// Returns the stack id of the sample's frames. A callstack seen before at the
// same addresses is found with one lookup in the stack cache (and verified);
// otherwise every frame is interned, then the stack.
static bool resolve_stack(struct ddog_prof_profile* profile, const ddog_prof_sample* sample, uint32_t* out_stack_id)
{
    size_t n = sample->locations_len;
    size_t cache_key_len = n * sizeof(frame_cache_key);
    if (!ensure_scratch(profile, cache_key_len + n * sizeof(uint32_t)))
    {
        return false;
    }
    frame_cache_key* cache_key = (frame_cache_key*)profile->scratch;
    memset(cache_key, 0, cache_key_len);
    for (size_t i = 0; i < n; i++)
    {
        const ddog_prof_location* in = &sample->locations[i];
        cache_key[i].ptr[0] = in->function.name.ptr;
        cache_key[i].len[0] = in->function.name.len;
        cache_key[i].ptr[1] = in->function.system_name.ptr;
        cache_key[i].len[1] = in->function.system_name.len;
        cache_key[i].ptr[2] = in->function.filename.ptr;
        cache_key[i].len[2] = in->function.filename.len;
        cache_key[i].ptr[3] = in->mapping_filename.ptr;
        cache_key[i].len[3] = in->mapping_filename.len;
        cache_key[i].address = in->address;
        cache_key[i].line = in->line;
    }

    uint32_t cache_id = 0;
    uint32_t slot;
    bool cached = interner_lookup(&profile->stack_cache, cache_key, cache_key_len, fx_hash(cache_key, cache_key_len),
                                  &cache_id, &slot);
    if (cached && stack_matches(profile, profile->stack_cache_ids[cache_id], sample->locations, n))
    {
        *out_stack_id = profile->stack_cache_ids[cache_id];
        return true;
    }

    uint32_t* location_ids = (uint32_t*)(profile->scratch + cache_key_len);
    for (size_t i = 0; i < n; i++)
    {
        if (!intern_location(profile, &sample->locations[i], &location_ids[i]))
        {
            return false;
        }
    }
    uint32_t stack_id;
    if (!interner_intern(&profile->stacks, location_ids, n * sizeof(uint32_t), &stack_id, NULL))
    {
        return false;
    }

    if (!cached && !interner_intern(&profile->stack_cache, cache_key, cache_key_len, &cache_id, NULL))
    {
        return false;
    }
    if (cache_id >= profile->stack_cache_ids_capacity)
    {
        size_t new_capacity = profile->stack_cache_ids_capacity == 0 ? 64 : profile->stack_cache_ids_capacity * 2;
        while (new_capacity <= cache_id)
        {
            new_capacity *= 2;
        }
        uint32_t* ids = (uint32_t*)realloc(profile->stack_cache_ids, new_capacity * sizeof(uint32_t));
        if (ids == NULL)
        {
            return false;
        }
        profile->stack_cache_ids = ids;
        profile->stack_cache_ids_capacity = new_capacity;
    }
    // a cached entry whose content changed (caller buffer reused) now points to the new stack
    profile->stack_cache_ids[cache_id] = stack_id;
    *out_stack_id = stack_id;
    return true;
}

static bool resolve_labelset(struct ddog_prof_profile* profile, const ddog_prof_sample* sample, uint32_t* out_id)
{
    size_t key_len = sample->labels_len * sizeof(ddog_label_key);
    if (!ensure_scratch(profile, key_len))
    {
        return false;
    }
    ddog_label_key* key = (ddog_label_key*)profile->scratch;
    memset(key, 0, key_len);
    for (size_t i = 0; i < sample->labels_len; i++)
    {
        const ddog_prof_label* in = &sample->labels[i];
        if (!ddog__intern_string(profile, in->key, &key[i].key))
        {
            return false;
        }
        if (in->str.len > 0)
        {
            if (!ddog__intern_string(profile, in->str, &key[i].str))
            {
                return false;
            }
        }
        else
        {
            key[i].num = in->num;
            if (in->num_unit.len > 0 && !ddog__intern_string(profile, in->num_unit, &key[i].num_unit))
            {
                return false;
            }
        }
    }
    return interner_intern(&profile->labelsets, key, key_len, out_id, NULL);
}

// ---- samples ----

static bool sample_array_reserve(ddog_sample_array* a, size_t record_size, size_t count)
{
    if (count <= a->capacity)
    {
        return true;
    }
    size_t new_capacity = a->capacity == 0 ? 256 : a->capacity * 2;
    while (new_capacity < count)
    {
        new_capacity *= 2;
    }
    uint8_t* data = (uint8_t*)realloc(a->data, new_capacity * record_size);
    if (data == NULL)
    {
        return false;
    }
    a->data = data;
    a->capacity = new_capacity;
    return true;
}

// libdatadog sums aggregated values with i64::saturating_add
static int64_t saturating_add(int64_t a, int64_t b)
{
    if (b > 0 && a > INT64_MAX - b)
    {
        return INT64_MAX;
    }
    if (b < 0 && a < INT64_MIN - b)
    {
        return INT64_MIN;
    }
    return a + b;
}

// Same checks as libdatadog's Profile::validate_sample_labels
static ddog_error_code validate_sample_labels(const ddog_prof_sample* sample)
{
    for (size_t i = 0; i < sample->labels_len; i++)
    {
        const ddog_prof_label* label = &sample->labels[i];
        for (size_t j = 0; j < i; j++)
        {
            if (slice_eq(sample->labels[j].key, label->key))
            {
                return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "Duplicate label on sample: %.*s", (int)label->key.len,
                                  label->key.ptr);
            }
        }
        if (slice_eq_cstr(label->key, "local root span id") && (label->str.len != 0 || label->num == 0))
        {
            return ddog__fail(DDOG_ERR_INVALID_ARGUMENT,
                              "Invalid \"local root span id\" label: it must be a non-zero number, not a string");
        }
        if (slice_eq_cstr(label->key, "end_timestamp_ns"))
        {
            return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "Timestamp should not be passed as a label");
        }
    }
    return DDOG_OK;
}

static void reset_upscaling_rules(struct ddog_prof_profile* profile);

ddog_error_code ddog_prof_profile_new(const ddog_prof_value_type* sample_types, size_t sample_types_len,
                                       const ddog_prof_period* period,
                                       ddog_prof_profile** out_profile)
{
    if (sample_types == NULL || sample_types_len == 0)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "sample_types must be non-empty");
    }
    if (period == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "period is NULL");
    }
    if (out_profile == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "out_profile is NULL");
    }

    struct ddog_prof_profile* profile = (struct ddog_prof_profile*)calloc(1, sizeof(struct ddog_prof_profile));
    if (profile == NULL)
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to allocate profile");
    }

    profile->sample_types = (ddog_prof_value_type*)calloc(sample_types_len, sizeof(ddog_prof_value_type));
    if (profile->sample_types == NULL)
    {
        free(profile);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to allocate sample types");
    }
    for (size_t i = 0; i < sample_types_len; i++)
    {
        if (!dup_value_type(&sample_types[i], &profile->sample_types[i]))
        {
            profile->sample_types_len = i;
            ddog_prof_profile_drop(profile);
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy sample_types[%zu]", i);
        }
    }
    profile->sample_types_len = sample_types_len;

    if (!dup_value_type(&period->type, &profile->period.type) || !clear_interners(profile))
    {
        ddog_prof_profile_drop(profile);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to initialize the profile");
    }
    profile->period.value = period->value;
    profile->start_time = ddog__now();

    *out_profile = profile;
    return DDOG_OK;
}

void ddog_prof_profile_drop(ddog_prof_profile* profile)
{
    if (profile == NULL)
    {
        return;
    }

    for (size_t i = 0; i < profile->sample_types_len; i++)
    {
        free_value_type(&profile->sample_types[i]);
    }
    free(profile->sample_types);
    free_value_type(&profile->period.type);

    free_interners(profile);
    free(profile->stack_cache_ids);
    free(profile->scratch);
    free(profile->timestamped.data);
    free(profile->aggregated.data);

    for (size_t i = 0; i < profile->endpoint_counts_len; i++)
    {
        ddog__charslice_free(&profile->endpoint_counts[i].endpoint);
    }
    free(profile->endpoint_counts);

    for (size_t i = 0; i < profile->endpoint_mappings_len; i++)
    {
        ddog__charslice_free(&profile->endpoint_mappings[i].endpoint);
    }
    free(profile->endpoint_mappings);

    reset_upscaling_rules(profile);

    free(profile);
}

ddog_error_code ddog_prof_profile_add(ddog_prof_profile* profile, const ddog_prof_sample* sample, int64_t timestamp)
{
    if (profile == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "profile is NULL");
    }
    if (sample == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "sample is NULL");
    }
    if (sample->values_len != profile->sample_types_len)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "sample has %zu values but profile declares %zu sample types", sample->values_len, profile->sample_types_len);
    }
    if ((sample->locations == NULL && sample->locations_len > 0) || (sample->labels == NULL && sample->labels_len > 0) ||
        (sample->values == NULL && sample->values_len > 0))
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "sample has a NULL array with a non-zero length");
    }
    ddog_error_code validation = validate_sample_labels(sample);
    if (validation != DDOG_OK)
    {
        return validation;
    }

    ddog_sample_header header;
    memset(&header, 0, sizeof(header));
    if (!resolve_stack(profile, sample, &header.stack) || !resolve_labelset(profile, sample, &header.labelset))
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to intern the sample");
    }
    header.timestamp = timestamp;

    size_t record_size = ddog__sample_record_size(profile);
    size_t values_size = sample->values_len * sizeof(int64_t);

    // Like libdatadog: samples with a timestamp are all kept separately,
    // samples without one are aggregated (values summed) per (stack, label set).
    if (timestamp != 0)
    {
        ddog_sample_array* a = &profile->timestamped;
        if (!sample_array_reserve(a, record_size, a->len + 1))
        {
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to grow the samples array");
        }
        uint8_t* record = a->data + a->len * record_size;
        memcpy(record, &header, sizeof(header));
        if (values_size > 0)
        {
            memcpy(record + sizeof(header), sample->values, values_size);
        }
        a->len += 1;
        return DDOG_OK;
    }

    ddog_sample_array* a = &profile->aggregated;
    if (!sample_array_reserve(a, record_size, a->len + 1))
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to grow the samples array");
    }
    uint32_t pair[2] = {header.stack, header.labelset};
    uint32_t index;
    bool inserted;
    if (!interner_intern(&profile->aggregation, pair, sizeof(pair), &index, &inserted))
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to grow the sample aggregation index");
    }
    uint8_t* record = a->data + (size_t)index * record_size;
    if (inserted)
    {
        // aggregation ids are dense, so the new id is the next record
        memcpy(record, &header, sizeof(header));
        if (values_size > 0)
        {
            memcpy(record + sizeof(header), sample->values, values_size);
        }
        a->len += 1;
    }
    else
    {
        for (size_t i = 0; i < sample->values_len; i++)
        {
            int64_t current;
            memcpy(&current, record + sizeof(header) + i * sizeof(int64_t), sizeof(current));
            current = saturating_add(current, sample->values[i]);
            memcpy(record + sizeof(header) + i * sizeof(int64_t), &current, sizeof(current));
        }
    }
    return DDOG_OK;
}

ddog_error_code ddog_prof_profile_set_endpoint(ddog_prof_profile* profile, uint64_t local_root_span_id, ddog_charslice endpoint)
{
    if (profile == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "profile is NULL");
    }

    if (profile->endpoint_mappings_len == profile->endpoint_mappings_capacity)
    {
        size_t new_capacity = profile->endpoint_mappings_capacity == 0 ? 8 : profile->endpoint_mappings_capacity * 2;
        ddog_prof_endpoint_mapping_owned* new_ptr = (ddog_prof_endpoint_mapping_owned*)realloc(
            profile->endpoint_mappings, new_capacity * sizeof(ddog_prof_endpoint_mapping_owned));
        if (new_ptr == NULL)
        {
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to grow endpoint mapping array");
        }
        profile->endpoint_mappings = new_ptr;
        profile->endpoint_mappings_capacity = new_capacity;
    }

    ddog_prof_endpoint_mapping_owned* slot = &profile->endpoint_mappings[profile->endpoint_mappings_len];
    if (!ddog__charslice_dup(endpoint, &slot->endpoint))
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy endpoint name");
    }
    slot->local_root_span_id = local_root_span_id;
    profile->endpoint_mappings_len += 1;
    return DDOG_OK;
}

ddog_error_code ddog_prof_profile_add_endpoint_count(ddog_prof_profile* profile, ddog_charslice endpoint, int64_t value)
{
    if (profile == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "profile is NULL");
    }

    // Summed per endpoint name, like libdatadog's ProfiledEndpointsStats
    for (size_t i = 0; i < profile->endpoint_counts_len; i++)
    {
        if (slice_eq(profile->endpoint_counts[i].endpoint, endpoint))
        {
            // unsigned arithmetic: wraps like Rust's release-mode `+=` instead of being UB
            profile->endpoint_counts[i].value = (int64_t)((uint64_t)profile->endpoint_counts[i].value + (uint64_t)value);
            return DDOG_OK;
        }
    }

    if (profile->endpoint_counts_len == profile->endpoint_counts_capacity)
    {
        size_t new_capacity = profile->endpoint_counts_capacity == 0 ? 8 : profile->endpoint_counts_capacity * 2;
        ddog_prof_endpoint_count_owned* new_ptr = (ddog_prof_endpoint_count_owned*)realloc(
            profile->endpoint_counts, new_capacity * sizeof(ddog_prof_endpoint_count_owned));
        if (new_ptr == NULL)
        {
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to grow endpoint counts array");
        }
        profile->endpoint_counts = new_ptr;
        profile->endpoint_counts_capacity = new_capacity;
    }

    ddog_prof_endpoint_count_owned* slot = &profile->endpoint_counts[profile->endpoint_counts_len];
    if (!ddog__charslice_dup(endpoint, &slot->endpoint))
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy endpoint name");
    }
    slot->value = value;
    profile->endpoint_counts_len += 1;
    return DDOG_OK;
}

static void free_upscaling_rule(ddog_upscaling_rule_owned* rule)
{
    free(rule->offsets);
    ddog__charslice_free(&rule->label_name);
    ddog__charslice_free(&rule->label_value);
}

static void reset_upscaling_rules(struct ddog_prof_profile* profile)
{
    for (size_t i = 0; i < profile->upscaling_rules_len; i++)
    {
        free_upscaling_rule(&profile->upscaling_rules[i]);
    }
    free(profile->upscaling_rules);
    profile->upscaling_rules = NULL;
    profile->upscaling_rules_len = 0;
}

static bool is_by_value_rule(ddog_charslice label_name, ddog_charslice label_value)
{
    return label_name.len == 0 && label_value.len == 0;
}

static bool offsets_overlap(const size_t* a, size_t a_len, const size_t* b, size_t b_len)
{
    for (size_t i = 0; i < a_len; i++)
    {
        for (size_t j = 0; j < b_len; j++)
        {
            if (a[i] == b[j])
            {
                return true;
            }
        }
    }
    return false;
}

static int compare_size_t(const void* a, const void* b)
{
    size_t x = *(const size_t*)a;
    size_t y = *(const size_t*)b;
    return x < y ? -1 : (x > y ? 1 : 0);
}

// Port of libdatadog's UpscalingRules::add (offset range, collision checks,
// UpscalingInfo::check_validity), storing the rule for encode time.
static ddog_error_code add_upscaling_rule(struct ddog_prof_profile* profile, const size_t* offset_values,
                                          size_t offset_values_len, ddog_charslice label_name,
                                          ddog_charslice label_value, ddog_upscaling_rule_owned* rule)
{
    size_t max_offset = profile->sample_types_len;
    for (size_t i = 0; i < offset_values_len; i++)
    {
        if (offset_values[i] >= max_offset)
        {
            return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "Invalid offset. Highest expected offset: %zu", max_offset);
        }
    }

    bool by_value = is_by_value_rule(label_name, label_value);
    for (size_t i = 0; i < profile->upscaling_rules_len; i++)
    {
        const ddog_upscaling_rule_owned* existing = &profile->upscaling_rules[i];
        if (!offsets_overlap(existing->offsets, existing->offsets_len, offset_values, offset_values_len))
        {
            continue;
        }
        bool existing_by_value = is_by_value_rule(existing->label_name, existing->label_value);
        if (slice_eq(existing->label_name, label_name) && slice_eq(existing->label_value, label_value))
        {
            return ddog__fail(DDOG_ERR_INVALID_ARGUMENT,
                              "There are duplicated by-label rules for the same label name: %.*s with at least one value offset in common.",
                              (int)label_name.len, label_name.ptr);
        }
        if (by_value && !existing_by_value)
        {
            return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "The by-value rule is colliding with at least one by-label rule");
        }
        if (!by_value && existing_by_value)
        {
            return ddog__fail(DDOG_ERR_INVALID_ARGUMENT,
                              "The by-label rule (label name %.*s, label value %.*s) is colliding with a by-value rule on values offsets",
                              (int)label_name.len, label_name.ptr, (int)label_value.len, label_value.ptr);
        }
    }

    if (rule->kind == DDOG_UPSCALING_POISSON &&
        (rule->sum_value_offset >= max_offset || rule->count_value_offset >= max_offset))
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT,
                          "sum_value_offset %zu and count_value_offset %zu must be strictly less than %zu",
                          rule->sum_value_offset, rule->count_value_offset, max_offset);
    }

    ddog_upscaling_rule_owned* new_rules = (ddog_upscaling_rule_owned*)realloc(
        profile->upscaling_rules, (profile->upscaling_rules_len + 1) * sizeof(ddog_upscaling_rule_owned));
    if (new_rules == NULL)
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to grow upscaling rules array");
    }
    profile->upscaling_rules = new_rules;

    rule->offsets = NULL;
    rule->offsets_len = 0;
    rule->label_name.ptr = NULL;
    rule->label_name.len = 0;
    rule->label_value.ptr = NULL;
    rule->label_value.len = 0;
    if (offset_values_len > 0)
    {
        rule->offsets = (size_t*)malloc(offset_values_len * sizeof(size_t));
        if (rule->offsets == NULL)
        {
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy upscaling rule offsets");
        }
        memcpy(rule->offsets, offset_values, offset_values_len * sizeof(size_t));
        qsort(rule->offsets, offset_values_len, sizeof(size_t), compare_size_t);
        rule->offsets_len = offset_values_len;
    }
    if (!ddog__charslice_dup(label_name, &rule->label_name) || !ddog__charslice_dup(label_value, &rule->label_value))
    {
        free_upscaling_rule(rule);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy upscaling rule label");
    }

    profile->upscaling_rules[profile->upscaling_rules_len] = *rule;
    profile->upscaling_rules_len += 1;
    return DDOG_OK;
}

ddog_error_code ddog_prof_profile_add_upscaling_rule_proportional(ddog_prof_profile* profile,
                                                                    const size_t* offset_values, size_t offset_values_len,
                                                                    ddog_charslice label_name, ddog_charslice label_value,
                                                                    uint64_t total_sampled, uint64_t total_real)
{
    if (profile == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "profile is NULL");
    }
    if (offset_values == NULL && offset_values_len > 0)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "offset_values is NULL but offset_values_len > 0");
    }
    if (total_sampled == 0)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "total_sampled must not be 0");
    }
    if (total_real == 0)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "total_real must not be 0");
    }

    ddog_upscaling_rule_owned rule;
    memset(&rule, 0, sizeof(rule));
    rule.kind = DDOG_UPSCALING_PROPORTIONAL;
    rule.scale = (double)total_real / (double)total_sampled;
    return add_upscaling_rule(profile, offset_values, offset_values_len, label_name, label_value, &rule);
}

ddog_error_code ddog_prof_profile_add_upscaling_rule_poisson(ddog_prof_profile* profile,
                                                               const size_t* offset_values, size_t offset_values_len,
                                                               ddog_charslice label_name, ddog_charslice label_value,
                                                               size_t sum_value_offset, size_t count_value_offset,
                                                               uint64_t sampling_distance)
{
    if (profile == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "profile is NULL");
    }
    if (offset_values == NULL && offset_values_len > 0)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "offset_values is NULL but offset_values_len > 0");
    }
    if (sampling_distance == 0)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "sampling_distance must not be 0");
    }

    ddog_upscaling_rule_owned rule;
    memset(&rule, 0, sizeof(rule));
    rule.kind = DDOG_UPSCALING_POISSON;
    rule.sum_value_offset = sum_value_offset;
    rule.count_value_offset = count_value_offset;
    rule.sampling_distance = sampling_distance;
    return add_upscaling_rule(profile, offset_values, offset_values_len, label_name, label_value, &rule);
}

ddog_error_code ddog_prof_profile_serialize(ddog_prof_profile* profile,
                                             const ddog_timespec* start_time, const ddog_timespec* end_time,
                                             ddog_prof_encoded_profile** out_encoded)
{
    if (profile == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "profile is NULL");
    }
    if (out_encoded == NULL)
    {
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "out_encoded is NULL");
    }

    ddog_timespec effective_start = start_time != NULL ? *start_time : profile->start_time;
    ddog_timespec effective_end = end_time != NULL ? *end_time : ddog__now();

    ddog__buf raw;
    ddog__buf_init(&raw);
    if (!ddog__pprof_encode(profile, &effective_start, &effective_end, &raw))
    {
        ddog__buf_free(&raw);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to encode profile to pprof");
    }

    ddog__buf compressed;
    ddog__buf_init(&compressed);
    bool compress_ok = ddog__zstd_compress(raw.data, raw.len, &compressed);
    ddog__buf_free(&raw);
    if (!compress_ok)
    {
        ddog__buf_free(&compressed);
        return ddog__fail(DDOG_ERR_IO, "failed to zstd-compress the encoded profile");
    }

    struct ddog_prof_encoded_profile* encoded =
        (struct ddog_prof_encoded_profile*)malloc(sizeof(struct ddog_prof_encoded_profile));
    if (encoded == NULL)
    {
        ddog__buf_free(&compressed);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to allocate encoded profile");
    }
    encoded->data = compressed.data; // ownership transferred from `compressed`
    encoded->len = compressed.len;
    encoded->start_time = effective_start;
    encoded->end_time = effective_end;
    // endpoint counts move to the encoded profile, for the exporter
    encoded->endpoint_counts = profile->endpoint_counts;
    encoded->endpoint_counts_len = profile->endpoint_counts_len;
    profile->endpoint_counts = NULL;
    profile->endpoint_counts_len = 0;
    profile->endpoint_counts_capacity = 0;

    // Reset for the next collection interval - matches real libdatadog's
    // "reset_and_return_previous", which swaps in a brand new profile with
    // the same sample types and period: samples, endpoint mappings and counts,
    // and upscaling rules are all gone afterwards. The capacity of the sample
    // arrays and interner tables is kept so they can be reused without churn.
    profile->timestamped.len = 0;
    profile->aggregated.len = 0;
    if (!clear_interners(profile))
    {
        ddog_prof_encoded_profile_drop((ddog_prof_encoded_profile*)encoded);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to reset the profile");
    }
    for (size_t i = 0; i < profile->endpoint_mappings_len; i++)
    {
        ddog__charslice_free(&profile->endpoint_mappings[i].endpoint);
    }
    profile->endpoint_mappings_len = 0;
    reset_upscaling_rules(profile);
    profile->start_time = ddog__now();

    *out_encoded = (ddog_prof_encoded_profile*)encoded;
    return DDOG_OK;
}
