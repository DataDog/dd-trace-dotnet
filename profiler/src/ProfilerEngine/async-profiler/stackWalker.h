/*
 * Copyright 2022 Nick Ripley
 * Copyright 2021 Andrei Pangin
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Modified by Nick Ripley to extract components needed for call stack unwinding
 */

#ifndef _STACKWALKER_H
#define _STACKWALKER_H

#include <stdint.h>

#include "codeCache.h"
#include "stack_context.h"

CodeCache *findLibraryByAddress(CodeCacheArray *cache, const void *address);

int stackWalk(CodeCacheArray *cache, ap::StackContext &sc,
              const ap::StackBuffer &buffer, void const **callchain,
              int max_depth, int skip);

#endif // _STACKWALKER_H
