#include "datadog_poc/profiling.h"
#include "encoded_profile.h"
#include "internal.h"
#include "pprof_encode.h"
#include "profile.h"
#include "zstd_compress.h"

#include <stdlib.h>
#include <string.h>
#include <time.h>

static ddog_timespec ddog__now(void)
{
    struct timespec ts;
    timespec_get(&ts, TIME_UTC);
    ddog_timespec out;
    out.seconds = (int64_t)ts.tv_sec;
    out.nanoseconds = (uint32_t)ts.tv_nsec;
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

static bool dup_function(const ddog_prof_function* in, ddog_prof_function* out)
{
    if (!ddog__charslice_dup(in->name, &out->name))
    {
        return false;
    }
    if (!ddog__charslice_dup(in->system_name, &out->system_name))
    {
        ddog__charslice_free(&out->name);
        return false;
    }
    if (!ddog__charslice_dup(in->filename, &out->filename))
    {
        ddog__charslice_free(&out->name);
        ddog__charslice_free(&out->system_name);
        return false;
    }
    return true;
}

static void free_function(ddog_prof_function* f)
{
    ddog__charslice_free(&f->name);
    ddog__charslice_free(&f->system_name);
    ddog__charslice_free(&f->filename);
}

static bool dup_location(const ddog_prof_location* in, ddog_prof_location_owned* out)
{
    if (!dup_function(&in->function, &out->function))
    {
        return false;
    }
    if (!ddog__charslice_dup(in->mapping_filename, &out->mapping_filename))
    {
        free_function(&out->function);
        return false;
    }
    out->address = in->address;
    out->line = in->line;
    return true;
}

static void free_location(ddog_prof_location_owned* loc)
{
    free_function(&loc->function);
    ddog__charslice_free(&loc->mapping_filename);
}

static bool dup_label(const ddog_prof_label* in, ddog_prof_label_owned* out)
{
    if (!ddog__charslice_dup(in->key, &out->key))
    {
        return false;
    }
    if (!ddog__charslice_dup(in->str, &out->str))
    {
        ddog__charslice_free(&out->key);
        return false;
    }
    if (!ddog__charslice_dup(in->num_unit, &out->num_unit))
    {
        ddog__charslice_free(&out->key);
        ddog__charslice_free(&out->str);
        return false;
    }
    out->num = in->num;
    return true;
}

static void free_label(ddog_prof_label_owned* label)
{
    ddog__charslice_free(&label->key);
    ddog__charslice_free(&label->str);
    ddog__charslice_free(&label->num_unit);
}

static void free_sample(ddog_prof_sample_owned* sample)
{
    for (size_t i = 0; i < sample->locations_len; i++)
    {
        free_location(&sample->locations[i]);
    }
    free(sample->locations);
    free(sample->values);
    for (size_t i = 0; i < sample->labels_len; i++)
    {
        free_label(&sample->labels[i]);
    }
    free(sample->labels);
    memset(sample, 0, sizeof(*sample));
}

static bool dup_sample(const ddog_prof_sample* in, int64_t timestamp, ddog_prof_sample_owned* out)
{
    memset(out, 0, sizeof(*out));

    if (in->locations_len > 0)
    {
        out->locations = (ddog_prof_location_owned*)calloc(in->locations_len, sizeof(ddog_prof_location_owned));
        if (out->locations == NULL)
        {
            return false;
        }
        for (size_t i = 0; i < in->locations_len; i++)
        {
            if (!dup_location(&in->locations[i], &out->locations[i]))
            {
                out->locations_len = i;
                free_sample(out);
                return false;
            }
        }
        out->locations_len = in->locations_len;
    }

    if (in->values_len > 0)
    {
        out->values = (int64_t*)malloc(in->values_len * sizeof(int64_t));
        if (out->values == NULL)
        {
            free_sample(out);
            return false;
        }
        memcpy(out->values, in->values, in->values_len * sizeof(int64_t));
        out->values_len = in->values_len;
    }

    if (in->labels_len > 0)
    {
        out->labels = (ddog_prof_label_owned*)calloc(in->labels_len, sizeof(ddog_prof_label_owned));
        if (out->labels == NULL)
        {
            free_sample(out);
            return false;
        }
        for (size_t i = 0; i < in->labels_len; i++)
        {
            if (!dup_label(&in->labels[i], &out->labels[i]))
            {
                out->labels_len = i;
                free_sample(out);
                return false;
            }
        }
        out->labels_len = in->labels_len;
    }

    out->timestamp = timestamp;
    return true;
}

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

    size_t filled = 0;
    for (size_t i = 0; i < sample_types_len; i++)
    {
        if (!dup_value_type(&sample_types[i], &profile->sample_types[i]))
        {
            for (size_t j = 0; j < filled; j++)
            {
                free_value_type(&profile->sample_types[j]);
            }
            free(profile->sample_types);
            free(profile);
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy sample_types[%zu]", i);
        }
        filled = i + 1;
    }
    profile->sample_types_len = sample_types_len;

    if (!dup_value_type(&period->type, &profile->period.type))
    {
        for (size_t j = 0; j < profile->sample_types_len; j++)
        {
            free_value_type(&profile->sample_types[j]);
        }
        free(profile->sample_types);
        free(profile);
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy period type");
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

    for (size_t i = 0; i < profile->samples_len; i++)
    {
        free_sample(&profile->samples[i]);
    }
    free(profile->samples);

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

    if (profile->samples_len == profile->samples_capacity)
    {
        size_t new_capacity = profile->samples_capacity == 0 ? 64 : profile->samples_capacity * 2;
        ddog_prof_sample_owned* new_ptr =
            (ddog_prof_sample_owned*)realloc(profile->samples, new_capacity * sizeof(ddog_prof_sample_owned));
        if (new_ptr == NULL)
        {
            return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to grow samples array to %zu entries", new_capacity);
        }
        profile->samples = new_ptr;
        profile->samples_capacity = new_capacity;
    }

    if (!dup_sample(sample, timestamp, &profile->samples[profile->samples_len]))
    {
        return ddog__fail(DDOG_ERR_OUT_OF_MEMORY, "failed to copy sample");
    }
    profile->samples_len += 1;
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

ddog_error_code ddog_prof_profile_add_upscaling_rule_proportional(ddog_prof_profile* profile,
                                                                    const size_t* offset_values, size_t offset_values_len,
                                                                    ddog_charslice label_name, ddog_charslice label_value,
                                                                    uint64_t total_sampled, uint64_t total_real)
{
    (void)label_name;
    (void)label_value;
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
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "total_sampled must be non-zero");
    }
    // Not yet applied to the encoded output - see profile.h.
    (void)total_real;
    return DDOG_OK;
}

