/**
 * @file policies.h
 * @brief This file should serve as the main entry point for the policy engine.
 * Ideally you should only have to include it to use the policy engine.
 */
#pragma once

#include "eval_ctx.h"           // evaluation context (evaluator setters/getters, value setters, action setters, etc.)
#include "evaluator_default.h"  // default evaluators (str, num, unum)

#include <stdint.h>
#include "error_codes.h"

/**
 * @brief Evaluates a buffer containing policies against the context model.
 * @param buffer The buffer containing the policies to evaluate.
 * @return int Returns the total number of errors encountered during evaluation.
 */
plcs_errors plcs_evaluate_buffer(const uint8_t *buffer, size_t size);
