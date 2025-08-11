#pragma once

/**
 * @brief Error codes for policy evaluation.
 *
 */
typedef enum plcs_errors {
  DD_ESUCCESS = 0,
  DD_EREGISTER_EVAL_PTR,
  DD_EIX_OVERFLOW,
  DD_ENULL_PTR,
  DD_EINITIZLIED,
  DD_ENO_DATA,
  DD_EUNKNOWN_EVAL_IX,
  DD_EACTIONS_EVAL,
  DD_EUNKNOWN_CMP,
} plcs_errors;
