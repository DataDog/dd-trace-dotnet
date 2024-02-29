#pragma once

#ifdef __has_include                           // Check if __has_include is present
#  if __has_include(<memory_resource>)                // Check for a standard library
#    include <memory_resource>
namespace pmr
{
using namespace std::pmr;
}
#  elif __has_include(<experimental/memory_resource>) // Check for an experimental version
#    include <experimental/memory_resource>
namespace pmr
{
using namespace std::experiental::pmr;
}
#  else                                        // Not found at all
#     error "Missing <memory_resource>"
#  endif
#endif

class allocators
{
public:

    static pmr::memory_resource* get_default_stack_allocator();
};
