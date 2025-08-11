#include <policy_reader.h>

#include <dd/policies/error_codes.h>
#include <dd/policies/evaluator_default.h>
#include <dd/policies/policies.h>

#include "eval_ctx.h"
#include "policy.h"
#include "wire/action.h"
#include "wire/boolean_operation.h"
#include "wire/dd_types.h"
#include "wire/evaluation_result.h"
#include <stdio.h>
plcs_evaluation_result evaluate_rules(dd_ns(NodeTypeWrapper_table_t) node);

plcs_evaluation_result evaluate_string(dd_ns(StrEvaluator_table_t) eval_str, const char *description) {
  if (!eval_str) {
    return EVAL_RESULT_ABSTAIN;
  }

  plcs_string_evaluators eval_id = dd_ns(StrEvaluator_id)(eval_str);

  const char *param = plcs_eval_ctx_get_string_param(eval_id);

  plcs_string_evaluator_function_ptr eval = plcs_eval_ctx_get_string_evaluator(eval_id);
  if (!eval) {
    eval = plcs_default_string_evaluator;
  }

  // parameter could potentially be NULL, so we check if there was an explicit error
  if (plcs_eval_ctx_peek_last_error() == DD_ESUCCESS) {
    return eval(dd_ns(StrEvaluator_value)(eval_str), dd_ns(StrEvaluator_cmp)(eval_str), param, description, eval_id);
  }

  return EVAL_RESULT_ABSTAIN;
}

plcs_evaluation_result evaluate_numeric(dd_ns(NumEvaluator_table_t) eval_num, const char *description) {
  if (!eval_num) {
    return EVAL_RESULT_ABSTAIN;
  }

  plcs_numeric_evaluators eval_id = dd_ns(NumEvaluator_id)(eval_num);

  const long param = plcs_eval_ctx_get_numeric_param(eval_id);

  plcs_numeric_evaluator_function_ptr eval = plcs_eval_ctx_get_numeric_evaluator(eval_id);
  if (!eval) {
    eval = plcs_default_numeric_evaluator;
  }

  // parameter could potentially be NULL, so we check if there was an explicit error
  if (plcs_eval_ctx_peek_last_error() == DD_ESUCCESS) {
    return eval(dd_ns(NumEvaluator_value)(eval_num), dd_ns(NumEvaluator_cmp)(eval_num), param, description, eval_id);
  }

  return EVAL_RESULT_ABSTAIN;
}

plcs_evaluation_result evaluate_unumeric(dd_ns(UNumEvaluator_table_t) eval_unum, const char *description) {
  if (!eval_unum) {
    return EVAL_RESULT_ABSTAIN;
  }

  plcs_numeric_evaluators eval_id = dd_ns(UNumEvaluator_id)(eval_unum);

  const unsigned long param = plcs_eval_ctx_get_unumeric_param(eval_id);

  plcs_unumeric_evaluator_function_ptr eval = plcs_eval_ctx_get_unumeric_evaluator(eval_id);
  if (!eval) {
    eval = plcs_default_unumeric_evaluator;
  }

  // parameter could potentially be NULL, so we check if there was an explicit error
  if (plcs_eval_ctx_peek_last_error() == DD_ESUCCESS) {
    return eval(
        dd_ns(UNumEvaluator_value)(eval_unum), dd_ns(UNumEvaluator_cmp)(eval_unum), param, description, eval_id
    );
  }

  return EVAL_RESULT_ABSTAIN;
}

plcs_evaluation_result node_evaluator(dd_ns(EvaluatorNode_table_t) node) {
  plcs_evaluation_result result = EVAL_RESULT_ABSTAIN;
  if (!node) {
    return result;  // log error?
  }

  dd_ns(EvaluatorType_union_t) evaluator = dd_ns(EvaluatorNode_eval_union)(node);

  switch (evaluator.type) {
    case dd_ns(EvaluatorType_StrEvaluator):
      return evaluate_string(evaluator.value, dd_ns(EvaluatorNode_description)(node));

    case dd_ns(EvaluatorType_NumEvaluator):
      return evaluate_numeric(evaluator.value, dd_ns(EvaluatorNode_description)(node));

    case dd_ns(EvaluatorType_UNumEvaluator):
      return evaluate_unumeric(evaluator.value, dd_ns(EvaluatorNode_description)(node));
  }

  plcs_eval_ctx_set_error(DD_EUNKNOWN_EVAL_IX);
  return result;
}

