// Hand-rolled pprof (Google's standard protobuf profile schema) encoder:
// only a varint + tag writer, no general protobuf library. Walks the
// profile's interned tables (see profile.h).
#pragma once

#include "internal.h"
#include "profile.h"

// Appends the raw (uncompressed) pprof-encoded bytes for the profile's
// currently-accumulated samples to `out`. start_time/end_time become the
// encoded Profile's time_nanos/duration_nanos fields. Strings the encoder
// adds (sample types, "trace endpoint", endpoint names...) are interned into
// the profile. Returns false on allocation failure.
bool ddog__pprof_encode(struct ddog_prof_profile* profile, const ddog_timespec* start_time,
                        const ddog_timespec* end_time, ddog__buf* out);
