# Policies Library

A small, embeddable policy system for making runtime decisions via a compact, language-agnostic wire format.

- Policy data is encoded with [FlatBuffers](https://flatbuffers.dev/).
- There’s a C runtime that evaluates policies against a host-provided context and executes registered actions.
- Go helpers generate policy buffers (and can emit a C header containing an embedded policy buffer for static inclusion).

The library is intended to be used by auto_inject and other projects as a standalone component.

---

## Features

- Compact, versionable policy representation (FlatBuffers schema in fbs-schema/)
- Tri-state boolean evaluation with short-circuiting (TRUE, FALSE, ABSTAIN)
- Composable rules via AND, OR, NOT over a tree of evaluators
- String, signed numeric, and unsigned numeric evaluators
- Pluggable action handlers (execute after rule evaluation)
- C library (libpolicies.a) with clean public headers
- Go tools and examples to build policy buffers and embed them in C

---

## Repository layout

- `c/`
  - `include/policies/*.h`: Public C API (actions, eval context, evaluators, error codes, etc.)
  - `src/`: Evaluator engine, context, "wire" mappings, and FlatBuffers integrations (C/flatcc)
  - `examples/`: Minimal programs showing how to register evaluators/actions and evaluate policy buffers
  - `Makefile`: Builds the static library and examples
- `go/`
  - `schema/`: Go code generated from the FlatBuffers schema
  - `example_writer/`: Programmatic examples for building .bin policy buffers
  - `example_generate_c_header_buffer/`: Emits a C header with an embedded policy buffer (for static inclusion)
  - `example_json_to_hardcoded_injector_policies/`: Converts a JSON policy description into a FlatBuffer (and a C header)
  - `Makefile`: Regenerates Go schema and runs examples
- `fbs-schema/`: FlatBuffers schema definitions (actions, evaluators, nodes, policies)
- `Makefile`: Multi-language orchestrator for C and Go

---

## Prerequisites

You’ll need the FlatBuffers toolchains and compilers:

- C toolchain (clang/gcc) and ar
- flatcc (for generating C readers): https://github.com/dvidelabs/flatcc
- flatc (for generating Go schema): https://github.com/google/flatbuffers
- Go (per go.mod, Go 1.23.x)
- Optional: clang-format for formatting (used by `make fmt`)

---

## Building

From the policies/ directory:

- Build both C and Go parts:
  - `make all`
- Build C library only:
  - `make -C c all`
- Build Go examples (regenerates Go schema):
  - `make -C go examples`
- Build all examples (C + Go):
  - `make examples`
- Clean:
  - `make clean`

C build outputs:
- Static library: `c/lib/libpolicies.a`
- Policy debugger: `c/lib/basic-debugger`
- Generated C schema headers: `c/src/generated/` (via `flatcc`)

Go examples output:
- Binary buffers: under example-specific `out/`
- Optional C headers with embedded buffers (see below)

---

## Quick start (C)

High-level steps:
1) Initialize the evaluation context with `plcs_eval_ctx_init()`.
2) Optionally register custom evaluator functions with:
   - `plcs_eval_ctx_register_str_evaluator(func, plcs_string_evaluators id)`
   - `plcs_eval_ctx_register_num_evaluator(func, plcs_numeric_evaluators id)`
   - `plcs_eval_ctx_register_unum_evaluator(func, plcs_numeric_evaluators id)`
   If you do not register, built-in default evaluators are used.
3) Set context parameters your policy relies on:
   - `plcs_eval_ctx_set_str_eval_param(plcs_string_evaluators id, const char* value)`
   - `plcs_eval_ctx_set_num_eval_param(plcs_numeric_evaluators id, long value)`
   - `plcs_eval_ctx_set_unum_eval_param(plcs_numeric_evaluators id, unsigned long value)`
4) Register action handlers using:
   - `plcs_eval_ctx_register_action(plcs_action_function_ptr, plcs_actions id)`
   Action signature:
   - `plcs_errors (*plcs_action_function_ptr)(plcs_evaluation_result res, char* values[], size_t value_len, const char* description, int action_id)`
5) Load a policy buffer (from disk, mmap, or embedded array) and call:
   - `plcs_errors plcs_evaluate_buffer(const uint8_t* buffer)`

Examples to review under `c/examples/`:
- `basic-reader.c`: reads a binary flat buffer policy file into memory and parses it
- `mmap-reader.c`: mmaps a binary flat buffer policy file and parses it
- `hardcoded-header-reader.c`: uses an embedded C header buffer and parses it
- `hardcoded_cmdline_evaluator.c`: reads a binary flat buffer policy file or a hardcoded header buffer and parses it (multiple evalutaors are used)

Build all C examples:
- `make -C c examples`

