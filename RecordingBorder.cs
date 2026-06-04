using System.Drawing.Drawing2D;

namespace WinTool;

/// <summary>
/// 录制时显示的边框窗口 - 透明窗口只显示边框，支持拖动调整位置
/// </summary>
public sealed class RecordingBorder : Form
{
    private const int WM_NCHITTEST = 0x84;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_TOPMOST = 0x8;

    // 边框宽度加大，方便拖动
    private const int BorderWidth = 6;
    private const int ResizeGripSize = 12;
    private const int CloseButtonSize = 24;
    private const int TopPadding = 35; // 顶部留出空间放关闭按钮

    private bool _isDragging;
    private Point _dragStart;
    private bool _allowResize = true;
    private bool _isHoveringCloseButton;
    private Rectangle _closeButtonRect;
    private float _animationPhase;
    private System.Windows.Forms.Timer? _animationTimer;

    /// <summary>
    /// 区域变化事件
    /// </summary>
    public new event Action<Rectangle>? OnRegionChanged;

    /// <summary>
    /// 关闭按钮点击事件
    /// </summary>
    public event Action? OnCloseButtonClicked;

    /// <summary>
    /// 实际录制区域（不包含边框和顶部按钮区域）
    /// </summary>
    public Rectangle CaptureRegion => new(
        Left + BorderWidth,
        Top + TopPadding,
        Width - BorderWidth * 2,
        Height - TopPadding - BorderWidth);

    public RecordingBorder()
    {
        InitializeForm();
        StartAnimation();
    }

    public RecordingBorder(Rectangle region)
    {
        InitializeForm();
        SetRegion(region);
        StartAnimation();
    }

