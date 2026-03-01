#pragma once
#ifdef BUILD_WITH_WORKLOAD_SELECTION
#include "workload_selection_impl.h"
#else
#include "workload_selection_noop.h"
#endif