plcs_evaluation_result DoAnd(plcs_evaluation_result a, plcs_evaluation_result b) {
  // 0 & {0, 1, x} -> 0
  if (a == EVAL_RESULT_FALSE || b == EVAL_RESULT_FALSE) {
    return EVAL_RESULT_FALSE;
  }

  // {1} & {1} -> a & b
  if (a != EVAL_RESULT_ABSTAIN && b != EVAL_RESULT_ABSTAIN) {
    return EVAL_RESULT_TRUE;
  }

  // {1, x} & {x} -> x
  return EVAL_RESULT_ABSTAIN;
}

plcs_evaluation_result DoOr(plcs_evaluation_result a, plcs_evaluation_result b) {
  // {1} | {0, 1, x} -> 1
  if (a == EVAL_RESULT_TRUE || b == EVAL_RESULT_TRUE) {
    return EVAL_RESULT_TRUE;
  }

  // {0, 1} | {0, 1} -> a | b
  if (a != EVAL_RESULT_ABSTAIN && b != EVAL_RESULT_ABSTAIN) {
    return EVAL_RESULT_FALSE;
  }

  // {0, x} | {x} -> x
  return EVAL_RESULT_ABSTAIN;
}

plcs_evaluation_result DoNot(plcs_evaluation_result res) {
  return res == EVAL_RESULT_ABSTAIN ? EVAL_RESULT_ABSTAIN : !res;
}

plcs_evaluation_result DoOper(dd_ns(BoolOperation_enum_t) oper, plcs_evaluation_result a, plcs_evaluation_result b) {
  switch (oper) {
    case dd_ns(BoolOperation_BOOL_AND):
      return DoAnd(a, b);
      break;

    case dd_ns(BoolOperation_BOOL_OR):
      return DoOr(a, b);
      break;

    case dd_ns(BoolOperation_BOOL_NOT):
      return DoNot(a);
      break;

    default:
      // error unknown result
      return EVAL_RESULT_ABSTAIN;
      break;
  }
}

plcs_evaluation_result composite_evaluator(dd_ns(CompositeNode_table_t) node) {
  if (!node) {
    return EVAL_RESULT_ABSTAIN;
  }

  dd_ns(NodeTypeWrapper_vec_t) children = dd_ns(CompositeNode_children)(node);
  size_t children_len = children ? dd_ns(NodeTypeWrapper_vec_len)(children) : 0;
  dd_ns(BoolOperation_enum_t) oper = dd_ns(CompositeNode_op)(node);

  plcs_evaluation_result res;

  switch (oper) {
    case dd_ns(BoolOperation_BOOL_OR):
      res = EVAL_RESULT_FALSE;
      break;

    case dd_ns(BoolOperation_BOOL_AND):
      res = EVAL_RESULT_TRUE;
      break;

    case dd_ns(BoolOperation_BOOL_NOT):
      // CAN ONLY HAVE ONE CHILD!
      // otherwise this is a non valid boolean operation
      if (children_len != 1) {
        // log error
        return EVAL_RESULT_ABSTAIN;
      }
      return DoNot(evaluate_rules(dd_ns(NodeTypeWrapper_vec_at)(children, 0)));
      break;
  }

  // keep iterating recursively over the tree
  for (size_t ix = 0; ix < children_len; ++ix) {
    res = DoOper(oper, res, evaluate_rules(dd_ns(NodeTypeWrapper_vec_at)(children, ix)));

    // short circuit
    if (oper == dd_ns(BoolOperation_BOOL_OR) && res == EVAL_RESULT_TRUE) {
      return res;
    }

    // short circuit
    if (oper == dd_ns(BoolOperation_BOOL_AND) && res == EVAL_RESULT_FALSE) {
      return res;
    }
  }

  return res;
}

plcs_evaluation_result evaluate_rules(dd_ns(NodeTypeWrapper_table_t) node) {
  switch (dd_ns(NodeTypeWrapper_node_type)(node)) {
    case dd_ns(NodeType_EvaluatorNode):
      dd_ns(EvaluatorNode_table_t) evaluator_node = dd_ns(NodeTypeWrapper_node)(node);
      return node_evaluator(evaluator_node);
      break;

    case dd_ns(NodeType_CompositeNode):
      dd_ns(CompositeNode_table_t) composite_node = dd_ns(NodeTypeWrapper_node)(node);
      return composite_evaluator(composite_node);
      break;

    default:
      // error, unknown node type!
      break;
  }

  // log error
  return EVAL_RESULT_ABSTAIN;
}

