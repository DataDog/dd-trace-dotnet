This directory contains a modified subset of the code from the
[async-profiler](https://github.com/jvm-profiling-tools/async-profiler) project
needed to implement async signal-safe DWARF call stack unwinding. This code
came from commit [56ae519224ed9a9b081fd8c384326784326fae43](https://github.com/jvm-profiling-tools/async-profiler/commit/56ae519224ed9a9b081fd8c384326784326fae43)

The following changes have been made to the original code:

* Anything not directly related to DWARF call stack unwinding has been removed.
* Java-related components of the call stack unwinding code have been removed.
* The `Profiler` class has been removed and its `getNativeFrames` method has
  been extracted to a stand-alone `async_profiler_backtrace` function. Its
  `CodeCacheArray` has been made into a global variable (wrapped by a singleton).
* The SEGV handler functionality is not used.