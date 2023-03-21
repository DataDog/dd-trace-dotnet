# Portable PDB native

A native library for working with the [Portable PDB](https://github.com/dotnet/core/blob/master/Documentation/diagnostics/portable_pdb.md) format.

## Project contents

`Dump/` - Tool to dump the contents of a Portable PDB to the console.

`Reader/` - Static library to read the contents of a Portable PDB.

`inc/` - Shared header files.

## Requirements

- [CMake](https://cmake.org/) version 3.0+
- Compiler with C++11 support

## Build

1) Create an output directory
    - e.g. `mkdir bin`
1) Generate the project
    - e.g. `cmake -S . -B bin`
1) Build the project
    - e.g. `cmake --build bin`

## References

[ECMA-335](https://www.ecma-international.org/publications/standards/Ecma-335.htm) - Metadata specification

[Portable PDB](https://github.com/dotnet/runtime/blob/master/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md) - Additional tables specification


## License
[MIT](https://opensource.org/licenses/MIT) - See files for details.