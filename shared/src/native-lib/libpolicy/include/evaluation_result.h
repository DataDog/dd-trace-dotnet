#pragma once

#include <evaluation_result_reader.h>

#include "dd_types.h"

/**
 * @brief defines a new type for boolean results, similar to optional where
 * RESULT_ABSTAIN defines a 'dont-care' (optional) state
 *
 */
typedef enum EvaluationResult {
  TRANSLATE_ENUM(RESULT_TRUE, dd_ns(EvaluationResult)),
  TRANSLATE_ENUM(RESULT_FALSE, dd_ns(EvaluationResult)),
  TRANSLATE_ENUM(RESULT_ABSTAIN, dd_ns(EvaluationResult))
} EvaluationResult;
