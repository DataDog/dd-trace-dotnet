using System;
using System.Collections.Generic;
using System.Diagnostics;

internal class GlobalListener : IObserver<DiagnosticListener>
{
    private readonly string _sourceName;
    private readonly object _target;

    public GlobalListener(string sourceName, object target)
    {
        _sourceName = sourceName;
        _target = target;
        DiagnosticListener.AllListeners.Subscribe(this);
    }

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener diagnosticListener)
    {
        if (diagnosticListener.Name == _sourceName)
        {
            diagnosticListener.SubscribeWithAdapter(_target);
        }
    }

    void IObserver<DiagnosticListener>.OnCompleted()
    {
    }

    void IObserver<DiagnosticListener>.OnError(Exception error)
    {
    }
}