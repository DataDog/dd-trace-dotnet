
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