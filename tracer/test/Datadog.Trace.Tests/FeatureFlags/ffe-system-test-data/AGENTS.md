# AGENTS.md — Writing FLEX Precomputed Assignment Fixtures

This guide is for AI agents and contributors writing precomputed assignment
test fixtures for the Datadog Feature Flag Evaluation (FFE) system test data
repository.

## Repository layout

```
ffe-system-test-data/
├── precomputed-assignments/
│   ├── README.md                              # Full format reference
│   └── cases/
│       ├── evaluator/                         # Core value resolution
│       ├── emissions/                         # Exposure & evaluation events
│       └── parsing/                           # Malformed data handling
├── schemas/
│   └── precomputed-assignment.schema.json     # JSON Schema definition
├── ci/
│   └── validate-precomputed-fixtures.py       # Schema validator
├── evaluation-cases/                          # Server-side UFC fixtures (separate format)
└── ufc-config.json                            # Flag definitions for evaluation-cases
```

## Choosing a category

| Category | When to use |
|----------|-------------|
| `evaluator/` | Core assignment behavior: type resolution, defaults, errors, flag-not-found, type mismatch, falsy values, data fidelity |
| `emissions/` | Exposure event dedup, doLog gates, evaluation event counts, aggregation behavior |
| `parsing/` | Malformed response data, null fields, flag isolation |

## Fixture schema

Each fixture file is a single JSON object. Schema: `schemas/precomputed-assignment.schema.json`.

```json
{
  "name": "my-fixture",
  "description": "What this fixture tests.",
  "context": {
    "targetingKey": "user-id",
    "attributes": {}
  },
  "response": {
    "data": {
      "attributes": {
        "flags": {
          "flag-key": {
            "allocationKey": "allocation-a",
            "variationKey": "variation-a",
            "variationType": "boolean",
            "variationValue": true,
            "reason": "TARGETING_MATCH",
            "doLog": true
          }
        }
      }
    }
  },
  "evaluations": [
    {
      "flag": "flag-key",
      "variationType": "boolean",
      "defaultValue": false,
      "expectedResult": {
        "value": true,
        "reason": "TARGETING_MATCH",
        "errorCode": null,
        "variantKey": "variation-a"
      }
    }
  ],
  "expectations": {
    "noUnmatchedEvents": true,
    "exposures": [
      {"_count": 1}
    ],
    "evaluationEvents": [
      {"_count": 1}
    ]
  }
}
```

## Field reference

### Top-level

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | yes | Must match filename stem |
| `description` | string | yes | What the fixture tests |
| `context` | object | yes | `targetingKey` (string or null) and `attributes` (object) |
| `response` | object | yes | Mocked precomputed-assignments response (open-ended) |
| `evaluations` | array | yes | Typed flag evaluations to run |
| `expectations` | object | yes | Event matchers |
| `_skip` | array | no | Skip fixture for listed platforms |
| `_include` | array | no | Run fixture only for listed platforms |

### Evaluation fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `flag` | string | yes | Flag key to evaluate |
| `variationType` | string | yes | `boolean`, `string`, `integer`, `float`, or `object` |
| `defaultValue` | any | yes | Default value passed to the getter |
| `expectedResult` | object | yes | Expected outcome |
| `expectedResult.value` | any | yes | Expected flag value |
| `expectedResult.reason` | string? | no | Expected reason code (null if error) |
| `expectedResult.errorCode` | string? | no | Expected error code (null if success) |
| `expectedResult.variantKey` | string? | no | Expected variation key (null if error) |
| `_skip` | array | no | Skip this evaluation for listed platforms |
| `_include` | array | no | Run this evaluation only for listed platforms |

### Event matchers

Matchers in `expectations.exposures` and `expectations.evaluationEvents` are
ordered and exclusive. Each received event is consumed by the first matching
matcher.

| Control field | Description |
|---------------|-------------|
| `_count` | Number of events to consume (required, >= 1) |
| `_skip` | Skip matcher for listed platforms |
| `_include` | Apply matcher only for listed platforms |

All other keys in a matcher are match fields compared against event data.
A match field set to `null` asserts the field is either absent or null on the
actual event. A matcher with only `_count` matches any event.

### `_skip` and `_include`

Always `[{"platform": "<id>", "reason": "<optional>"}]`. Mutually exclusive.
Default (neither present): applies to all platforms.

Valid platforms: `web`, `ios`, `android`, `react-native`, `flutter`, `maui`,
`kotlin-multiplatform`, `electron`.

## Error codes

| Code | When |
|------|------|
| `FLAG_NOT_FOUND` | Flag key not in response, or flag dropped due to malformed data |
| `TYPE_MISMATCH` | Requested type does not match `variationType` |
| `PARSE_ERROR` | Enumerated field contains unexpected value |
| `TARGETING_KEY_MISSING` | Evaluation requires targeting key but none provided |

## Reason codes

| Code | Meaning |
|------|---------|
| `TARGETING_MATCH` | Matched an explicit rule condition |
| `SPLIT` | Assigned via shard range |
| `STATIC` | Single allocation at 100%, no rules |
| `DEFAULT` | No allocation matched |

## Emission rules

- **Exposures**: only when `doLog: true` AND evaluation succeeds (no error).
- **Evaluation events**: all evaluations, success and error alike.
- Use `noUnmatchedEvents: true` to assert exact event counts.

## Naming conventions

- Schema fields: **camelCase** (`expectedResult`, `variationType`)
- Wire-format match fields in matchers: **snake_case** (`evaluation_count`, `serial_id`)
- Filenames: **kebab-case** (`type-mismatch-all-types.json`)

## Validation

```bash
python3 ci/validate-precomputed-fixtures.py
```

Checks: JSON Schema conformance, `_skip`/`_include` mutual exclusivity,
`_count` >= 1, name matches filename.

## Common patterns

### Testing flag isolation

Interleave valid and malformed flags in the response. Assert valid flags
return correct values. Assert malformed flags return `defaultValue` with
`FLAG_NOT_FOUND`.

### Testing type mismatch

Put a valid flag in the response but evaluate with a different `variationType`.
Expect `defaultValue` with `TYPE_MISMATCH`.

### Platform-specific behavior

Use `_skip`/`_include` on individual evaluations or matchers. For different
expected event counts per platform, use separate matchers with `_skip`/`_include`
rather than platform overrides.

Example — web aggregates 2 evaluations into 1 event:
```json
"evaluationEvents": [
  {"_count": 2, "_skip": [{"platform": "web"}]},
  {"_count": 1, "_include": [{"platform": "web"}], "evaluation_count": 2}
]
```
