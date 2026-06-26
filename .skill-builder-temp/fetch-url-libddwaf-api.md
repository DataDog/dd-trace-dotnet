# libddwaf C API Reference

This document describes the public C API of libddwaf.

## Table of Contents

- [Enumerations](#enumerations)
  - [DDWAF_OBJ_TYPE](#ddwaf-obj-type)
  - [DDWAF_RET_CODE](#ddwaf-ret-code)
  - [DDWAF_LOG_LEVEL](#ddwaf-log-level)
- [Type Definitions](#type-definitions)
- [Functions](#functions)
  - [Initialization/Destruction](#initializationdestruction)
  - [Builder](#builder)
  - [Context](#context)
  - [Subcontext](#subcontext)
  - [Allocator](#allocator)
  - [Object Creation](#object-creation)
  - [Object Inspection](#object-inspection)
  - [Object Container Operations](#object-container-operations)
  - [Object Type Checking](#object-type-checking)
  - [Utility](#utility)

---

## Enumerations

### DDWAF_OBJ_TYPE

Specifies the type of a ddwaf::object.

| Value | Code | Description |
|-------|------|-------------|
| `DDWAF_OBJ_INVALID` | `= 0` | Unkmown or uninitialised type |
| `DDWAF_OBJ_NULL` | `= 0x01` | Null type, only used for its semantical value |
| `DDWAF_OBJ_BOOL` | `= 0x02` | Boolean type |
| `DDWAF_OBJ_SIGNED` | `= 0x04` | 64-bit signed integer type |
| `DDWAF_OBJ_UNSIGNED` | `= 0x06` | 64-bit unsigned integer type |
| `DDWAF_OBJ_FLOAT` | `= 0x08` | 64-bit float (or double) type |
| `DDWAF_OBJ_STRING` | `= 0x10` | Dynamic UTF-8 string of up to max(uint32) length |
| `DDWAF_OBJ_LITERAL_STRING` | `= 0x12` | Literal UTF-8 string of up to max(uint32) length, these are never freed |
| `DDWAF_OBJ_SMALL_STRING` | `= 0x14` | UTF-8 string of up to 14 bytes in length |
| `DDWAF_OBJ_ARRAY` | `= 0x20` | Array of ddwaf_object, up to max(uint16) capacity |
| `DDWAF_OBJ_MAP` | `= 0x40` | Array of ddwaf_object_kv, up to max(uint16) capacity |

### DDWAF_RET_CODE

Codes returned by ddwaf_context_eval.

| Value | Code | Description |
|-------|------|-------------|
| `DDWAF_ERR_INTERNAL` | `= -3` | Unknown error, typically due to an unexpected exception |
| `DDWAF_ERR_INVALID_OBJECT` | `= -2` | The provided data object didn't match the expected schema |
| `DDWAF_ERR_INVALID_ARGUMENT` | `= -1` | One or more of the provided arguments to a function is invalid |
| `DDWAF_OK` | `= 0` | The data evaluation didn't yield any events, attributes, etc |
| `DDWAF_MATCH` | `= 1` | The data evaluation resulted in an event, attribute, etc |

### DDWAF_LOG_LEVEL

Internal WAF log levels, to be used when setting the minimum log level and cb.

| Value | Code | Description |
|-------|------|-------------|
| `DDWAF_LOG_TRACE` | `= 0` | Finest-grained logging for detailed tracing |
| `DDWAF_LOG_DEBUG` | `= 1` | Debugging information for development |
| `DDWAF_LOG_INFO` | `= 2` | General informational messages |
| `DDWAF_LOG_WARN` | `= 3` | Warning messages for potential issues |
| `DDWAF_LOG_ERROR` | `= 4` | Error messages for failures |
| `DDWAF_LOG_OFF` | `= 5` | Disable all logging |

---

## Type Definitions

### Handle Types

| Type | Definition |
|------|------------|
| `ddwaf_handle` | `typedef struct _ddwaf_handle* ddwaf_handle` |
| `ddwaf_context` | `typedef struct _ddwaf_context* ddwaf_context` |
| `ddwaf_subcontext` | `typedef struct _ddwaf_subcontext* ddwaf_subcontext` |
| `ddwaf_builder` | `typedef struct _ddwaf_builder* ddwaf_builder` |
| `ddwaf_allocator` | `typedef struct _ddwaf_allocator* ddwaf_allocator` |
| `ddwaf_alloc_fn_type` | `typedef void *() ddwaf_alloc_fn_type(void *, size_t, size_t)` |
| `ddwaf_free_fn_type` | `typedef void() ddwaf_free_fn_type(void *, void *, size_t, size_t)` |
| `ddwaf_udata_free_fn_type` | `typedef void() ddwaf_udata_free_fn_type(void *)` |
| `ddwaf_object` | `typedef union _ddwaf_object ddwaf_object` |
| `ddwaf_object_kv` | `typedef struct _ddwaf_object_kv ddwaf_object_kv` |

### ddwaf_log_cb

```c
ddwaf_log_cb)(DDWAF_LOG_LEVEL level, const char *function, const char *file, unsigned line, const char *message, uint64_t message_len)
```

Callback that libddwaf will call to relay messages to the binding.

**Parameters:**

- `level`: The logging level.
- `function`: The native function that emitted the message. (nonnull)
- `file`: The file of the native function that emmitted the message. (nonnull)
- `line`: The line where the message was emmitted.
- `message`: The size of the logging message. NUL-terminated
- `message_len`: The length of the logging message (excluding NUL terminator).

---

## Functions

### Initialization/Destruction

#### ddwaf_init

```c
ddwaf_handle ddwaf_init(const ddwaf_object * ruleset, ddwaf_object * diagnostics)
```

Initialize a ddwaf instance

**Parameters:**

- `ruleset`: ddwaf::object map containing rules, exclusions, rules_override and rules_data. (nonnull)
- `diagnostics`: Optional ruleset parsing diagnostics. (nullable)

**Returns:** Handle to the WAF instance or NULL on error.

> **Note:** If ruleset is NULL, the diagnostics object will not be initialised.

> **Note:** The deallocation of the diagnostics must be made with default allocator.

#### ddwaf_destroy

```c
void ddwaf_destroy(ddwaf_handle handle)
```

Destroy a WAF instance.

**Parameters:**

- `handle`: Handle to the WAF instance.

#### ddwaf_known_addresses

```c
const char *const * ddwaf_known_addresses(const ddwaf_handle handle, uint32_t * size)
```

Get an array of known (root) addresses used by rules, exclusion filters and processors. This array contains both required and optional addresses. A more accurate distinction between required and optional addresses is provided within the diagnostics.

**Parameters:**

- `handle`: Handle to the WAF instance.
- `size`: Output parameter in which the size will be returned. The value of size will be 0 if the return value is NULL.

**Returns:** NULL if empty, otherwise a pointer to an array with size elements.

> **Note:** This function is not thread-safe

> **Note:** The returned array should be considered invalid after calling ddwaf_destroy on the handle used to obtain it.

#### ddwaf_known_actions

```c
const char *const * ddwaf_known_actions(const ddwaf_handle handle, uint32_t * size)
```

Get an array of all the action types which could be triggered as a result of the current set of rules and exclusion filters.

**Parameters:**

- `handle`: Handle to the WAF instance.
- `size`: Output parameter in which the size will be returned. The value of size will be 0 if the return value is NULL.

**Returns:** NULL if empty, otherwise a pointer to an array with size elements.

> **Note:** This function is not thread-safe

> **Note:** The returned array should be considered invalid after calling ddwaf_destroy on the handle used to obtain it.
