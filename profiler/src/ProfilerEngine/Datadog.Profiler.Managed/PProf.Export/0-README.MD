For now, the PPfor exporter is not a standalone library.
This directory contans types that would logically by public,
but they are still compiled as internal for now.

In the meantime, consider the namespace boundary to be the package boundary.
Types from other namespaces are only allowed to use types in this directory.