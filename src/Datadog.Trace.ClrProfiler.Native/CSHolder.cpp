#include <cassert>

#include "CSHolder.h"

CSHolder::CSHolder(CRITICAL_SECTION* pcs) {
  m_pcs = pcs;
  EnterCriticalSection(m_pcs);
}

CSHolder::~CSHolder() {
  assert(m_pcs != NULL);
  LeaveCriticalSection(m_pcs);
}