    private void InitializeForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
        MinimumSize = new Size(100, 100);
    }

    private void StartAnimation()
    {
        _animationTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _animationTimer.Tick += (_, _) =>
        {
            _animationPhase += 0.3f;
            if (_animationPhase > 8) _animationPhase = 0;
            Invalidate();
        };
        _animationTimer.Start();
    }

    public void SetRegion(Rectangle region)
    {
        // 窗口位置和大小要包含边框和顶部按钮区域
        Location = new Point(region.X - BorderWidth, region.Y - TopPadding);
        Size = new Size(region.Width + BorderWidth * 2, region.Height + TopPadding + BorderWidth);
        UpdateCloseButtonRect();
    }

    public void SetAllowResize(bool allow)
    {
        _allowResize = allow;
        Invalidate();
    }

    private void UpdateCloseButtonRect()
    {
        // 关闭按钮在右上角（窗口内部）
        _closeButtonRect = new Rectangle(
            Width - CloseButtonSize - 8,
            8,
            CloseButtonSize,
            CloseButtonSize);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 计算录制区域的边框矩形
        var borderRect = new Rectangle(
            BorderWidth / 2,
            TopPadding - BorderWidth / 2,
            Width - BorderWidth,
            Height - TopPadding);

        // 绘制外发光效果
        for (var i = 3; i >= 1; i--)
        {
            using var glowPen = new Pen(Color.FromArgb(40 - i * 10, 52, 152, 219), i * 2 + BorderWidth);
            g.DrawRectangle(glowPen, borderRect);
        }

        // 绘制主边框（动画虚线）
        using var pen = new Pen(Color.FromArgb(52, 152, 219), BorderWidth);
        pen.DashStyle = DashStyle.Dash;
        pen.DashPattern = new float[] { 4, 3 };
        pen.DashOffset = _animationPhase;
        g.DrawRectangle(pen, borderRect);

        // 绘制内边框
        using var innerPen = new Pen(Color.FromArgb(150, 255, 255, 255), 1);
        g.DrawRectangle(innerPen,
            BorderWidth + 1,
            TopPadding + 1,
            Width - BorderWidth * 2 - 2,
            Height - TopPadding - BorderWidth - 2);

        // 如果允许调整大小，绘制调整手柄和关闭按钮
        if (_allowResize)
        {
            DrawResizeHandles(g, borderRect);
            DrawCloseButton(g);
        }

        // 绘制尺寸信息
        DrawSizeInfo(g);
    }

    private void DrawResizeHandles(Graphics g, Rectangle borderRect)
    {
        using var handleBrush = new SolidBrush(Color.FromArgb(52, 152, 219));
        using var handlePen = new Pen(Color.White, 1);
        var size = ResizeGripSize;

        // 四个角的手柄
        var handles = new[]
        {
            // 左上
            new Rectangle(0, TopPadding - size / 2, size, size),
            // 右上
            new Rectangle(Width - size, TopPadding - size / 2, size, size),
            // 左下
            new Rectangle(0, Height - size, size, size),
            // 右下
            new Rectangle(Width - size, Height - size, size, size),
            // 上中
            new Rectangle(Width / 2 - size / 2, TopPadding - size / 2, size, size),
            // 下中
            new Rectangle(Width / 2 - size / 2, Height - size, size, size),
            // 左中
            new Rectangle(0, TopPadding + (Height - TopPadding) / 2 - size / 2, size, size),
            // 右中
            new Rectangle(Width - size, TopPadding + (Height - TopPadding) / 2 - size / 2, size, size),
        };

        foreach (var handle in handles)
        {
            g.FillRectangle(handleBrush, handle);
            g.DrawRectangle(handlePen, handle);
        }
    }

    private void DrawCloseButton(Graphics g)
    {
        UpdateCloseButtonRect();

        // 背景颜色（悬停时变红）
        var bgColor = _isHoveringCloseButton
            ? Color.FromArgb(230, 231, 76, 60)
            : Color.FromArgb(230, 52, 152, 219);

        using var bgBrush = new SolidBrush(bgColor);
        using var borderPen = new Pen(Color.White, 1);

        // 绘制圆形背景
        g.FillEllipse(bgBrush, _closeButtonRect);
        g.DrawEllipse(borderPen, _closeButtonRect);

        // 绘制 X 图标
        using var xPen = new Pen(Color.White, 2.5f);
        xPen.StartCap = LineCap.Round;
        xPen.EndCap = LineCap.Round;

        var padding = 7;
        g.DrawLine(xPen,
            _closeButtonRect.X + padding, _closeButtonRect.Y + padding,
            _closeButtonRect.Right - padding, _closeButtonRect.Bottom - padding);
        g.DrawLine(xPen,
            _closeButtonRect.Right - padding, _closeButtonRect.Y + padding,
            _closeButtonRect.X + padding, _closeButtonRect.Bottom - padding);
    }

    private void DrawSizeInfo(Graphics g)
    {
        var region = CaptureRegion;
        var sizeText = $"{region.Width} x {region.Height}";
        using var font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        using var bgBrush = new SolidBrush(Color.FromArgb(220, 52, 152, 219));

        var textSize = g.MeasureString(sizeText, font);
        var textX = BorderWidth + 10;
        var textY = (TopPadding - textSize.Height) / 2;

        var bgRect = new RectangleF(textX - 6, textY - 2, textSize.Width + 12, textSize.Height + 4);
        using var path = CreateRoundedRectangle(bgRect, 4);
        g.FillPath(bgBrush, path);
        g.DrawString(sizeText, font, textBrush, textX, textY);

        // 显示提示文字
        if (_allowResize)
        {
            var tipText = "拖动边框调整 | 点击X关闭";
            using var tipFont = new Font("Microsoft YaHei UI", 8);
            var tipSize = g.MeasureString(tipText, tipFont);
            var tipX = textX + bgRect.Width + 15;
            var tipY = (TopPadding - tipSize.Height) / 2;
            using var tipBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255));
            g.DrawString(tipText, tipFont, tipBrush, tipX, tipY);
        }
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            var pt = PointToClient(new Point(m.LParam.ToInt32()));
            
            // 关闭按钮区域返回 HTCLIENT，让鼠标事件正常传递
            if (_allowResize && _closeButtonRect.Contains(pt))
            {
                m.Result = (IntPtr)1; // HTCLIENT
                return;
            }
            
            if (_allowResize)
            {
                var hitResult = GetHitTestResult(pt);
                if (hitResult != 0)
                {
                    m.Result = (IntPtr)hitResult;
                    return;
                }
            }
        }
        base.WndProc(ref m);
    }

    private int GetHitTestResult(Point pt)
    {
        var grip = ResizeGripSize;

        // 顶部工具栏区域（除关闭按钮外）可拖动
        if (pt.Y < TopPadding - grip / 2)
        {
            return HTCAPTION;
        }

        // 检查四个角
        if (pt.X < grip && pt.Y < TopPadding + grip) return HTTOPLEFT;
        if (pt.X >= Width - grip && pt.Y < TopPadding + grip) return HTTOPRIGHT;
        if (pt.X < grip && pt.Y >= Height - grip) return HTBOTTOMLEFT;
        if (pt.X >= Width - grip && pt.Y >= Height - grip) return HTBOTTOMRIGHT;

        // 检查四条边
        if (pt.X < grip) return HTLEFT;
        if (pt.X >= Width - grip) return HTRIGHT;
        if (pt.Y < TopPadding + grip) return HTTOP;
        if (pt.Y >= Height - grip) return HTBOTTOM;

        // 中间区域可拖动
        return HTCAPTION;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            // 检查是否点击了关闭按钮
            if (_allowResize && _closeButtonRect.Contains(e.Location))
            {
                OnCloseButtonClicked?.Invoke();
                Close();
                return;
            }

            _isDragging = true;
            _dragStart = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDragging)
        {
            var newLocation = new Point(
                Left + e.X - _dragStart.X,
                Top + e.Y - _dragStart.Y);
            Location = newLocation;
        }

        // 检查是否悬停在关闭按钮上
        if (_allowResize)
        {
            var wasHovering = _isHoveringCloseButton;
            _isHoveringCloseButton = _closeButtonRect.Contains(e.Location);
            if (wasHovering != _isHoveringCloseButton)
            {
                Invalidate();
            }

            // 更新鼠标指针
            if (_isHoveringCloseButton)
            {
                Cursor = Cursors.Hand;
            }
            else
            {
                UpdateCursor(e.Location);
            }
        }
        else
        {
            Cursor = Cursors.Default;
        }
    }

    private void UpdateCursor(Point pt)
    {
        var grip = ResizeGripSize;

        // 顶部工具栏区域
        if (pt.Y < TopPadding - grip / 2)
        {
            Cursor = Cursors.SizeAll;
            return;
        }

        if ((pt.X < grip && pt.Y < TopPadding + grip) || (pt.X >= Width - grip && pt.Y >= Height - grip))
            Cursor = Cursors.SizeNWSE;
        else if ((pt.X >= Width - grip && pt.Y < TopPadding + grip) || (pt.X < grip && pt.Y >= Height - grip))
            Cursor = Cursors.SizeNESW;
        else if (pt.X < grip || pt.X >= Width - grip)
            Cursor = Cursors.SizeWE;
        else if (pt.Y < TopPadding + grip || pt.Y >= Height - grip)
            Cursor = Cursors.SizeNS;
        else
            Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_isHoveringCloseButton)
        {
            _isHoveringCloseButton = false;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            OnRegionChanged?.Invoke(CaptureRegion);
        }
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        // 确保宽高是偶数（FFmpeg 要求）
        var captureRegion = CaptureRegion;
        var needAdjust = false;
        var newWidth = Width;
        var newHeight = Height;

        if (captureRegion.Width % 2 != 0)
        {
            newWidth--;
            needAdjust = true;
        }
        if (captureRegion.Height % 2 != 0)
        {
            newHeight--;
            needAdjust = true;
        }

        if (needAdjust)
        {
            Size = new Size(newWidth, newHeight);
        }

        UpdateCloseButtonRect();
        OnRegionChanged?.Invoke(CaptureRegion);
        Invalidate();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        OnRegionChanged?.Invoke(CaptureRegion);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateCloseButtonRect();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer?.Stop();
            _animationTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
