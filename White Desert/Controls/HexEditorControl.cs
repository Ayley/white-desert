using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using White_Desert.Helper;
using White_Desert.Helper.Files;

namespace White_Desert.Controls;

public class HexEditorControl : TemplatedControl
{
    #region Properties

    public static readonly StyledProperty<IBufferSource?> DataSourceProperty =
        AvaloniaProperty.Register<HexEditorControl, IBufferSource?>(nameof(DataSource));

    public static readonly StyledProperty<bool> IsCaseSensitiveProperty =
        AvaloniaProperty.Register<HexEditorControl, bool>(nameof(IsCaseSensitive), true);

    public static readonly StyledProperty<bool> SearchWholeWordsProperty =
        AvaloniaProperty.Register<HexEditorControl, bool>(nameof(SearchWholeWords), false);

    public static readonly StyledProperty<bool> SearchFromTopProperty =
        AvaloniaProperty.Register<HexEditorControl, bool>(nameof(SearchFromTop), false);

    public IBufferSource? DataSource
    {
        get => GetValue(DataSourceProperty);
        set => SetValue(DataSourceProperty, value);
    }

    public bool IsCaseSensitive
    {
        get => GetValue(IsCaseSensitiveProperty);
        set => SetValue(IsCaseSensitiveProperty, value);
    }

    public bool SearchWholeWords
    {
        get => GetValue(SearchWholeWordsProperty);
        set => SetValue(SearchWholeWordsProperty, value);
    }

    public bool SearchFromTop
    {
        get => GetValue(SearchFromTopProperty);
        set => SetValue(SearchFromTopProperty, value);
    }

    #endregion

    private ScrollViewer? _scrollViewer;
    private Canvas? _virtualCanvas;
    private TextBox? _searchBox;

    private readonly double _lineHeight = 22;
    private readonly int _bytesPerLine = 16;
    private double _charWidth;
    private readonly Typeface _typeface = new(FontFamily.Parse("Cascadia Code, JetBrains Mono, monospace"));

    private long _selectionStart = -1;
    private long _selectionEnd = -1;
    private bool _isSelecting = false;
    private bool _startedSelectionInAscii = false;
    private Point _lastMousePosition;

    private readonly List<long> _searchResults = new();
    private readonly HashSet<long> _searchHitMap = new();
    private string _lastSearchPattern = "";
    private int _lastSearchPatternLength = -1;
    private int _currentSearchResultIndex = -1;
    private readonly DispatcherTimer _autoScrollTimer;

    public HexEditorControl()
    {
        InitializeCharWidth();
        _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _autoScrollTimer.Tick += (s, e) => HandleAutoScroll();
        Focusable = true;
    }

    private void InitializeCharWidth()
    {
        var ft = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _typeface, 14,
            Brushes.Black);
        _charWidth = ft.Width;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _virtualCanvas = e.NameScope.Find<Canvas>("PART_VirtualCanvas");
        _searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");

        if (e.NameScope.Find<Button>("PART_FindNext") is Button btnNext) btnNext.Click += (_, _) => ExecuteSearch();
        if (e.NameScope.Find<Button>("PART_ClearSearch") is Button btnClear) btnClear.Click += (_, _) => ClearSearch();

