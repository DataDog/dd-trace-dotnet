// Standalone driver for M1 ("dump") and M2 ("send") verification - mirrors
// libdatadog's own examples/ffi/profiles.c. Not part of the public API,
// not installed anywhere; just a local dev tool.
#include "datadog_poc/common.h"
#include "datadog_poc/profiling.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static ddog_charslice cs(const char* s)
{
    ddog_charslice out;
    out.ptr = s;
    out.len = strlen(s);
    return out;
}

static ddog_prof_profile* build_sample_profile(void)
{
    ddog_prof_value_type sample_types[2];
    sample_types[0].type = cs("wall-time");
    sample_types[0].unit = cs("nanoseconds");
    sample_types[1].type = cs("cpu-time");
    sample_types[1].unit = cs("nanoseconds");

    ddog_prof_period period;
    period.type = sample_types[0];
    period.value = 60000000; // 60ms, arbitrary

    ddog_prof_profile* profile = NULL;
    ddog_error_code rc = ddog_prof_profile_new(sample_types, 2, &period, &profile);
    if (rc != DDOG_OK)
    {
        fprintf(stderr, "ddog_prof_profile_new failed: %s\n", ddog_last_error_message());
        return NULL;
    }

    for (int i = 0; i < 3; i++)
    {
        ddog_prof_location locations[2];
        memset(locations, 0, sizeof(locations));
        locations[0].function.name = cs("MyApp.DoWork");
        locations[0].function.filename = cs("/src/MyApp/Worker.cs");
        locations[0].mapping_filename = cs("MyApp.dll");
        locations[0].line = 42;
        locations[1].function.name = cs("MyApp.Main");
        locations[1].function.filename = cs("/src/MyApp/Program.cs");
        locations[1].mapping_filename = cs("MyApp.dll");
        locations[1].line = 10;

        int64_t values[2];
        values[0] = 1000000 * (i + 1);
        values[1] = 500000 * (i + 1);

        ddog_prof_label labels[1];
        memset(labels, 0, sizeof(labels));
        labels[0].key = cs("thread name");
        labels[0].str = cs("main");

        ddog_prof_sample sample;
        sample.locations = locations;
        sample.locations_len = 2;
        sample.values = values;
        sample.values_len = 2;
        sample.labels = labels;
        sample.labels_len = 1;

        rc = ddog_prof_profile_add(profile, &sample, 0);
        if (rc != DDOG_OK)
        {
            fprintf(stderr, "ddog_prof_profile_add failed: %s\n", ddog_last_error_message());
            ddog_prof_profile_drop(profile);
            return NULL;
        }
    }

    return profile;
}

static int cmd_dump(const char* output_path)
{
    ddog_prof_profile* profile = build_sample_profile();
    if (profile == NULL)
    {
        return 1;
    }

    ddog_prof_encoded_profile* encoded = NULL;
    ddog_error_code rc = ddog_prof_profile_serialize(profile, NULL, NULL, &encoded);
    if (rc != DDOG_OK)
    {
        fprintf(stderr, "ddog_prof_profile_serialize failed: %s\n", ddog_last_error_message());
        ddog_prof_profile_drop(profile);
        return 1;
    }

    const uint8_t* bytes = NULL;
    size_t len = 0;
    rc = ddog_prof_encoded_profile_bytes(encoded, &bytes, &len);
    if (rc != DDOG_OK)
    {
        fprintf(stderr, "ddog_prof_encoded_profile_bytes failed: %s\n", ddog_last_error_message());
        ddog_prof_encoded_profile_drop(encoded);
        ddog_prof_profile_drop(profile);
        return 1;
    }

    FILE* f = fopen(output_path, "wb");
    if (f == NULL)
    {
        fprintf(stderr, "failed to open %s for writing\n", output_path);
        ddog_prof_encoded_profile_drop(encoded);
        ddog_prof_profile_drop(profile);
        return 1;
    }
    fwrite(bytes, 1, len, f);
    fclose(f);

    printf("wrote %zu zstd-compressed pprof bytes to %s\n", len, output_path);

    ddog_prof_encoded_profile_drop(encoded);
    ddog_prof_profile_drop(profile);
    return 0;
}

static int cmd_send(const char* agent_url)
{
    ddog_prof_profile* profile = build_sample_profile();
    if (profile == NULL)
    {
        return 1;
    }

    ddog_prof_encoded_profile* encoded = NULL;
    ddog_error_code rc = ddog_prof_profile_serialize(profile, NULL, NULL, &encoded);
    if (rc != DDOG_OK)
    {
        fprintf(stderr, "ddog_prof_profile_serialize failed: %s\n", ddog_last_error_message());
        ddog_prof_profile_drop(profile);
        return 1;
    }

    ddog_prof_endpoint endpoint;
    rc = ddog_prof_endpoint_agent(cs(agent_url), 3000, false, &endpoint);
    if (rc != DDOG_OK)
    {
        fprintf(stderr, "ddog_prof_endpoint_agent failed: %s\n", ddog_last_error_message());
        ddog_prof_encoded_profile_drop(encoded);
        ddog_prof_profile_drop(profile);
        return 1;
    }

    ddog_vec_tag tags;
    ddog_vec_tag_new(&tags);
    ddog_vec_tag_push(&tags, cs("language"), cs("dotnet"));

    ddog_prof_exporter* exporter = NULL;
    rc = ddog_prof_exporter_new(cs("datadog-profiling-poc"), cs("0.1.0"), cs("dotnet"), &tags, &endpoint, &exporter);
    ddog_vec_tag_drop(&tags);
    if (rc != DDOG_OK)
    {
        fprintf(stderr, "ddog_prof_exporter_new failed: %s\n", ddog_last_error_message());
        ddog_prof_encoded_profile_drop(encoded);
        ddog_prof_profile_drop(profile);
        return 1;
    }

    uint16_t http_status = 0;
    rc = ddog_prof_exporter_send_blocking(exporter, encoded, NULL, 0, NULL, NULL, NULL, NULL, &http_status);
    if (rc != DDOG_OK)
    {
        fprintf(stderr, "ddog_prof_exporter_send_blocking failed: %s\n", ddog_last_error_message());
    }
    else
    {
        printf("upload succeeded, HTTP status %u\n", (unsigned)http_status);
    }

    ddog_prof_exporter_drop(exporter);
    ddog_prof_encoded_profile_drop(encoded);
    ddog_prof_profile_drop(profile);
    return rc == DDOG_OK ? 0 : 1;
}

int main(int argc, char** argv)
{
    if (argc == 3 && strcmp(argv[1], "dump") == 0)
    {
        return cmd_dump(argv[2]);
    }
    if (argc == 3 && strcmp(argv[1], "send") == 0)
    {
        return cmd_send(argv[2]);
    }

    fprintf(stderr, "usage: %s dump <output-file>\n       %s send <agent-base-url>\n", argv[0], argv[0]);
    return 2;
}