Notes:
- If you do not register custom evaluator functions, the library falls back to built-in defaults:
  - String: `exact/prefix/suffix/contains`
  - Numeric (signed/unsigned): `==, >, >=, <, <=`
- You still must provide the context parameter values (`plcs_eval_ctx_set_*_eval_param`) for evaluators referenced by the policy.

---

## Generating policy buffers (Go)

Create policies programmatically and save them as FlatBuffers:

- Run all Go examples (also regenerates Go schema):
  - `make -C go examples`

Examples:
- `go/example_writer`: emits out/*.bin - these are binary flat buffer policy files that can be used with the C examples.
- `go/example_generate_c_header_buffer`: emits out/buffer.bin and out/buffer.h (C header with `const uint8_t hardcoded_policies[]`)
- `go/example_json_to_hardcoded_injector_policies`: converts a JSON file (skips.json by default) to out/hardcoded.h and hardcoded.bin

Include the generated header in your C app or examples (see c/examples/hardcoded-header-reader.h).

---

## Wire format and evaluation model

Schema lives in fbs-schema/:
- plcs_actions: action_ids.fbs, actions.fbs
- Evaluators: evaluator_ids.fbs, evaluators.fbs
- Nodes and composition: nodes.fbs, boolean_operation.fbs
- Policy container: policy.fbs (root type: Policies)

Policy structure (high level):
- Policies: vector of Policy
- Policy:
  - description: string
  - rules: NodeTypeWrapper (a tree of evaluator nodes and/or composite boolean nodes)
  - actions: [Action] (executed after evaluation)

Tri-state plcs_evaluation_result:
- TRUE, FALSE, ABSTAIN ("don’t care")

Boolean composition:
- AND: FALSE dominates; TRUE if all TRUE; ABSTAIN propagates if no decisive result
- OR: TRUE dominates; FALSE if all FALSE; ABSTAIN propagates if no decisive result
- NOT: inverts TRUE/FALSE, ABSTAIN remains ABSTAIN

Evaluators:
- String: StrEvaluator with id, cmp, value
- Numeric (signed): NumEvaluator with id, cmp, value: long
- Numeric (unsigned): UNumEvaluator with id, cmp, value: ulong

plcs_actions:
- Each Action has action: ActionId, description, values: [string]
- C action handler signature:
  - `plcs_errors (*plcs_action_function_ptr)(plcs_evaluation_result res, char* values[], size_t value_len, const char* description, int action_id)`

---

## C API overview

Key public headers (under c/include/policies):
- `eval_ctx.h`
  - Initialize/reset: `plcs_eval_ctx_init`, `plcs_eval_ctx_reset`
  - Register evals/actions: `plcs_eval_ctx_register_*`
  - Set parameters: `plcs_eval_ctx_set_*_eval_param`
  - Accessors: `plcs_eval_ctx_get_*` (primarily used by the engine and your custom evals)
  - Error tracking: `plcs_eval_ctx_get_last_error`, `plcs_eval_ctx_peek_last_error`, per-evaluator/action error setters
- `evaluator.h`
  - `plcs_evaluate_buffer(const uint8_t *buffer)` — evaluate all policies from a FlatBuffers buffer
- `evaluator_types.h`
  - Enums for comparator types and evaluator IDs
- `action.h`
  - `plcs_actions` enum, `plcs_action_function_ptr`
- `evaluation_result.h`
  - `plcs_evaluation_result` enum and `plcs_evaluation_result_to_string(...)`
- `error_codes.h`
  - `plcs_errors` enum (e.g., `DD_ESUCCESS`, `DD_EIX_OVERFLOW`, etc.)

---

## Common gotchas

- ABSTAIN is expected and not an error; it often means "insufficient context" and can be fine depending on boolean composition.
- Call `plcs_eval_ctx_init()` once per process before using the API; subsequent calls return `-DD_EINITIZLIED`.
- If you rely on default evaluators, remember to set the corresponding context parameters for any evaluators referenced by the policies.
- Action handlers should return `DD_ESUCCESS` unless they truly failed; `plcs_evaluate_buffer` sums error codes across policies.

---

## Extending the schema

When adding new enumerations in the FlatBuffers schema:
- Append new values just before the "count" sentinel (e.g., `*_COUNT` entries).
- Update the C "wire" translation tables under c/src/wire/ (compile-time asserts help detect drift).
- Regenerate FlatBuffers code for both C (flatcc) and Go (flatc), then rebuild.

---

## Development

- Format C code:
  - `make -C c fmt`
  - `make -C c fmt-check`
- Re-generate C readers:
  - Happens automatically when building the C library (`c/src/generated` from `fbs-schema/*.fbs`)
- Re-generate Go schema:
  - `make -C go generate-schema-headers`
  - or run: `make -C go examples`

---

## License

TBD.
