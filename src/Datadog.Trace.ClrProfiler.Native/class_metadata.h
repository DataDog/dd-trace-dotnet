#ifndef DD_CLR_PROFILER_CLASS_METADATA_H_
#define DD_CLR_PROFILER_CLASS_METADATA_H_

#include <corhlpr.h>
#include "clr_helpers.h"

namespace trace {

class ClassMetadata {
 public:
  const ModuleID module_id = 0;
  const ClassID class_id = 0;
  const mdToken md_token = 0;

  ClassMetadata(ModuleID module_id, ClassID class_id, mdToken md_token)
      : module_id(module_id), class_id(class_id), md_token(md_token) {}
};

}  // namespace trace

#endif  // DD_CLR_PROFILER_CLASS_METADATA_H_
