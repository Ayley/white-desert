using System;

namespace White_Desert.Services.Contracts;

public interface ICursorService
{
    void SetWaitCursor();
    
    void SetDefaultCursor();
    
    IDisposable BusyScope();
}