static inline plcs_errors perform_actions(plcs_evaluation_result eval_res, dd_ns(Action_vec_t) actions_vec) {
  plcs_errors res = DD_ESUCCESS;

  // iterate
  size_t len = dd_ns(Action_vec_len)(actions_vec);
  for (size_t ix = 0; ix < len; ++ix) {
    dd_ns(Action_table_t) action = dd_ns(Action_vec_at)(actions_vec, ix);
    int action_id = dd_ns(Action_action)(action);
    if (action_id >= dd_ns(ActionId_ACTIONS_COUNT) || !plcs_eval_ctx_get_action(action_id)) {
      continue;
    }
    size_t values_len = flatbuffers_vec_len(dd_ns(Action_values(action)));
    char *values[ACTION_VALUES_MAX];
    for (size_t v_ix = 0; v_ix < values_len; ++v_ix) {
      values[v_ix] = (char *)flatbuffers_string_vec_at(dd_ns(Action_values(action)), v_ix);
    }
    plcs_action_function_ptr action_function = plcs_eval_ctx_get_action(action_id);
    if (action_function) {
      res = action_function(eval_res, values, values_len, dd_ns(Action_description)(action), action_id);
      plcs_eval_ctx_set_action_error(action_id, res);
    } else {
      res = DD_EACTIONS_EVAL;
    }
  }

  return res;
}

plcs_errors evaluate_policy(dd_ns(Policy_table_t) policy) {
  // extract actions
  dd_ns(Action_vec_t) actions = dd_ns(Policy_actions)(policy);

  // extract rules
  dd_ns(NodeTypeWrapper_table_t) rules = dd_ns(Policy_rules)(policy);

  // // evaluate rules if they exist, otherwise return EVAL_RESULT_ABSTAIN
  plcs_evaluation_result eval_res = rules ? evaluate_rules(rules) : EVAL_RESULT_ABSTAIN;

  // perform actions given evaluation result
  return perform_actions(eval_res, actions);
}

plcs_errors plcs_evaluate_buffer(const uint8_t *buffer, size_t size) {
  dd_ns(Policy_vec_t) policies = plcs_get_policies(buffer, size);
  if (!policies) {
    // not necessarily an error, could be empty policies
    return DD_ENO_DATA;
  }

  size_t policies_count = dd_ns(Policy_vec_len)(policies);
  plcs_errors total_errors = 0;
  for (size_t ix = 0; ix < policies_count; ++ix) {
    dd_ns(Policy_table_t) policy = dd_ns(Policy_vec_at)(policies, ix);
    if (!policy) {
      // not necessarily an error, could be empty policy
      continue;
    }
    plcs_errors res = evaluate_policy(policy);
    // success is 0, errors are > 0, if total_errors is > 0, it means there was
    // an error
    // TODO: track these errors using an errono style map in the eval_ctx
    total_errors += res;
  }

  return total_errors;
}

const char *plcs_string_evaluators_to_string(enum plcs_string_evaluators v) {
  return dd_ns(StringEvaluators_name)(dd_streval_to_wire(v));
}

const char *plcs_numeric_evaluators_to_string(enum plcs_numeric_evaluators v) {
  return dd_ns(NumericEvaluators_name)(dd_numeval_to_wire(v));
}

const char *plcs_string_comparator_to_string(enum plcs_string_comparator v) {
  return dd_ns(CmpTypeSTR_name)(dd_strcmp_to_wire(v));
}

const char *plcs_numeric_comparator_to_string(enum plcs_numeric_comparator v) {
  return dd_ns(CmpTypeNUM_name)(dd_numcmp_to_wire(v));
}

const char *plcs_evaluation_result_to_string(enum plcs_evaluation_result res) {
  return dd_ns(EvaluationResult_name)(dd_evalresult_to_wire(res));
}

const char *plcs_actions_to_string(enum plcs_actions action) {
  return dd_ns(ActionId_name)(dd_action_to_wire(action));
}
