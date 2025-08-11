#pragma once

#include <boolean_operation_reader.h>

#include "dd_types.h"

#define BOOL_OPER(name) TRANSLATE_ENUM(name, dd_ns(BoolOperation))

/**
 * @brief Representnts a boolean operation and declares a mapping between local
 * enums to flatbuffers representation
 *
 */
typedef enum BooleanOperation { BOOL_OPER(BOOL_AND), BOOL_OPER(BOOL_NOT), BOOL_OPER(BOOL_OR) } BooleanOperation;