ddog_error_code ddog_prof_profile_add_upscaling_rule_poisson(ddog_prof_profile* profile,
                                                               const size_t* offset_values, size_t offset_values_len,
                                                               ddog_charslice label_name, ddog_charslice label_value,
                                                               size_t sum_value_offset, size_t count_value_offset,
                                                               uint64_t sampling_distance)
{
    (void)label_name;
    (void)label_value;
    (void)sum_value_offset;
    (void)count_value_offset;
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
        return ddog__fail(DDOG_ERR_INVALID_ARGUMENT, "sampling_distance must be non-zero");
    }
    // Not yet applied to the encoded output - see profile.h.
    return DDOG_OK;
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

    // Reset for the next collection interval - matches real libdatadog's
    // "reset_and_return_previous" behavior, so a long-running process doesn't
    // just keep accumulating samples across every periodic upload. Capacity
    // is kept (not freed) so the arrays can be reused without churn.
    for (size_t i = 0; i < profile->samples_len; i++)
    {
        free_sample(&profile->samples[i]);
    }
    profile->samples_len = 0;
    for (size_t i = 0; i < profile->endpoint_counts_len; i++)
    {
        ddog__charslice_free(&profile->endpoint_counts[i].endpoint);
    }
    profile->endpoint_counts_len = 0;
    profile->start_time = ddog__now();

    *out_encoded = (ddog_prof_encoded_profile*)encoded;
    return DDOG_OK;
}
