# Precomputed Assignment Fixtures (`precomputed-assignments/`)

These fixtures cover the Datadog precompute assignment API consumed by mobile
and browser SDKs which do not run the evaluator locally.

## Directory structure

```
precomputed-assignments/
  cases/
    evaluator/     # Core value resolution, types, defaults, errors
    emissions/     # Exposure and evaluation event behavior
    parsing/       # Malformed data handling, isolation (future)
  README.md
```

Test runners should recursively discover `*.json` files under `cases/`.

## Fixture structure

Each fixture file is a single JSON object:

```json
{
  "name": "fixture-name",
  "description": "What this fixture tests.",
  "context": {
    "targetingKey": "user-id",
    "attributes": {}
  },
  "response": { ... },
  "evaluations": [ ... ],
  "expectations": { ... }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | yes | Fixture identifier; must match filename stem |
| `description` | string | yes | What the fixture tests |
| `context` | object | yes | `targetingKey` and `attributes` for the evaluation context |
| `response` | object | yes | Mocked precomputed-assignments response (open-ended) |
| `evaluations` | array | yes | Typed flag evaluations to run |
| `expectations` | object | yes | Event matchers (see below) |
| `_skip` | array | no | Skip entire fixture for listed platforms |
| `_include` | array | no | Run fixture only for listed platforms |

## Evaluations

Each evaluation calls a typed getter on the SDK and asserts the result:

```json
{
  "flag": "flag-key",
  "variationType": "boolean",
  "defaultValue": false,
  "expectedResult": {
    "value": true,
    "reason": "TARGETING_MATCH",
    "errorCode": null,
    "variantKey": "on"
  }
}
```

Evaluations may carry `_skip` or `_include` to control per-platform applicability.

## Expectations and event matchers

The `expectations` block uses ordered, exclusive matchers to assert on emitted
exposure and evaluation events.

```json
"expectations": {
  "noUnmatchedEvents": true,
  "exposures": [
    {"_count": 1, "flag": "my-flag"}
  ],
  "evaluationEvents": [
    {"_count": 2, "_skip": [{"platform": "web"}]},
    {"_count": 1, "_include": [{"platform": "web"}], "evaluation_count": 2}
  ]
}
```

### Matcher semantics

Matchers are processed **in order**. Each received event is consumed by the
first matcher whose non-`_` fields are a subset of the event. Once consumed,
the event is unavailable to later matchers.

- `_count` *(required)*: how many events this matcher should consume.
- All other non-`_` keys are match fields compared against event data.
- A match field set to `null` asserts the field is either absent or null on
  the actual event.
- A matcher with only `_count` (no match fields) matches any event.

### `noUnmatchedEvents`

When `true`, the test runner asserts that all received events (both exposures
and evaluations) are consumed by matchers. No extra events allowed. Applies to
both event types.

### Control fields

All fields prefixed with `_` are control fields, not match fields:

| Field | Description |
|-------|-------------|
| `_count` | Number of events to consume (required on matchers) |
| `_skip` | Skip this matcher/evaluation/fixture for listed platforms |
| `_include` | Apply this matcher/evaluation/fixture only for listed platforms |

Test runners in dynamic languages should filter keys starting with `_` to
separate control from match fields. Typed runners can use the known set.

## `_skip` and `_include`

Platform filters are always an array of `{platform, reason?}` objects,
regardless of where they appear (case, evaluation, or matcher level).

```json
"_skip": [
  {"platform": "web", "reason": "Browser SDK aggregates differently."}
]

"_include": [
  {"platform": "web"}
]
```

- **Default** (neither present): applies to all platforms.
- `_skip`: applies to all platforms except listed.
- `_include`: applies only to listed platforms.
- **Mutually exclusive**: specifying both is invalid.
- `reason` is optional.

Valid platform identifiers: `web`, `ios`, `android`, `react-native`, `flutter`,
`maui`, `kotlin-multiplatform`, `electron`.

## How to implement a test runner

1. Recursively discover `*.json` files under `precomputed-assignments/cases/`.
2. For each fixture, check `_skip`/`_include` at case level. Skip if the
   current platform is excluded.
3. Initialize the SDK with `context`. Feed `response` as the mocked server
   payload.
4. Walk `evaluations` in order. For each evaluation not skipped for the current
   platform, call the typed getter (e.g., `getBooleanValue`) with `flag`,
   `defaultValue`. Assert against `expectedResult`.
5. Flush the SDK.
6. Collect emitted exposure and evaluation events.
7. Walk `expectations.exposures` matchers in order. For each applicable matcher
   (not skipped for the current platform), greedily consume up to `_count`
   events whose fields are a superset of the matcher's non-`_` fields. Fail if
   fewer than `_count` matched.
8. Repeat step 7 for `expectations.evaluationEvents`.
9. If `noUnmatchedEvents` is `true`, fail if any unconsumed events remain.

## Field naming

- All schema field names use **camelCase** (`expectedResult`, `variationType`).
- Snake_case in matchers is only for wire-format fields that match emitted
  event schemas (e.g., `evaluation_count`, `serial_id`).
- Fixture filenames use **kebab-case**.

## Schema and validation

The fixture schema is defined in `schemas/precomputed-assignment.schema.json`.
Validate fixtures locally with:

```bash
python3 ci/validate-precomputed-fixtures.py
```
