using System.Drawing.Drawing2D;

namespace WinTool;

/// <summary>
/// 屏幕区域选择器窗口 - 虚影选择框效果
/// </summary>
public sealed class RegionSelector : Form
{
    private Point _startPoint;
    private Point _endPoint;
    private bool _isSelecting;
    private bool _hasSelection;
    private Rectangle _selectedRegion;
    private Bitmap? _screenCapture;
    private float _animationPhase;
    private System.Windows.Forms.Timer? _animationTimer;

    /// <summary>
    /// 用户选择的屏幕区域
    /// </summary>
    public Rectangle SelectedRegion => _selectedRegion;

    /// <summary>
    /// 是否已选择区域
    /// </summary>
    public bool HasSelection => _hasSelection;

    public RegionSelector()
    {
        InitializeForm();
        CaptureScreen();
        StartAnimation();
    }

    private void InitializeForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;

        var bounds = GetVirtualScreenBounds();
        Location = new Point(bounds.Left, bounds.Top);
        Size = new Size(bounds.Width, bounds.Height);
    }

    private void StartAnimation()
    {
        _animationTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _animationTimer.Tick += (_, _) =>
        {
            _animationPhase += 0.5f;
            if (_animationPhase > 8) _animationPhase = 0;
            if (_isSelecting || _hasSelection) Invalidate();
        };
        _animationTimer.Start();
    }

    private void CaptureScreen()
    {
        var bounds = GetVirtualScreenBounds();
        _screenCapture = new Bitmap(bounds.Width, bounds.Height);
        using var g = Graphics.FromImage(_screenCapture);
        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        var left = Screen.AllScreens.Min(s => s.Bounds.Left);
        var top = Screen.AllScreens.Min(s => s.Bounds.Top);
        var right = Screen.AllScreens.Max(s => s.Bounds.Right);
        var bottom = Screen.AllScreens.Max(s => s.Bounds.Bottom);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 绘制屏幕截图
        if (_screenCapture != null)
        {
            g.DrawImage(_screenCapture, 0, 0);
        }

        // 绘制半透明遮罩
        using var overlay = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
        g.FillRectangle(overlay, ClientRectangle);

        // 如果正在选择或已选择，绘制选择区域
        if (_isSelecting || _hasSelection)
        {
            var rect = GetSelectionRectangle();
            if (rect.Width > 0 && rect.Height > 0)
            {
                // 清除选择区域的遮罩，显示原始屏幕内容
                if (_screenCapture != null)
                {
                    g.SetClip(rect);
                    g.DrawImage(_screenCapture, 0, 0);
                    g.ResetClip();
                }

                // 绘制虚影边框（动画效果）
                DrawAnimatedBorder(g, rect);

                // 绘制四角和边中点的调整手柄
                DrawResizeHandles(g, rect);

                // 绘制尺寸信息
                DrawSizeInfo(g, rect);
            }
        }

        // 绘制提示文字
        DrawHelpText(g);
    }

    private void DrawAnimatedBorder(Graphics g, Rectangle rect)
    {
        // 外发光效果
        for (var i = 3; i >= 1; i--)
        {
            using var glowPen = new Pen(Color.FromArgb(50 - i * 15, 52, 152, 219), i * 2);
            g.DrawRectangle(glowPen, rect.X - i, rect.Y - i, rect.Width + i * 2, rect.Height + i * 2);
        }

        // 主边框（动画虚线）
        using var pen = new Pen(Color.FromArgb(52, 152, 219), 2);
        pen.DashStyle = DashStyle.Dash;
        pen.DashPattern = new float[] { 6, 4 };
        pen.DashOffset = _animationPhase;
        g.DrawRectangle(pen, rect);

        // 内边框
        using var innerPen = new Pen(Color.FromArgb(100, 255, 255, 255), 1);
        g.DrawRectangle(innerPen, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
    }

    private static void DrawResizeHandles(Graphics g, Rectangle rect)
    {
        var handleSize = 8;
        using var handleBrush = new SolidBrush(Color.FromArgb(52, 152, 219));
        using var handlePen = new Pen(Color.White, 1);

        var handles = new[]
        {
            new Rectangle(rect.X - handleSize / 2, rect.Y - handleSize / 2, handleSize, handleSize),
            new Rectangle(rect.Right - handleSize / 2, rect.Y - handleSize / 2, handleSize, handleSize),
            new Rectangle(rect.X - handleSize / 2, rect.Bottom - handleSize / 2, handleSize, handleSize),
            new Rectangle(rect.Right - handleSize / 2, rect.Bottom - handleSize / 2, handleSize, handleSize),
            new Rectangle(rect.X + rect.Width / 2 - handleSize / 2, rect.Y - handleSize / 2, handleSize, handleSize),
            new Rectangle(rect.X + rect.Width / 2 - handleSize / 2, rect.Bottom - handleSize / 2, handleSize, handleSize),
            new Rectangle(rect.X - handleSize / 2, rect.Y + rect.Height / 2 - handleSize / 2, handleSize, handleSize),
            new Rectangle(rect.Right - handleSize / 2, rect.Y + rect.Height / 2 - handleSize / 2, handleSize, handleSize),
        };

        foreach (var handle in handles)
        {
            g.FillRectangle(handleBrush, handle);
            g.DrawRectangle(handlePen, handle);
        }
    }

    private void DrawSizeInfo(Graphics g, Rectangle rect)
    {
        var sizeText = $"{rect.Width} x {rect.Height}";
        using var font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        using var bgBrush = new SolidBrush(Color.FromArgb(220, 52, 152, 219));

        var textSize = g.MeasureString(sizeText, font);
        var textX = rect.X + (rect.Width - textSize.Width) / 2;
        var textY = rect.Y - textSize.Height - 10;

        if (textY < 5) textY = rect.Bottom + 10;

        var bgRect = new RectangleF(textX - 8, textY - 2, textSize.Width + 16, textSize.Height + 4);

        // 圆角背景
        using var path = CreateRoundedRectangle(bgRect, 4);
        g.FillPath(bgBrush, path);
        g.DrawString(sizeText, font, textBrush, textX, textY);

        // 显示坐标信息
        var posText = $"({rect.X}, {rect.Y})";
        using var smallFont = new Font("Microsoft YaHei UI", 9);
        var posSize = g.MeasureString(posText, smallFont);
        var posX = rect.X;
        var posY = rect.Y - posSize.Height - 5;
        if (posY < textY + textSize.Height + 5) posY = rect.Bottom + 5;

        using var posBgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
        g.FillRectangle(posBgBrush, posX - 3, posY - 1, posSize.Width + 6, posSize.Height + 2);
        g.DrawString(posText, smallFont, textBrush, posX, posY);
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void DrawHelpText(Graphics g)
    {
        var helpText = _hasSelection
            ? "按 Enter 确认选择 | 按 Esc 取消 | 拖拽重新选择"
            : "拖拽鼠标选择录制区域 | 按 Esc 取消";

        using var font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold);
        using var brush = new SolidBrush(Color.White);
        using var bgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0));

        var textSize = g.MeasureString(helpText, font);
        var x = (ClientSize.Width - textSize.Width) / 2;
        var y = 30f;

        var bgRect = new RectangleF(x - 15, y - 8, textSize.Width + 30, textSize.Height + 16);
        using var path = CreateRoundedRectangle(bgRect, 8);
        g.FillPath(bgBrush, path);
        g.DrawString(helpText, font, brush, x, y);
    }

    private Rectangle GetSelectionRectangle()
    {
        var x = Math.Min(_startPoint.X, _endPoint.X);
        var y = Math.Min(_startPoint.Y, _endPoint.Y);
        var width = Math.Abs(_endPoint.X - _startPoint.X);
        var height = Math.Abs(_endPoint.Y - _startPoint.Y);
        return new Rectangle(x, y, width, height);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _isSelecting = true;
            _hasSelection = false;
            _startPoint = e.Location;
            _endPoint = e.Location;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isSelecting)
        {
            _endPoint = e.Location;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _isSelecting)
        {
            _isSelecting = false;
            _endPoint = e.Location;
            _selectedRegion = GetSelectionRectangle();

            // 确保选择区域有效（至少 50x50 像素）
            if (_selectedRegion.Width >= 50 && _selectedRegion.Height >= 50)
            {
                _hasSelection = true;

                // 转换为屏幕坐标
                var bounds = GetVirtualScreenBounds();
                _selectedRegion.X += bounds.Left;
                _selectedRegion.Y += bounds.Top;

                // 确保宽高是偶数（FFmpeg 要求）
                if (_selectedRegion.Width % 2 != 0) _selectedRegion.Width--;
                if (_selectedRegion.Height % 2 != 0) _selectedRegion.Height--;
            }

            Invalidate();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
        {
            _hasSelection = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }
        else if (e.KeyCode == Keys.Enter && _hasSelection)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer?.Stop();
            _animationTimer?.Dispose();
            _screenCapture?.Dispose();
        }
        base.Dispose(disposing);
    }
}
