/*
 * Unit test stubs for eval_ctx module using utest.h
 *
 * These tests exercise basic registration, parameter setting, getters,
 * bounds checking, and a couple of default evaluator sanity checks.
 *
 * Build system is expected to compile this alongside test.c
 * which provides UTEST_MAIN().
 */
#define _GNU_SOURCE
#include "utest/utest.h"

#include <dd/policies/policies.h>

#include "actions_reader.h"
#include "wire/dd_types.h"

#include <stddef.h>
#include <stdint.h>
#include <string.h>
