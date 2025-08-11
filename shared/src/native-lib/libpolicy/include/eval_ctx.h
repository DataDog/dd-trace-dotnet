#pragma once

/**
 *
 * The context model represents the local system or component and includes:
 * 1. Function pointers for supported evaluators (string, numeric, unsigned
 * numeric).
 * 2. Parameters associated with each evaluator type (string, numeric, unsigned
 * numeric).
 * 3. Function pointers for supported actions.
 *
 * Since evaluation may occur at various stages of the application lifecycle,
 * the model should be updated promptly as new parameters become available.
 *
 */

#include "action.h"
#include "error_codes.h"
#include "evaluator_types.h"

#include <limits.h>

#define STR_NOT_SET NULL
#define NUM_NOT_SET LONG_MAX
#define UNUM_NOT_SET ULONG_MAX

/**
 * @brief opaque struct, defined in src/eval_ctx.h
 */
typedef struct eval_ctx eval_ctx;

/**
 * @brief Registers a new string evaluator.
 *
 * @param func_ptr a function pointer to the string evaluator function.
 * @param ix a number representing one of the StringEvaluators (up to
 * STR_EVAL_COUNT)
 * @return int on success DD_ESUCCESS(0), on error > 0 policies_errors code
 */
policies_errors eval_ctx_register_string_evaluator(string_evaluator_function_ptr func_ptr, StringEvaluators ix);

/**
 * @brief Registers a new numeric evaluator.
 *
 * @param func_ptr a function pointer to the numeric evaluator function.
 * @param ix a number representing one of the NumericEvaluators (up to
 * NUM_EVAL_COUNT)
 * @return int on success DD_ESUCCESS(0), on error > 0 policies_errors code
 */
policies_errors eval_ctx_register_numeric_evaluator(numeric_evaluator_function_ptr func_ptr, NumericEvaluators ix);

/**
 * @brief Registers a new unsigned numeric evaluator.
 *
 * @param func_ptr a function pointer to the unsigned numeric evaluator
 * function.
 * @param ix a number representing one of the UnumericEvaluators (up to
 * UNUM_EVAL_COUNT)
 * @return int on success DD_ESUCCESS(0), on error > 0 policies_errors code
 */
policies_errors eval_ctx_register_unumeric_evaluator(unumeric_evaluator_function_ptr func_ptr, NumericEvaluators ix);

/**
 * @brief Registers a new action.
 *
 * @param action a function pointer to the action function.
 * @param ix a number representing one of the Actions (up to ACTIONS_COUNT)
 * @return int on success DD_ESUCCESS(0), on error > 0 policies_errors code
 */
policies_errors eval_ctx_register_action(action_function_ptr action, Actions ix);

/**
 * @brief Sets the local string parameter for string evaluator ix
 *
 * @param value a string representing the local value to evaluate against *NOTE*
 * caller responsible for freeing.
 * @param ix a number representing one of the StringEvaluators (up to
 * STR_EVAL_COUNT)
 * @return int on success DD_ESUCCESS(0), on error > 0 policies_errors code
 */
policies_errors eval_ctx_set_str_eval_param(StringEvaluators ix, const char *value);

/**
 * @brief Sets the local numeric parameter for numeric evaluator ix
 *
 * @param ix a number representing one of the NumericEvaluators (up to
 * NUM_EVAL_COUNT)
 * @param value the local numeric value to evaluate against
 * @return int on success DD_ESUCCESS(0), on error > 0 policies_errors code
 */
policies_errors eval_ctx_set_num_eval_param(NumericEvaluators ix, const long value);

/**
 * @brief Sets the local unsigned numeric parameter for unsigned numeric
 * evaluator ix
 *
 * @param ix a number representing one of the UnumericEvaluators (up to
 * UNUM_EVAL_COUNT)
 * @param value the local unsigned numeric value to evaluate against
 * @return int on success DD_ESUCCESS(0), on error > 0 policies_errors code
 */
policies_errors eval_ctx_set_unum_eval_param(NumericEvaluators ix, const unsigned long value);

