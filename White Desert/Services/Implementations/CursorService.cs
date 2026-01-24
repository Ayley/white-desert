using System;
using System.Net.Mime;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using White_Desert.Services.Contracts;

namespace White_Desert.Services.Implementations;

public class CursorService : ICursorService
{
    private TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    public void SetWaitCursor()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var topLevel = GetTopLevel();
            if (topLevel != null)
                topLevel.Cursor = new Cursor(StandardCursorType.Wait);
        });
    }

    public void SetDefaultCursor()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var topLevel = GetTopLevel();
            if (topLevel != null)
                topLevel.Cursor = Cursor.Default;
        });
    }

    public IDisposable BusyScope() => new BusyDisposable(this);

    private class BusyDisposable : IDisposable
    {
        private readonly ICursorService _service;
        public BusyDisposable(ICursorService service)
        {
            _service = service;
            _service.SetWaitCursor();
        }
        public void Dispose() => _service.SetDefaultCursor();
    }
}