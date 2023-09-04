#pragma once

#include "IpcEndpoint.h"
#include "IIpcRecorder.h"

class PidEndpoint : public IpcEndpoint
{
public:
    static PidEndpoint* Create(int pid, IIpcRecorder* pRecorder);

    virtual bool Close() override;

private:
    PidEndpoint(IIpcRecorder* pRecorder);
    static PidEndpoint* CreateForWindows(int pid, IIpcRecorder* pRecorder);
    //TODO: static PidEndpoint* CreateForLinux();

    void CloseForWindows();
};