        if (_scrollViewer != null) _scrollViewer.ScrollChanged += (_, _) => InvalidateVisual();
        UpdateExtent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataSourceProperty)
        {
            ClearSearch();
            _selectionStart = -1;
            _selectionEnd = -1;
            UpdateExtent();
            _scrollViewer?.ScrollToHome();
            InvalidateVisual();
        }
    }

    private void UpdateExtent()
    {
        if (_virtualCanvas == null || DataSource == null) return;

        double neededWidth = (10 + (_bytesPerLine * 3) + 2 + _bytesPerLine + 2) * _charWidth;
        double lineCount = Math.Ceiling((double)DataSource.Length / _bytesPerLine);
        double neededHeight = lineCount * _lineHeight;

        _virtualCanvas.Width = neededWidth;
        _virtualCanvas.Height = Math.Max(neededHeight, 1);
    }

    public void ClearSearch()
    {
        _searchResults.Clear();
        _searchHitMap.Clear();
        _lastSearchPatternLength = -1;
        _currentSearchResultIndex = -1;
        _lastSearchPattern = "";
        if (_searchBox != null) _searchBox.Text = string.Empty;
        InvalidateVisual();
    }

    #region Interaction

    private void HandleAutoScroll()
    {
        if (_scrollViewer == null || !_isSelecting) return;
        double margin = 30;
        if (_lastMousePosition.Y > _scrollViewer.Bounds.Height - margin)
            _scrollViewer.Offset = _scrollViewer.Offset.WithY(_scrollViewer.Offset.Y + 15);
        else if (_lastMousePosition.Y < margin)
            _scrollViewer.Offset = _scrollViewer.Offset.WithY(Math.Max(0, _scrollViewer.Offset.Y - 15));

        _selectionEnd = GetOffsetFromPoint(_lastMousePosition);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetCurrentPoint(this);
        if (p.Properties.IsLeftButtonPressed)
        {
            this.Focus();
            long offset = GetOffsetFromPoint(p.Position);
            if (offset != -1)
            {
                _selectionStart = offset;
                _selectionEnd = offset;
                _isSelecting = true;
                _autoScrollTimer.Start();

                double hexStartX = 10 + (_charWidth * 10);
                double ascStartX = hexStartX + (_bytesPerLine * _charWidth * 3) + 20;
                double virtualX = p.Position.X + (_scrollViewer?.Offset.X ?? 0);
                _startedSelectionInAscii = virtualX >= ascStartX;
            }

            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _lastMousePosition = e.GetPosition(this); 
        if (_isSelecting)
        {
            _selectionEnd = GetOffsetFromPoint(_lastMousePosition);
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isSelecting = false;
        _autoScrollTimer.Stop();
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            await CopySelectionToClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (DataSource != null)
            {
                _selectionStart = 0;
                _selectionEnd = DataSource.Length - 1;
                InvalidateVisual();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _searchBox?.Focus();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    #endregion

    #region Rendering

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (DataSource == null || _scrollViewer == null || Bounds.Width <= 0) return;

        var scroll = _scrollViewer.Offset;

        double availableWidth = Math.Max(0, Bounds.Width - 300);
        var viewportRect = new Rect(0, 0, availableWidth, Bounds.Height);

        using (context.PushClip(viewportRect))
        {
            context.FillRectangle(Background ?? Brushes.Transparent, viewportRect);

            using (context.PushTransform(Matrix.CreateTranslation(-scroll.X, -scroll.Y)))
            {
                double addrX = 10;
                double hexX = 10 + (_charWidth * 10);
                double ascX = hexX + (_bytesPerLine * _charWidth * 3) + 20;

                double viewHeight = _scrollViewer.Viewport.Height > 0 ? _scrollViewer.Viewport.Height : Bounds.Height;

                int first = (int)(scroll.Y / _lineHeight);
                int last = first + (int)(viewHeight / _lineHeight) + 1;

                for (int i = first; i <= last; i++)
                {
                    long lineStart = (long)i * _bytesPerLine;
                    if (lineStart >= DataSource.Length) break;
                    double y = i * _lineHeight;

                    DrawText(context, lineStart.ToString("X8"), addrX, y, Brushes.Gray);

                    byte[] data = DataSource.GetSlice(lineStart, _bytesPerLine);
                    for (int b = 0; b < data.Length; b++)
                    {
                        long offset = lineStart + b;
                        if (offset >= DataSource.Length) break;

                        double xH = hexX + (b * _charWidth * 3);
                        double xA = ascX + (b * _charWidth);

                        RenderHighlights(context, offset, xH, xA, y);

                        DrawText(context, data[b].ToString("X2"), xH, y, Foreground ?? Brushes.Black);
                        char c = (char)data[b];
                        DrawText(context, (c < 32 || c > 126) ? "." : c.ToString(), xA, y, Foreground ?? Brushes.Black);
                    }
                }
            }
        }
    }

    private void RenderHighlights(DrawingContext context, long offset, double xH, double xA, double y)
    {
        if (_selectionStart != -1 && _selectionEnd != -1)
        {
            long start = Math.Min(_selectionStart, _selectionEnd);
            long end = Math.Max(_selectionStart, _selectionEnd);
            if (offset >= start && offset <= end)
            {
                var brush = new SolidColorBrush(_startedSelectionInAscii ? Colors.SeaGreen : Colors.DodgerBlue, 0.4);
                context.FillRectangle(brush, new Rect(xH - 2, y, _charWidth * 2.5, _lineHeight));
                context.FillRectangle(brush, new Rect(xA, y, _charWidth, _lineHeight));
            }
        }

        if (_searchHitMap.Contains(offset))
        {
            var searchBrush = new SolidColorBrush(Colors.Orange, 0.6); 
            context.FillRectangle(searchBrush, new Rect(xH - 2, y, _charWidth * 2.5, _lineHeight));
            context.FillRectangle(searchBrush, new Rect(xA, y, _charWidth, _lineHeight));
        }
    }

    private void DrawText(DrawingContext context, string text, double x, double y, IBrush brush)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _typeface, 14, brush);
        context.DrawText(ft, new Point(x, y));
    }

    #endregion

    private long GetOffsetFromPoint(Point p)
    {
        if (_scrollViewer == null || DataSource == null) return -1;

        double vx = p.X + _scrollViewer.Offset.X;
        double vy = p.Y + _scrollViewer.Offset.Y;

        double hexX = 10 + (_charWidth * 10);
        double ascX = hexX + (_bytesPerLine * _charWidth * 3) + 20;

        int line = (int)(vy / _lineHeight);
        int col = 0;

        if (vx >= ascX - 10)
            col = (int)Math.Round((vx - ascX) / _charWidth);
        else
            col = (int)Math.Round((vx - hexX) / (_charWidth * 3));

        long offset = (long)line * _bytesPerLine + Math.Clamp(col, 0, _bytesPerLine - 1);
        return Math.Clamp(offset, 0, DataSource.Length - 1);
    }

    private async void ExecuteSearch()
    {
        if (DataSource == null || _searchBox == null || string.IsNullOrWhiteSpace(_searchBox.Text)) return;

        string pattern = _searchBox.Text.Trim();

        if (pattern == _lastSearchPattern && _searchResults.Count > 0)
        {
            _currentSearchResultIndex = (_currentSearchResultIndex + 1) % _searchResults.Count;
            ScrollToOffset(_searchResults[_currentSearchResultIndex] - 20);
            return;
        }

        _lastSearchPattern = pattern;
        _currentSearchResultIndex = 0;
        _searchResults.Clear();
        _searchHitMap.Clear();

        byte[] targetBytes;
        bool isHexSearch = false;
        try
        {
            if (pattern.Contains(" ") ||
                (pattern.Length >= 2 && pattern.All(c => "0123456789ABCDEFabcdef ".Contains(c))))
            {
                targetBytes = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => byte.Parse(x, NumberStyles.HexNumber)).ToArray();
                isHexSearch = true;
            }
            else
            {
                targetBytes = Encoding.UTF8.GetBytes(pattern.ToLower());
            }
        }
        catch
        {
            return;
        }

        if (targetBytes.Length == 0) return;

        long totalLength = DataSource.Length;
        int chunkSize = 128 * 1024;
        int overlap = targetBytes.Length - 1;

        for (long offset = 0; offset < totalLength; offset += (chunkSize - overlap))
        {
            int toRead = (int)Math.Min(chunkSize, totalLength - offset);
            if (toRead < targetBytes.Length) break;

            byte[] chunk = await Dispatcher.UIThread.InvokeAsync(() => DataSource.GetSlice(offset, toRead));
            if (chunk == null) continue;

            await Task.Run(() =>
            {
                for (int i = 0; i <= chunk.Length - targetBytes.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < targetBytes.Length; j++)
                    {
                        byte b1 = chunk[i + j];
                        byte b2 = targetBytes[j];

                        if (!isHexSearch)
                        {
                            if (b1 >= 0x41 && b1 <= 0x5A) b1 += 0x20;
                        }

                        if (b1 != b2)
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        long globalOffset = offset + i;
                        if (!_searchResults.Contains(globalOffset))
                        {
                            _searchResults.Add(globalOffset);
                            for (int m = 0; m < targetBytes.Length; m++)
                            {
                                _searchHitMap.Add(globalOffset + m);
                            }
                        }
                    }
                }
            });
        }

        _lastSearchPatternLength = targetBytes.Length;
        if (_searchResults.Count > 0) ScrollToOffset(_searchResults[0] - 20);

        InvalidateVisual();
    }

    private void ScrollToOffset(long offset)
    {
        if (_scrollViewer == null) return;
        _scrollViewer.Offset = _scrollViewer.Offset.WithY(((double)offset / _bytesPerLine) * _lineHeight);
    }

    private async Task CopySelectionToClipboard()
    {
        if (DataSource == null || _selectionStart == -1) return;
        long start = Math.Min(_selectionStart, _selectionEnd);
        int len = (int)(Math.Max(_selectionStart, _selectionEnd) - start + 1);
        byte[] data = DataSource.GetSlice(start, len);
        string content = _startedSelectionInAscii
            ? Encoding.UTF8.GetString(data.Select(b => b < 32 || b > 126 ? (byte)'.' : b).ToArray())
            : BitConverter.ToString(data).Replace("-", " ");
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null) await topLevel.Clipboard.SetTextAsync(content);
    }
}