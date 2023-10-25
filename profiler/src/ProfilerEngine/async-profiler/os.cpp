#include <unistd.h>

#include "os.h"

const size_t OS::page_size = sysconf(_SC_PAGESIZE);
const size_t OS::page_mask = OS::page_size - 1;