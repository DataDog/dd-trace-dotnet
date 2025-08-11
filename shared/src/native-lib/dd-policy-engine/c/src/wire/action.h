/**
 * @file wire/actions.h
 * @brief Internal translation for plcs_actions enum and related constants.
 *
 * @details
 * This header maps our public-facing `plcs_actions` enum in
 *   `../../include/actions.h`
 * to the on-the-wire / generated enum values from the FlatBuffers schema
 * (via `actions_reader.h`).
 *
 * It provides:
 *   - Static inline helpers to translate public enum values to vendor/FlatBuffers values
 *   - Static inline helpers to translate vendor/FlatBuffers values back to public values
 *   - Compile-time checks to ensure mappings remain in sync
 *   - Access to the vendor-defined `ACTION_VALUES_MAX` constant
 *
 * @note
 * This is **private** library glue — it is not installed, not exported,
 * and must not be included in any public-facing headers.
 * Outside the library, include the public header from `../../include/`
 * and use only the public enum names and constants.
 *
 * Guidelines:
 *   - Keep mapping aligned with both the public enum and vendor schema
 *   - Use `_Static_assert` and `-Wswitch-enum` to catch drift early
 *   - No clever macros in public headers
 */

#pragma once

#include <actions_reader.h>          /* FlatBuffers generated headers */
#include <dd/policies/action.h>      /* public-facing enum and constants */
#include <dd/policies/error_codes.h> /* public-facing enum and constants */
#include "dd_types.h"
#include "evaluation_result.h"

#ifdef __cplusplus
extern "C" {
#endif

/** Maximum number of values allowed in an action (from vendor schema). */
#define ACTION_VALUES_MAX dd_ns(ActionMax_ACTION_VALUES_MAX)

static inline dd_ns(ActionId_enum_t) dd_action_to_wire(enum plcs_actions v) {
  static const int map[ACTIONS__COUNT] = {
      [INJECT_DENY] = dd_ns(ActionId_INJECT_DENY), [INJECT_ALLOW] = dd_ns(ActionId_INJECT_ALLOW),
      [ENABLE_SDK] = dd_ns(ActionId_ENABLE_SDK),   [ENABLE_PROFILER] = dd_ns(ActionId_ENABLE_PROFILER),
      [SET_ENVAR] = dd_ns(ActionId_SET_ENVAR), [REEXEC] = dd_ns(ActionId_REEXEC)
  };
  _Static_assert(
      ACTIONS__COUNT == dd_ns(ActionId_ACTIONS_COUNT),
      "update dd_action_to_wire & plcs_actions mappings when ActionId enum changes"
  );
  return (dd_ns(ActionId_enum_t))((unsigned)v < ACTIONS__COUNT ? map[v] : -1);
}

/**
 * @brief Represents an action function signature.
 *
 * @param res        The evaluation result determining action behavior.
 * @param values     Array of string values passed to the action.
 * @param value_len  Length of the `values` array.
 * @param description Optional description of the action.
 * @param action_id  Integer ID of the action.
 *
 * @return A `plcs_errors` status code.
 */
typedef plcs_errors (*plcs_action_function_ptr)(
    plcs_evaluation_result res,
    char *values[],
    size_t value_len,
    const char *description,
    int action_id
);

#ifdef __cplusplus
}
#endif
