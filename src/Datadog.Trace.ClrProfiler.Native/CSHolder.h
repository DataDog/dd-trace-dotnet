#ifndef DD_CLR_PROFILER_CS_HOLDER_H_
#define DD_CLR_PROFILER_CS_HOLDER_H_

#include <cor.h>

class CSHolder {
 public:
  CSHolder(CRITICAL_SECTION* pcs);
  ~CSHolder();

 private:
  CRITICAL_SECTION* m_pcs;
};

#endif  // DD_CLR_PROFILER_CS_HOLDER_H_
