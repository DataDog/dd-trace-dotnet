#pragma once

#include <actions_reader.h>

#include "evaluation_result.h"
#include "dd_types.h"
#include "error_codes.h"

#define ACTION_EVAL(name) TRANSLATE_ENUM(name, dd_ns(ActionId))

/**
 * The maximum number of values allowed in an action
 */
#define ACTION_VALUES_MAX dd_ns(ActionMax_ACTION_VALUES_MAX)

/**
 * @brief A mapping between flatbuffers defined enums and a local
 * representation.
 *
 */
typedef enum Actions {
  ACTION_EVAL(INJECT_DENY),
  ACTION_EVAL(INJECT_ALLOW),
  ACTION_EVAL(ENABLE_SDK),
  ACTION_EVAL(ENABLE_PROFILER),
  ACTION_EVAL(SET_ENVAR),
  ACTION_EVAL(ACTIONS_COUNT)
} Actions;

/**
 * @brief represents an action function signature
 *
 */
typedef policies_errors (*action_function_ptr)(
    EvaluationResult res,
    char *values[],
    size_t value_len,
    const char *description,
    int action_id
);
