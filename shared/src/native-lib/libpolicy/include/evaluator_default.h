#pragma once

#include "evaluation_result.h"
#include "evaluator_types.h"

EvaluationResult default_string_evaluator(
    const char *policy,
    const StringComparator cmp,
    const char *ctx,
    const char *description,
    StringEvaluators eval_id
);

EvaluationResult default_numeric_evaluator(
    const long policy,
    const NumericComparator cmp,
    const long ctx,
    const char *description,
    NumericEvaluators eval_id
);

EvaluationResult default_unumeric_evaluator(
    const unsigned long policy,
    const NumericComparator cmp,
    const unsigned long ctx,
    const char *description,
    NumericEvaluators eval_id
);
