#pragma once

#include "evaluation_result.h"
#include "evaluator_types.h"

/**
 * @brief A default string evaluator.
 * @param policy the value provided by the policy (remote value)
 * @param cmp the comparison operator to use
 * @param ctx the value provided by the component (i.e: injector)
 * @param description a description of the evaluation, can be used for debugging
 * @param eval_id the enum of the evaluator
 * @return the result of the evaluation
 */
plcs_evaluation_result plcs_default_string_evaluator(
    const char *policy,
    const plcs_string_comparator cmp,
    const char *ctx,
    const char *description,
    plcs_string_evaluators eval_id
);

/**
 * @brief A default numeric evaluator.
 * @param policy the value provided by the policy (remote value)
 * @param cmp the comparison operator to use
 * @param ctx the value provided by the component (i.e: injector)
 * @param description a description of the evaluation, can be used for debugging
 * @param eval_id the enum of the evaluator
 * @return the result of the evaluation
 */
plcs_evaluation_result plcs_default_numeric_evaluator(
    const long policy,
    const plcs_numeric_comparator cmp,
    const long ctx,
    const char *description,
    plcs_numeric_evaluators eval_id
);

/**
 * @brief A default unsigned numeric evaluator.
 * @param policy the value provided by the policy (remote value)
 * @param cmp the comparison operator to use
 * @param ctx the value provided by the component (i.e: injector)
 * @param description a description of the evaluation, can be used for debugging
 * @param eval_id the enum of the evaluator
 * @return the result of the evaluation
 */
plcs_evaluation_result plcs_default_unumeric_evaluator(
    const unsigned long policy,
    const plcs_numeric_comparator cmp,
    const unsigned long ctx,
    const char *description,
    plcs_numeric_evaluators eval_id
);
