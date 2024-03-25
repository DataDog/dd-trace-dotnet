#include "allocators.h"

#include <memory>
#include <memory_resource>

pmr::memory_resource* allocators::get_default_stack_allocator()
{
    static std::unique_ptr<pmr::synchronized_pool_resource> instance = std::make_unique<pmr::synchronized_pool_resource>(
        pmr::pool_options{.max_blocks_per_chunk = 1000, .largest_required_pool_block = 1024 * sizeof(std::uintptr_t)}
    );

    return instance.get();
}
