// Just enough JSON to build the multipart upload's "event" part. The shape
// of that object is fixed and small (see plan doc "Exporter (libcurl)"), so
// exporter.c assembles it by hand with literal C strings for keys/punctuation
// - the only real hazard is a tag value containing a `"` or `\`, so this file
// provides exactly one primitive: a proper escaped-string writer.
#pragma once

#include "internal.h"

// Appends `value`, JSON-escaped and double-quoted, to `buf`.
// Returns false on allocation failure.
bool ddog__json_append_escaped_string(ddog__buf* buf, ddog_charslice value);
