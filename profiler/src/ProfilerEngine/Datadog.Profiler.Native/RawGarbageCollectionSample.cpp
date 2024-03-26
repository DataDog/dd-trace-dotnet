#include "RawGarbageCollectionSample.h"


const std::vector<std::string> RawGarbageCollectionSample::_reasons =
{
    "AllocSmall",
    "Induced",
    "LowMemory",
    "Empty",
    "AllocLarge",
    "OutOfSpaceSOH",
    "OutOfSpaceLOH",
    "InducedNotForced",
    "Internal",
    "InducedLowMemory",
    "InducedCompacting",
    "LowMemoryHost",
    "PMFullGC",
    "LowMemoryHostBlocking"
};

const std::vector<std::string> RawGarbageCollectionSample::_types =
{
    "NonConcurrent",
    "Background",
    "Foreground"
};
