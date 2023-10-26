// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Exporter.h"

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog::detail {

struct ExporterImpl
{

public:
    struct ExporterDeleter
    {
        void operator()(ddog_prof_Exporter* object)
        {
            ddog_prof_Exporter_drop(object);
        }
    };

    std::unique_ptr<ddog_prof_Exporter, ExporterDeleter> _exporter;
};
}; // namespace libdatadog::detail