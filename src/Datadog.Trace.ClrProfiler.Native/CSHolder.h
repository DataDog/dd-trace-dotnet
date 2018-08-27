#pragma once
#include <cor.h>

class CSHolder {
 public:
  CSHolder(CRITICAL_SECTION* pcs);
  ~CSHolder();

 private:
  CRITICAL_SECTION* m_pcs;
};
