#pragma once

#include <evaluators_reader.h>
#include <policy.h>

#include "action.h"
#include "dd_types.h"
#include "eval_ctx.h"
#include "evaluator_types.h"

/**
 * @brief Evaluates a buffer containing policies against the context model.
 *
 *
 * @param buffer The buffer containing the policies to evaluate.
 * @param ctx The context model containing evaluators, actions and parameters.
 * @return int Returns the total number of errors encountered during evaluation.
 */
policies_errors evaluate_buffer(uint8_t *buffer);
