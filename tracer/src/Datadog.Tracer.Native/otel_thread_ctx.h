#pragma once

#ifdef LINUX

// Gets the calling thread's OTEP 4947 Thread-Local Context Record, allocating,
// publishing, and registering it for native thread-exit cleanup on first use.
// Returns nullptr when the record cannot be allocated or its destructor cannot
// be registered.
extern "C" __attribute__((visibility("default"))) void* GetOrCreateOtelThreadContextRecord();

#endif
