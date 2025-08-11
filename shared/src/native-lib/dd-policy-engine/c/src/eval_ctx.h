#pragma once

#include <dd/policies/action.h>
#include <dd/policies/error_codes.h>
#include <dd/policies/eval_ctx.h>
#include "wire/evaluator_types.h"

/**
 * @brief String evaluator entry structure.
 */
typedef struct string_evaluator_entry {
  /**< Function pointer to the string evaluator function */
  plcs_string_evaluator_function_ptr function_ptr;
  /**< The value to evaluate against, can be NULL if not set */
  const char *value;
  /**< Error code if the evaluator is not registered or */
  /**< fails !NEEDS TO BE RESET EVERY EVALUATION! */
  plcs_errors error;
} string_evaluator_entry;

typedef struct numeric_evaluator_entry {
  /**< Function pointer to the numeric evaluator function */
  plcs_numeric_evaluator_function_ptr function_ptr;
  /**< The value to evaluate against, can be NUM_NOT_SET if not set */
  long value;
  /**< Error code if the evaluator is not registered or */
  /**< fails !NEEDS TO BE RESET EVERY EVALUATION! */
  plcs_errors error;
} numeric_evaluator_entry;

typedef struct unumeric_evaluator_entry {
  /**< Function pointer to the unumeric evaluator function */
  plcs_unumeric_evaluator_function_ptr function_ptr;
  /**< The value to evaluate against, can be UNUM_NOT_SET if not set */
  unsigned long value;
  /**< Error code if the evaluator is not registered or */
  /**< fails !NEEDS TO BE RESET EVERY EVALUATION! */
  plcs_errors error;
} unumeric_evaluator_entry;

typedef struct action_entry {
  /**< Function pointer to the action function */
  plcs_action_function_ptr function_ptr;
  /**< Error code if the action is not registered or fails !NEEDS */
  plcs_errors error;
} action_entry;

/**
 * @brief The eval_ctx structure represents the evaluators and actions available
 * in the system. It includes:
 * - String evaluators for evaluating string-based policies.
 * - Numeric evaluators for evaluating numeric policies.
 * - Unsigned numeric evaluators for evaluating unsigned numeric policies.
 * - Action function pointers for executing actions based on evaluation results.
 *
 * The eval_ctx is initialized with default evaluators and actions,
 * for debugging purposes you can register custom evaluators and actions.
 * The model is designed to be flexible and extensible, allowing for easy
 * addition of new evaluators and actions.
 *
 * @note The eval_ctx is a mandatory component for policy evaluation.
 * It is used to store the current state of the system and to evaluate policies
 * based on that state. The model is updated as new parameters become available,
 * allowing for dynamic evaluation of policies.
 *
 * For Strings:
 * - Evaluators are registered with a function pointer and an index representing
 * the evaluator type.
 * - Parameters are set using the index and the value to evaluate against.
 * - Parameters containing `STR_NOT_SET` (`NULL`) values are considered as *not
 * set*.
 *
 * For Numerics:
 * - Evaluators are registered with a function pointer and an index representing
 * the evaluator type.
 * - Parameters are set using the index and the numeric value to evaluate
 * against.
 * - Parameters containing `NUM_NOT_SET` (`LONG_MAX`) are considered as *not
 * set*.
 *
 * For Unsigned Numerics:
 * - Evaluators are registered with a function pointer and an index representing
 * the evaluator type.
 * - Parameters are set using the index and the unsigned numeric value to
 * evaluate against.
 * - Parameters containing `UNUM_NOT_SET` (`ULONG_MAX`) are considered as *not
 * set*.
 *
 */
typedef struct plcs_eval_ctx {
  /**< EVALUATORS */
  /**< (a simple map evaluator id (enum):func_ptr) */
  string_evaluator_entry string_evaluators[STR_EVAL__COUNT];

  /**< (a simple map evaluator id (enum):func_ptr) */
  numeric_evaluator_entry numeric_evaluators[NUM_EVAL__COUNT];

  /**< (a simple map evaluator id (enum):func_ptr) */
  unumeric_evaluator_entry unumeric_evaluators[NUM_EVAL__COUNT];

  /**< (a simple map action id (enum):func_ptr) */
  action_entry actions[ACTIONS__COUNT];

  /**< TODO: consider implementing this as a stack to preserve history of errors */
  plcs_errors error;

} plcs_eval_ctx;

/**
 * @brief Used to set an error in the evaluation context
 * @param error plcs_errors enum
 */
void plcs_eval_ctx_set_error(plcs_errors error);

/**
 * @brief Sets an error code for an action
 * @param ix An action ID from plcs_actions enum
 * @param error plcs_errors enum
 */
void plcs_eval_ctx_set_action_error(plcs_actions ix, plcs_errors error);

/**
 * @brief Sets an error code for a string evaluator
 * @param ix A plcs_string_evaluators enum ID
 * @param error plcs_errors enum
 */
void plcs_eval_ctx_set_str_eval_error(plcs_string_evaluators ix, plcs_errors error);

/**
 * @brief Sets an error code for a numeric evaluator
 * @param id A plcs_numeric_evaluators enum ID
 * @param error plcs_errors enum
 */
void plcs_eval_ctx_set_num_eval_error(plcs_numeric_evaluators id, plcs_errors error);

/**
 * @brief Sets an error code for an unsigned numeric evaluator
 * @param ix A plcs_numeric_evaluators enum ID
 * @param error plcs_errors enum
 */
void plcs_eval_ctx_set_unum_eval_error(plcs_numeric_evaluators ix, plcs_errors error);
