#pragma once

#include "Integration.h"
#include "TypeReference.h"
#include "MemberReference.h"
#include "GlobalTypeReferences.h"

Integration CustomIntegration( /* IsEnabled */
                                  false,
                                  IntegrationType_Custom,
                                  std::vector<MemberReference>{
                                      // TODO: read from configuration
                                  });
