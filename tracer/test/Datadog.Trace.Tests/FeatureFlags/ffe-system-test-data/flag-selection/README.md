# Flag Selection Vectors (`flag-selection/`)

`selection-vectors.json` defines how a flag configuration endpoint narrows its
response to the flags an SDK selected, and how an SDK canonicalizes the
selection it sends. Every server-side implementation and every SDK that supports
flag selection runs these cases, so they agree on matching, normalization,
limits, and the reason reported when a selection is ignored.

Each case carries two kinds of expectation:

- `expected`: what the server does with the selection in the request.
- `sdk` (optional): the canonical selection an SDK sends for the same input, or
  the reason the SDK drops it.

Two request forms carry the same selection:

- GET rules-based configuration endpoints read the `selected_flag_tags` and
  `selected_flag_key_patterns` query parameters. Each parameter may repeat.
- `POST /precompute-assignments` reads a `flag_selection` body field shaped as
  `{"tags": [...], "key_patterns": [...]}`.

The limits come from the UFC Tag Filtering RFC.

## Format

```json
{
  "flags": [{ "key": "checkout.new-flow", "tags": ["team:payments"] }],
  "cases": [
    {
      "name": "human-readable description",
      "query": { "selected_flag_tags": [" team:payments ", "checkout"] },
      "expected": { "outcome": "applied", "selectedKeys": ["checkout.new-flow"] },
      "sdk": { "selection": { "tags": ["checkout", "team:payments"], "keyPatterns": [] } }
    },
    {
      "name": "tag of 201 bytes is invalid",
      "query": { "selected_flag_tags": ["aaa…a"] },
      "expected": { "outcome": "ignored", "reason": "invalid_tag" },
      "sdk": { "dropped": "invalid_tag" }
    },
    {
      "name": "undecodable value is malformed",
      "rawQuery": "selected_flag_tags=%zz",
      "expected": { "outcome": "ignored", "reason": "malformed_selection" }
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `flags` | array | The flag set every case runs against: `key` (string) and `tags` (array of strings) |
| `cases[].name` | string | Unique, human-readable case name |
| `cases[].query` | object? | Decoded values per parameter, in request order, before trimming. Keys are `selected_flag_tags` and `selected_flag_key_patterns` |
| `cases[].rawQuery` | string? | An undecoded URL query string, for cases about percent-decoding and repeated parameters |
| `cases[].expected.outcome` | string | `applied` or `ignored` |
| `cases[].expected.reason` | string | Present only when `ignored`: `malformed_selection`, `too_many_values`, `invalid_key_pattern`, or `invalid_tag` |
| `cases[].expected.selectedKeys` | array | Present only when `applied`: the selected flag keys, sorted by byte order |
| `cases[].sdk` | object? | Optional SDK expectation. Present on every `query` case and absent on `rawQuery` cases. Holds exactly one of `selection` or `dropped` |
| `cases[].sdk.selection` | object | The canonical selection the SDK sends: `tags` and `keyPatterns`, each an array of strings sorted by UTF-8 bytes. Both empty means the SDK sends no selection |
| `cases[].sdk.dropped` | string | The reason the SDK drops the selection. Equal to `expected.reason` |

Each case has exactly one of `query` or `rawQuery`.

`sdk` is optional so that a reader written before it existed still runs every
case against `expected` and ignores `sdk`. An SDK reader runs `sdk`; a server
reader may ignore it.

## Semantics

Normalization, applied to each list independently:

- Values are trimmed of leading and trailing characters with the Unicode
  `White_Space` property. Other characters, such as U+FEFF and U+200B, are kept.
- Empty values are dropped, then duplicates are removed. Request order does not
  matter.
- The canonical lists are sorted by UTF-8 bytes. This differs from UTF-16 code
  unit order for characters outside the Basic Multilingual Plane.

Limits, checked after normalization:

- Each list holds at most 31 values. One list over the limit ignores the whole
  selection, even when the other list is within it.
- A key pattern is at most 200 bytes, measured in UTF-8 after trimming.
- A tag is at most 200 bytes, measured in UTF-8 after trimming. One tag over the
  limit ignores the whole selection, even when other tags match.

Matching:

- Tags match exactly and case-sensitively. A flag matches when it has any of the
  selected tags. Tags are never globs.
- Key patterns match the whole key, case-sensitively. `*` matches any run of
  characters, including none. Every other character is literal, including `?`,
  `.`, `[`, `]`, `{`, `}`, and `\`.
- A flag is selected when it matches any selected tag or any selected key
  pattern. When both lists are empty after normalization, every flag is
  selected.
- A selection that matches no flag is applied and returns no flags. It is not
  an error.

## Ignored Reasons

When a selection is ignored, the response contains every flag and the server
sets the `DD-Flag-Selection-Ignored` response header to the reason. When more
than one reason applies, the first in this table wins:

| Precedence | Reason | When | In vectors |
|------------|--------|------|------------|
| 1 | `malformed_selection` | GET: a selection parameter value cannot be percent-decoded. POST: `flag_selection` is not an object, has a field other than `tags` or `key_patterns`, holds a list that is not an array of strings, or appears more than once | GET only |
| 2 | `too_many_values` | A list holds more than 31 values after normalization | Yes |
| 3 | `invalid_key_pattern` | A key pattern is longer than 200 bytes after trimming | Yes |
| 4 | `invalid_tag` | A tag is longer than 200 bytes after trimming | Yes |
| 5 | `tag_load_failed` | The selection uses tags and the server cannot load the tags of the configured flags | No, server state |

A parameter that is not a selection parameter never causes `malformed_selection`,
even when it cannot be decoded. Parameter names are percent-decoded before they
are compared, so `selected%5Fflag%5Ftags` is a selection parameter. In the POST
body, JSON `null` for `flag_selection`, `tags`, or `key_patterns` means absent.

## Running the Vectors

Load `selection-vectors.json`, build the flag set from `flags`, and run each
case through the same parsing, validation, and matching code the endpoint uses.
For an `applied` case, compare the sorted selected keys with `selectedKeys`. For
an `ignored` case, compare the reason with `reason`.

### GET query form

- `query` case: either hand the decoded lists to the normalization step, or
  build a query string that repeats each parameter once per value, in the listed
  order, percent-encoding each value.
- `rawQuery` case: parse the string as the raw query of the request URL.

### POST body form

- `query` case: build the body field from the two parameters. A parameter
  missing from `query` becomes `null`. Omit `flag_selection` when `query` is
  empty.

  ```json
  {
    "flag_selection": {
      "tags": "<query.selected_flag_tags, or null>",
      "key_patterns": "<query.selected_flag_key_patterns, or null>"
    }
  }
  ```

- `rawQuery` case: skip it. JSON bodies have no percent-decoding.

### SDK form

For each case with `sdk`, pass the SDK's flag selection option
`{tags: query.selected_flag_tags, keyPatterns: query.selected_flag_key_patterns}`
to the SDK's canonicalization step. A parameter missing from `query` is an
omitted list. Then:

- `sdk.selection`: compare the canonical lists the SDK would send, in order.
  When both are empty, the SDK sends no selection parameters.
- `sdk.dropped`: the SDK drops the selection with this reason and sends none.

## Consumers

Each consumer pins a merged commit of this repository. A change to these
vectors merges here first; each consumer then moves its pin to the merged commit
in its own change.

## Validation

```bash
python3 ci/validate-flag-selection.py
```

Checks: valid JSON without duplicate keys, the fields above, exactly one input
per case, unique case names, known reasons, and `selectedKeys` that are sorted,
distinct, and present in `flags`. For `sdk`, it checks that every `query` case
has one and no `rawQuery` case does, that `sdk.selection` lists hold non-empty
strings sorted by UTF-8 bytes without duplicates, that `sdk.selection` appears
only on `applied` cases, and that `sdk.dropped` equals `expected.reason`. It
does not recompute expected outcomes.