/**
 * @brief Get a function pointer for a specific evaluator id.
 * @param id StringEvaluators enum.
 * @return NULL on error (also sets ctx.error) or the function ptr.
 */
string_evaluator_function_ptr eval_ctx_get_string_evaluator(StringEvaluators id);

/**
 * @brief Get the parameter for a specific evaluator id.
 * @param id StringEvaluators enum.
 * @return STR_NOT_SET on error (also sets ctx.error) or a const char* value.
 */
const char *eval_ctx_get_string_param(StringEvaluators id);

/**
 * @brief Get a function pointer for a specific evaluator id.
 * @param id NumericEvaluators enum.
 * @return NULL on error (also sets ctx.error) or the function ptr.
 */
numeric_evaluator_function_ptr eval_ctx_get_numeric_evaluator(NumericEvaluators id);

/**
 * @brief Get the parameter for a specific evaluator id.
 * @param id NumericEvaluators enum.
 * @return NUM_NOT_SET on error (also sets ctx.error) or a long value.
 */
long eval_ctx_get_numeric_param(NumericEvaluators id);

/**
 * @brief Get a function pointer for a specific evaluator id.
 * @param id NumericEvaluators enum.
 * @return NULL on error (also sets ctx.error) or the function ptr.
 */
unumeric_evaluator_function_ptr eval_ctx_get_unumeric_evaluator(NumericEvaluators id);

/**
 * @brief Get the parameter for a specific evaluator id.
 * @param id NumericEvaluators enum.
 * @return NUM_NOT_SET on error (also sets ctx.error) or an unsigned long value.
 */
unsigned long eval_ctx_get_unumeric_param(NumericEvaluators id);

/**
 * @brief Get a function pointer for a specific action id.
 * @param ix Actions enum.
 * @return NULL on error (also sets ctx.error) or the function ptr.
 */
action_function_ptr eval_ctx_get_action(Actions ix);

/**
 * @brief An accessor for the last error
 * @return the last error as a policies_errors enum, NOTE: This will RESET the error!
 */
policies_errors eval_ctx_get_last_error(void);

/**
 * @brief An accessor for the last error that doesnt reset the error!
 * @return the last error as policies_error enum, NOTE: This WILL NOT RESET the error!
 */
policies_errors eval_ctx_peek_last_error(void);

/**
 * @brief Used to set an error in the evaluation context
 * @param error policies_errors enum
 */
void eval_ctx_set_error(policies_errors error);

/**
 * @brief Sets an error code for an action
 * @param ix An action ID from Actions enum
 * @param error policies_errors enum
 */
void eval_ctx_set_action_error(Actions ix, policies_errors error);

/**
 * @brief Sets an error code for a string evaluator
 * @param ix A StringEvaluators enum ID
 * @param error policies_errors enum
 */
void eval_ctx_set_str_eval_error(StringEvaluators ix, policies_errors error);

/**
 * @brief Sets an error code for a numeric evaluator
 * @param id A NumericEvaluators enum ID
 * @param error policies_errors enum
 */
void eval_ctx_set_num_eval_error(NumericEvaluators id, policies_errors error);

/**
 * @brief Sets an error code for an unsigned numeric evaluator
 * @param ix A NumericEvaluators enum ID
 * @param error policies_errors enum
 */
void eval_ctx_set_unum_eval_error(NumericEvaluators ix, policies_errors error);

/**
 * @brief Initializes the context model.
 * This function sets all evaluator function pointers to NULL and parameters to
 * their 'not set' values
 * @return
 */
int eval_ctx_init(void);

/**
 * @brief Resets all the error codes to DD_ESUCESS, must be run after each evaluation.
 * @param
 */
void eval_ctx_reset(void);

#define REGISTER_STR_EVAL_PARAM(id, function_ptr, param)                                                               \
  do {                                                                                                                 \
    eval_ctx_register_string_evaluator(function_ptr, id);                                                              \
    eval_ctx_set_str_eval_param(id, param);                                                                            \
  } while (0)
