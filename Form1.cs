using System.Diagnostics;

namespace WinTool;

public partial class Form1 : Form
{
    private readonly Color _menuBg = ColorTranslator.FromHtml("#2C3E50");
    private readonly Color _menuHover = ColorTranslator.FromHtml("#34495E");
    private readonly Color _menuActive = ColorTranslator.FromHtml("#3498DB");
    private readonly Color _pageBg = ColorTranslator.FromHtml("#ECF0F1");
    private readonly Font _titleFont = new("Microsoft YaHei UI", 22, FontStyle.Bold);
    private readonly Font _subtitleFont = new("Microsoft YaHei UI", 16, FontStyle.Bold);
    private readonly Font _descFont = new("Microsoft YaHei UI", 10, FontStyle.Regular);
    private readonly Font _buttonFont = new("Microsoft YaHei UI", 10, FontStyle.Regular);
    private readonly Font _logFont = new("Consolas", 10, FontStyle.Regular);

    private readonly PythonBridge _pythonBridge = new();

    private readonly Dictionary<string, Panel> _pages = [];
    private readonly List<Button> _menuButtons = [];

    private Button? _currentSelectedButton;
    private Panel _submenuVideo = null!;
    private Panel _submenuImage = null!;
    private Panel _submenuOther = null!;
    private Panel _contentHost = null!;

    private string _videoPath = string.Empty;
    private string _videoOutputPath = string.Empty;
    private string _dedupFolderPath = string.Empty;
    private string _fileSizePath = string.Empty;
    private bool _deleteDuplicateImages;

    private TextBox _videoLog = null!;
    private TextBox _dedupLog = null!;
    private TextBox _fileSizeLog = null!;
    private TextBox _alarmLog = null!;
    private Label _videoPathLabel = null!;
    private Label _videoOutputLabel = null!;
    private Label _dedupFolderLabel = null!;
    private Label _fileSizePathLabel = null!;
    private TextBox _fileSizeThresholdBox = null!;

    private TextBox _alarmYearBox = null!;
    private TextBox _alarmMonthBox = null!;
    private TextBox _alarmDayBox = null!;
    private TextBox _alarmHourBox = null!;
    private TextBox _alarmMinuteBox = null!;
    private TextBox _alarmContentBox = null!;
    private Button _setAlarmButton = null!;
    private Button _stopAlarmButton = null!;
    private CancellationTokenSource? _alarmCts;
    private Process? _speechProcess;

    public Form1()
    {
        InitializeComponent();
        BuildUi();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _alarmCts?.Cancel();
        StopSpeechLoop();
        base.OnFormClosing(e);
    }

    private void BuildUi()
    {
        Text = "window小工具集";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);
        Size = new Size(1100, 700);
        BackColor = _pageBg;

        var mainContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = _pageBg
        };
        mainContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        mainContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(mainContainer);

        var leftMenu = BuildLeftMenu();
        _contentHost = new Panel { Dock = DockStyle.Fill, BackColor = _pageBg };

        mainContainer.Controls.Add(leftMenu, 0, 0);
        mainContainer.Controls.Add(_contentHost, 1, 0);

        BuildPages();
        ShowPage("home");
    }

    private Panel BuildLeftMenu()
    {
        var root = new Panel { Dock = DockStyle.Fill, BackColor = _menuBg };
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = _menuBg
        };
        root.Controls.Add(layout);

        var home = CreateMainMenuButton("首页");
        home.Click += (_, _) => { SetSelected(home); ShowPage("home"); };
        layout.Controls.Add(home);

        var video = CreateMainMenuButton("视频");
        video.Click += (_, _) => ToggleSubmenu(_submenuVideo, video);
        layout.Controls.Add(video);

        _submenuVideo = CreateSubmenuPanel(
            ("视频帧转图片", "video_extract"),
            ("其他视频工具", "video_other")
        );
        layout.Controls.Add(_submenuVideo);

        var image = CreateMainMenuButton("图片");
        image.Click += (_, _) => ToggleSubmenu(_submenuImage, image);
        layout.Controls.Add(image);

        _submenuImage = CreateSubmenuPanel(
            ("图片去重", "image_dedup"),
            ("图片处理", "image_process")
        );
        layout.Controls.Add(_submenuImage);

        var other = CreateMainMenuButton("其他工具");
        other.Click += (_, _) => ToggleSubmenu(_submenuOther, other);
        layout.Controls.Add(other);

        _submenuOther = CreateSubmenuPanel(
            ("文件大小工具", "file_size"),
            ("智能闹钟工具", "alarm")
        );
        layout.Controls.Add(_submenuOther);

        SetSelected(home);
        return root;
    }

    private Panel CreateSubmenuPanel(params (string text, string pageKey)[] items)
    {
        var panel = new Panel
        {
            Width = 200,
            Height = 40 * items.Length,
            Visible = false,
            BackColor = _menuBg
        };

        var y = 0;
        foreach (var item in items)
        {
            var btn = new Button
            {
                Text = item.text,
                Size = new Size(180, 40),
                Location = new Point(20, y),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 10, FontStyle.Regular),
                BackColor = _menuBg,
                ForeColor = Color.Gainsboro,
                TextAlign = ContentAlignment.MiddleLeft
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, _) =>
            {
                SetSelected(btn);
                ShowPage(item.pageKey);
            };
            AttachMenuHover(btn, isMainMenu: false);
            _menuButtons.Add(btn);
            panel.Controls.Add(btn);
            y += 40;
        }

        return panel;
    }

    private Button CreateMainMenuButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(200, 50),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold),
            BackColor = _menuBg,
            ForeColor = Color.White
        };
        btn.FlatAppearance.BorderSize = 0;
        AttachMenuHover(btn, isMainMenu: true);
        _menuButtons.Add(btn);
        return btn;
    }

    private void AttachMenuHover(Button btn, bool isMainMenu)
    {
        btn.MouseEnter += (_, _) =>
        {
            if (btn != _currentSelectedButton)
            {
                btn.BackColor = _menuHover;
            }
        };
        btn.MouseLeave += (_, _) =>
        {
            if (btn != _currentSelectedButton)
            {
                btn.BackColor = _menuBg;
            }
        };
    }

    private void ToggleSubmenu(Panel panel, Button owner)
    {
        panel.Visible = !panel.Visible;
        if (panel.Visible)
        {
            SetSelected(owner);
        }
    }

    private void SetSelected(Button button)
    {
        foreach (var btn in _menuButtons)
        {
            btn.BackColor = _menuBg;
            btn.ForeColor = btn.Width == 200 ? Color.White : Color.Gainsboro;
        }
        button.BackColor = _menuActive;
        button.ForeColor = Color.White;
        _currentSelectedButton = button;
    }

    private void BuildPages()
    {
        AddPage("home", BuildSimpleInfoPage(
            "window小工具集",
            "欢迎使用window小工具集！\r\n\r\n功能说明：\r\n• 视频模块：支持视频帧提取为图片、视频格式转换等\r\n• 图片模块：支持图片去重、图片批量处理等\r\n• 其他工具模块：支持判断文件或文件夹大小、智能闹钟语音提醒\r\n\r\n使用方式：点击左侧菜单选择对应功能"));

        AddPage("video_other", BuildSimpleInfoPage("视频其他工具", "规划中..."));
        AddPage("image_process", BuildSimpleInfoPage("图片处理工具", "规划中..."));

        AddPage("video_extract", BuildVideoExtractPage());
        AddPage("image_dedup", BuildImageDedupPage());
        AddPage("file_size", BuildFileSizePage());
        AddPage("alarm", BuildAlarmPage());
    }

    private void AddPage(string key, Panel page)
    {
        page.Visible = false;
        _pages[key] = page;
        _contentHost.Controls.Add(page);
    }

    private void ShowPage(string key)
    {
        foreach (var page in _pages.Values)
        {
            page.Visible = false;
        }
        if (_pages.TryGetValue(key, out var selected))
        {
            selected.Visible = true;
            selected.BringToFront();
        }
    }

    private Panel BuildSimpleInfoPage(string title, string description)
    {
        var panel = CreatePageContainer();
        var layout = CreateVerticalLayout(panel);
        layout.Controls.Add(CreateTitleLabel(title, _titleFont));
        layout.Controls.Add(CreateDescriptionLabel(description));
        return panel;
    }

    private Panel BuildVideoExtractPage()
    {
        var panel = CreatePageContainer();
        var layout = CreateVerticalLayout(panel);
        layout.Controls.Add(CreateTitleLabel("视频帧提取工具", _titleFont));
        layout.Controls.Add(CreateDescriptionLabel("功能说明：将视频文件按帧提取为 PNG 图片，支持 MP4/AVI/MOV/MKV。\r\n使用步骤：1.选择视频文件 -> 2.选择输出文件夹 -> 3.点击开始提取 -> 4.查看日志"));

        var fileGroup = CreateGroup("文件选择");
        var fileLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        _videoPathLabel = new Label { Text = "未选择视频文件", AutoSize = true, MaximumSize = new Size(600, 0) };
        _videoOutputLabel = new Label { Text = "未选择输出文件夹", AutoSize = true, MaximumSize = new Size(600, 0) };
        fileLayout.Controls.Add(CreateActionRow("选择视频文件", _videoPathLabel, SelectVideoFile));
        fileLayout.Controls.Add(CreateActionRow("选择输出文件夹", _videoOutputLabel, SelectVideoOutputFolder));
        fileGroup.Controls.Add(fileLayout);
        layout.Controls.Add(fileGroup);

        var runButton = CreatePrimaryButton("开始提取");
        runButton.Click += async (_, _) => await RunVideoExtractAsync(runButton);
        layout.Controls.Add(runButton);

        _videoLog = CreateLogTextBox();
        layout.Controls.Add(CreateLogGroup("提取日志", _videoLog));
        return panel;
    }

    private Panel BuildImageDedupPage()
    {
        var panel = CreatePageContainer();
        var layout = CreateVerticalLayout(panel);
        layout.Controls.Add(CreateTitleLabel("图片去重工具", _titleFont));
        layout.Controls.Add(CreateDescriptionLabel("功能说明：扫描指定文件夹内图片，识别重复/相似图片（PNG/JPG/JPEG/WEBP）。\r\n使用步骤：1.选择文件夹 -> 2.选择是否删除重复图片 -> 3.开始去重 -> 4.查看日志"));

        var group = CreateGroup("去重设置");
        var inside = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        _dedupFolderLabel = new Label { Text = "未选择文件夹", AutoSize = true, MaximumSize = new Size(600, 0) };
        inside.Controls.Add(CreateActionRow("去重文件夹", _dedupFolderLabel, SelectDedupFolder));

        var check = new CheckBox
        {
            Text = "删除重复图片（保留一张）",
            AutoSize = true,
            Font = _descFont
        };
        check.Click += (_, _) =>
        {
            if (check.Checked)
            {
                var dialog = MessageBox.Show("会删除重复图片，但会保留一张不重复图片。\r\n是否确认开启该功能？", "警告", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                _deleteDuplicateImages = dialog == DialogResult.OK;
                check.Checked = _deleteDuplicateImages;
                AppendLog(_dedupLog, _deleteDuplicateImages ? "⚠️ 已开启删除重复图片功能（保留一张）" : "ℹ️ 已关闭删除重复图片功能");
            }
            else
            {
                _deleteDuplicateImages = false;
                AppendLog(_dedupLog, "ℹ️ 已关闭删除重复图片功能");
            }
        };
        inside.Controls.Add(check);
        group.Controls.Add(inside);
        layout.Controls.Add(group);

        var runButton = CreatePrimaryButton("开始去重");
        runButton.Click += async (_, _) => await RunImageDedupAsync(runButton);
        layout.Controls.Add(runButton);

        _dedupLog = CreateLogTextBox();
        layout.Controls.Add(CreateLogGroup("去重日志", _dedupLog));
        return panel;
    }

    private Panel BuildFileSizePage()
    {
        var panel = CreatePageContainer();
        var layout = CreateVerticalLayout(panel);
        layout.Controls.Add(CreateTitleLabel("文件大小工具", _titleFont));
        layout.Controls.Add(CreateDescriptionLabel("功能说明：统计指定文件或文件夹大小，支持阈值过滤。\r\n使用步骤：1.选择文件/文件夹 -> 2.输入阈值（字节，可空） -> 3.判断大小 -> 4.查看日志"));

        var group = CreateGroup("路径选择");
        var inside = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        _fileSizePathLabel = new Label { Text = "未选择文件/文件夹", AutoSize = true, MaximumSize = new Size(600, 0) };
        var row = new Panel { Width = 760, Height = 40 };
        var selectFile = CreateActionButton("选择文件");
        selectFile.Location = new Point(0, 2);
        selectFile.Click += (_, _) => SelectSizeFile();
        var selectFolder = CreateActionButton("选择文件夹");
        selectFolder.Width = 130;
        selectFolder.Location = new Point(selectFile.Right + 10, 2);
        selectFolder.Click += (_, _) => SelectSizeFolder();
        _fileSizePathLabel.Location = new Point(selectFolder.Right + 10, 8);
        row.Controls.Add(selectFile);
        row.Controls.Add(selectFolder);
        row.Controls.Add(_fileSizePathLabel);
        inside.Controls.Add(row);

        var thresholdRow = new FlowLayoutPanel { Width = 760, Height = 40, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        thresholdRow.Controls.Add(new Label { Text = "大小阈值（字节）：", AutoSize = true, Font = _descFont, Margin = new Padding(0, 10, 4, 0) });
        _fileSizeThresholdBox = new TextBox { Width = 120, Font = _descFont, PlaceholderText = "整数" };
        thresholdRow.Controls.Add(_fileSizeThresholdBox);
        thresholdRow.Controls.Add(new Label { Text = "换算：1KB=1024B | 1MB=1024KB | 1GB=1024MB", AutoSize = true, Font = new Font("Microsoft YaHei UI", 9), Margin = new Padding(10, 10, 0, 0) });
        inside.Controls.Add(thresholdRow);

        group.Controls.Add(inside);
        layout.Controls.Add(group);

        var runButton = CreatePrimaryButton("判断大小");
        runButton.Click += async (_, _) => await RunFileSizeAsync(runButton);
        layout.Controls.Add(runButton);

        _fileSizeLog = CreateLogTextBox();
        layout.Controls.Add(CreateLogGroup("大小统计日志", _fileSizeLog));
        return panel;
    }

    private Panel BuildAlarmPage()
    {
        var panel = CreatePageContainer();
        var layout = CreateVerticalLayout(panel);
        layout.Controls.Add(CreateTitleLabel("智能闹钟工具", _titleFont));
        layout.Controls.Add(CreateDescriptionLabel("功能说明：设置指定时间闹钟，支持自定义提醒内容，到点弹窗并语音提醒。\r\n使用步骤：1.设置年月日时分 -> 2.输入提醒内容 -> 3.确定闹钟 -> 4.可随时终止"));

        var group = CreateGroup("闹钟设置");
        var inside = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        var now = DateTime.Now;
        _alarmYearBox = CreateNumberBox(now.Year.ToString(), 60);
        _alarmMonthBox = CreateNumberBox(now.Month.ToString(), 60);
        _alarmDayBox = CreateNumberBox(now.Day.ToString(), 60);
        _alarmHourBox = CreateNumberBox(now.Hour.ToString(), 60);
        _alarmMinuteBox = CreateNumberBox(now.Minute.ToString(), 60);

        inside.Controls.Add(CreateAlarmTimeRow());

        var contentRow = new FlowLayoutPanel { Width = 760, Height = 40, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        contentRow.Controls.Add(new Label { Text = "提醒内容：", AutoSize = true, Font = _descFont, Margin = new Padding(0, 10, 2, 0) });
        _alarmContentBox = new TextBox { Width = 550, Font = _descFont, PlaceholderText = "请输入闹钟提醒内容（如：开会、喝水、休息）" };
        contentRow.Controls.Add(_alarmContentBox);
        inside.Controls.Add(contentRow);

        group.Controls.Add(inside);
        layout.Controls.Add(group);

        var buttonRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 760, Height = 50 };
        _setAlarmButton = CreatePrimaryButton("确定闹钟");
        _setAlarmButton.Click += async (_, _) => await SetAlarmAsync();
        _stopAlarmButton = CreateDangerButton("终止闹钟");
        _stopAlarmButton.Enabled = false;
        _stopAlarmButton.Click += (_, _) => StopAlarm();
        buttonRow.Controls.Add(_setAlarmButton);
        buttonRow.Controls.Add(_stopAlarmButton);
        layout.Controls.Add(buttonRow);

        _alarmLog = CreateLogTextBox();
        layout.Controls.Add(CreateLogGroup("闹钟日志", _alarmLog));
        return panel;
    }

    private FlowLayoutPanel CreateAlarmTimeRow()
    {
        var row = new FlowLayoutPanel { Width = 760, Height = 80, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        row.Controls.AddRange(
        [
            new Label { Text = "年：", AutoSize = true, Font = _descFont, Margin = new Padding(0, 10, 0, 0) }, _alarmYearBox,
            new Label { Text = "月：", AutoSize = true, Font = _descFont, Margin = new Padding(20, 10, 0, 0) }, _alarmMonthBox,
            new Label { Text = "日：", AutoSize = true, Font = _descFont, Margin = new Padding(20, 10, 0, 0) }, _alarmDayBox,
            new Label { Text = "时：", AutoSize = true, Font = _descFont, Margin = new Padding(20, 10, 0, 0) }, _alarmHourBox,
            new Label { Text = "分：", AutoSize = true, Font = _descFont, Margin = new Padding(20, 10, 0, 0) }, _alarmMinuteBox
        ]);
        return row;
    }

    private TextBox CreateNumberBox(string text, int width)
    {
        var box = new TextBox { Text = text, Width = width, Font = _descFont };
        box.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        };
        return box;
    }

    private Panel CreatePageContainer()
    {
        return new Panel { Dock = DockStyle.Fill, BackColor = _pageBg, Padding = new Padding(30, 20, 30, 20) };
    }

    private FlowLayoutPanel CreateVerticalLayout(Control parent)
    {
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = _pageBg
        };
        parent.Controls.Add(layout);
        EnableResponsiveLayout(layout);
        return layout;
    }

    private void EnableResponsiveLayout(FlowLayoutPanel layout)
    {
        layout.SizeChanged += (_, _) => UpdateResponsiveLayout(layout);
        layout.ControlAdded += (_, _) => UpdateResponsiveLayout(layout);
        UpdateResponsiveLayout(layout);
    }

    private void UpdateResponsiveLayout(FlowLayoutPanel layout)
    {
        var pageWidth = Math.Max(420, layout.ClientSize.Width - 25);
        foreach (Control control in layout.Controls)
        {
            switch (control)
            {
                case GroupBox group:
                    group.Width = pageWidth;
                    ResizeGroupContent(group);
                    break;
                case FlowLayoutPanel flow:
                    flow.Width = pageWidth;
                    ResizeInnerFlow(flow);
                    break;
                case TextBox textBox when textBox.Multiline:
                    textBox.Width = pageWidth;
                    break;
                case Label label when label.MaximumSize.Width > 0:
                    label.MaximumSize = new Size(pageWidth, 0);
                    break;
            }
        }

        UpdateResponsiveHeights(layout);
    }

    private void UpdateResponsiveHeights(FlowLayoutPanel layout)
    {
        var logGroups = layout.Controls
            .OfType<GroupBox>()
            .Where(IsLogGroup)
            .ToList();
        if (logGroups.Count == 0)
        {
            return;
        }

        var occupiedHeight = 0;
        foreach (Control control in layout.Controls)
        {
            if (control is GroupBox group && IsLogGroup(group))
            {
                continue;
            }

            occupiedHeight += control.Height + control.Margin.Vertical;
        }

        // Keep some breathing room to avoid bottom clipping when scroll bar appears.
        var available = Math.Max(240, layout.ClientSize.Height - occupiedHeight - 40);
        var each = Math.Max(240, available / logGroups.Count);
        foreach (var logGroup in logGroups)
        {
            logGroup.Height = each;
            ResizeGroupContent(logGroup);
        }
    }

    private static bool IsLogGroup(GroupBox group)
    {
        return string.Equals(group.Tag as string, "log-group", StringComparison.Ordinal);
    }

    private void ResizeGroupContent(GroupBox group)
    {
        var contentWidth = Math.Max(320, group.ClientSize.Width - 16);
        foreach (Control child in group.Controls)
        {
            switch (child)
            {
                case FlowLayoutPanel flow:
                    flow.Width = contentWidth;
                    ResizeInnerFlow(flow);
                    break;
                case TextBox textBox when textBox.Multiline:
                    textBox.Width = contentWidth;
                    break;
            }
        }
    }

    private void ResizeInnerFlow(FlowLayoutPanel panel)
    {
        var width = Math.Max(300, panel.ClientSize.Width - 10);
        foreach (Control child in panel.Controls)
        {
            switch (child)
            {
                case FlowLayoutPanel nested:
                    nested.Width = width;
                    ResizeInnerFlow(nested);
                    break;
                case Panel row:
                    row.Width = width;
                    ResizeActionRow(row);
                    break;
                case TextBox textBox when textBox.Multiline:
                    textBox.Width = width;
                    break;
                case Label label when label.MaximumSize.Width > 0:
                    label.MaximumSize = new Size(width, 0);
                    break;
            }
        }
    }

    private static void ResizeActionRow(Panel row)
    {
        var label = row.Controls.OfType<Label>().FirstOrDefault();
        if (label is null)
        {
            return;
        }

        var rightMostButton = row.Controls.OfType<Button>().Select(btn => btn.Right).DefaultIfEmpty(0).Max();
        var x = rightMostButton > 0 ? rightMostButton + 12 : label.Left;
        label.Location = new Point(x, label.Location.Y);
        label.MaximumSize = new Size(Math.Max(120, row.ClientSize.Width - x - 8), 0);
    }

    private Label CreateTitleLabel(string text, Font font)
    {
        return new Label
        {
            Text = text,
            Font = font,
            ForeColor = Color.Black,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 10)
        };
    }

    private Label CreateDescriptionLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = _descFont,
            ForeColor = ColorTranslator.FromHtml("#333333"),
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Margin = new Padding(0, 0, 0, 15)
        };
    }

    private GroupBox CreateGroup(string title)
    {
        return new GroupBox
        {
            Text = title,
            Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
            ForeColor = Color.Black,
            Width = 900,
            Height = 160,
            MinimumSize = new Size(420, 160),
            BackColor = Color.White
        };
    }

    private Panel CreateActionRow(string buttonText, Label targetLabel, Action onClick)
    {
        var row = new Panel { Width = 760, Height = 40 };
        var button = CreateActionButton(buttonText);
        button.Location = new Point(0, 2);
        button.Click += (_, _) => onClick();
        targetLabel.Location = new Point(button.Width + 12, 8);
        row.Controls.Add(button);
        row.Controls.Add(targetLabel);
        return row;
    }

    private Button CreateActionButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            Width = 110,
            Height = 34,
            BackColor = ColorTranslator.FromHtml("#3498DB"),
            ForeColor = Color.White,
            Font = _buttonFont,
            FlatStyle = FlatStyle.Flat
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private Button CreatePrimaryButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            Width = 120,
            Height = 40,
            BackColor = ColorTranslator.FromHtml("#4CAF50"),
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 0, 10)
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private Button CreateDangerButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            Width = 120,
            Height = 40,
            BackColor = ColorTranslator.FromHtml("#F44336"),
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(10, 0, 0, 10)
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private TextBox CreateLogTextBox()
    {
        var box = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = _logFont,
            Width = 860,
            Height = 230
        };

        var menu = new ContextMenuStrip();
        var clearItem = new ToolStripMenuItem("清空日志");
        clearItem.Click += (_, _) => box.Clear();
        menu.Items.Add(clearItem);
        box.ContextMenuStrip = menu;

        return box;
    }

    private GroupBox CreateLogGroup(string title, Control content)
    {
        var group = CreateGroup(title);
        group.Tag = "log-group";
        group.Height = 280;
        group.MinimumSize = new Size(420, 240);
        group.Padding = new Padding(10, 25, 10, 10);
        content.Dock = DockStyle.Fill;
        group.Controls.Add(content);
        return group;
    }

    private void AppendLog(TextBox box, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        if (InvokeRequired)
        {
            Invoke(() => AppendLog(box, message));
            return;
        }
        box.AppendText(line + Environment.NewLine);
    }

    private void SelectVideoFile()
    {
        using var dialog = new OpenFileDialog { Filter = "视频文件|*.mp4;*.avi;*.mov;*.mkv" };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        _videoPath = dialog.FileName;
        _videoPathLabel.Text = $"已选：{_videoPath}";
        AppendLog(_videoLog, $"✅ 选择视频文件：{_videoPath}");
    }

    private void SelectVideoOutputFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        _videoOutputPath = dialog.SelectedPath;
        _videoOutputLabel.Text = $"已选：{_videoOutputPath}";
        AppendLog(_videoLog, $"✅ 选择输出文件夹：{_videoOutputPath}");
    }

    private async Task RunVideoExtractAsync(Button runButton)
    {
        if (string.IsNullOrWhiteSpace(_videoPath))
        {
            MessageBox.Show("请先选择视频文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_videoOutputPath))
        {
            MessageBox.Show("请先选择输出文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        runButton.Enabled = false;
        runButton.Text = "提取中...";
        AppendLog(_videoLog, "⏳ 正在提取帧，视频越大耗时越长，请耐心等待...");

        var args = $"video_extract --video \"{_videoPath}\" --output \"{_videoOutputPath}\"";
        var result = await _pythonBridge.RunAsync(args, msg => AppendLog(_videoLog, msg));
        runButton.Enabled = true;
        runButton.Text = "开始提取";

        AppendLog(_videoLog, result.Success ? $"🎉 {result.Message}" : $"❌ {result.Message}");
        MessageBox.Show(result.Message, result.Success ? "成功" : "失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private void SelectDedupFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        _dedupFolderPath = dialog.SelectedPath;
        _dedupFolderLabel.Text = $"已选：{_dedupFolderPath}";
        AppendLog(_dedupLog, $"✅ 选择待去重文件夹：{_dedupFolderPath}");
    }

    private async Task RunImageDedupAsync(Button runButton)
    {
        if (string.IsNullOrWhiteSpace(_dedupFolderPath))
        {
            MessageBox.Show("请先选择待去重文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        runButton.Enabled = false;
        runButton.Text = "去重中...";
        AppendLog(_dedupLog, "📌 开始图片去重扫描...");
        AppendLog(_dedupLog, $"🔧 删除重复图片功能：{(_deleteDuplicateImages ? "开启" : "关闭")}");

        var args = $"image_dedup --folder \"{_dedupFolderPath}\" --delete {(_deleteDuplicateImages ? "true" : "false")}";
        var result = await _pythonBridge.RunAsync(args, msg => AppendLog(_dedupLog, msg));
        runButton.Enabled = true;
        runButton.Text = "开始去重";

        AppendLog(_dedupLog, result.Success ? $"🎉 去重完成：{result.Message}" : $"❌ 去重失败：{result.Message}");
        MessageBox.Show(result.Message, result.Success ? "成功" : "失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private void SelectSizeFile()
    {
        using var dialog = new OpenFileDialog { Filter = "所有文件|*.*" };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        _fileSizePath = dialog.FileName;
        _fileSizePathLabel.Text = $"已选文件：{Path.GetFileName(_fileSizePath)}";
        AppendLog(_fileSizeLog, $"✅ 选择文件：{_fileSizePath}");
    }

    private void SelectSizeFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        _fileSizePath = dialog.SelectedPath;
        _fileSizePathLabel.Text = $"已选文件夹：{_fileSizePath}";
        AppendLog(_fileSizeLog, $"✅ 选择文件夹：{_fileSizePath}");
    }

    private async Task RunFileSizeAsync(Button runButton)
    {
        if (string.IsNullOrWhiteSpace(_fileSizePath))
        {
            MessageBox.Show("请先选择文件或文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var threshold = 0;
        if (!string.IsNullOrWhiteSpace(_fileSizeThresholdBox.Text) && !int.TryParse(_fileSizeThresholdBox.Text, out threshold))
        {
            MessageBox.Show("大小阈值请输入有效整数！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        runButton.Enabled = false;
        runButton.Text = "计算中...";
        AppendLog(_fileSizeLog, $"📌 开始统计文件/文件夹大小（阈值：{threshold} 字节）...");

        var args = $"file_size --path \"{_fileSizePath}\" --threshold {threshold}";
        var result = await _pythonBridge.RunAsync(args, msg => AppendLog(_fileSizeLog, msg));
        runButton.Enabled = true;
        runButton.Text = "判断大小";

        AppendLog(_fileSizeLog, result.Success ? $"🎉 统计完成：{result.Message}" : $"❌ 统计失败：{result.Message}");
        MessageBox.Show(result.Message, result.Success ? "成功" : "失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private async Task SetAlarmAsync()
    {
        if (!TryReadAlarmTime(out var alarmTime))
        {
            return;
        }

        if (alarmTime < DateTime.Now)
        {
            var pass = MessageBox.Show("设置的时间已过去，是否继续？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (pass == DialogResult.No)
            {
                return;
            }
        }

        _alarmCts?.Cancel();
        _alarmCts = new CancellationTokenSource();
        _setAlarmButton.Enabled = false;
        _stopAlarmButton.Enabled = true;

        var content = _alarmContentBox.Text.Trim();
        AppendLog(_alarmLog, $"📌 闹钟已启动，将在 {alarmTime:yyyy-MM-dd HH:mm} 触发！");
        AppendLog(_alarmLog, $"📝 语音提醒内容：{content}");

        try
        {
            while (DateTime.Now < alarmTime)
            {
                _alarmCts.Token.ThrowIfCancellationRequested();
                await Task.Delay(1000, _alarmCts.Token);
            }

            AppendLog(_alarmLog, $"⏰ 【闹钟触发】已到设置时间：{alarmTime:yyyy-MM-dd HH:mm}！");
            StartSpeechLoop(content);
            MessageBox.Show($"已到设置时间：{alarmTime:yyyy-MM-dd HH:mm}\r\n提醒内容：{(string.IsNullOrWhiteSpace(content) ? "无提醒内容" : content)}", "闹钟提醒", MessageBoxButtons.OK, MessageBoxIcon.Information);
            StopSpeechLoop();
            AppendLog(_alarmLog, "🛑 已关闭闹钟语音提醒！");
        }
        catch (OperationCanceledException)
        {
            AppendLog(_alarmLog, "🛑 已终止当前闹钟");
        }
        finally
        {
            _setAlarmButton.Enabled = true;
            _stopAlarmButton.Enabled = false;
        }
    }

    private void StopAlarm()
    {
        _alarmCts?.Cancel();
        StopSpeechLoop();
    }

    private void StartSpeechLoop(string content)
    {
        StopSpeechLoop();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }
        try
        {
            var escaped = content.Replace("\"", "\\\"");
            var psi = new ProcessStartInfo
            {
                FileName = _pythonBridge.PythonExePath,
                Arguments = $"\"{_pythonBridge.ScriptPath}\" speak_loop --text \"{escaped}\" --interval 1.0",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            _speechProcess = Process.Start(psi);
        }
        catch (Exception ex)
        {
            AppendLog(_alarmLog, $"❌ 语音提醒启动失败：{ex.Message}");
        }
    }

    private void StopSpeechLoop()
    {
        if (_speechProcess is null)
        {
            return;
        }
        try
        {
            if (!_speechProcess.HasExited)
            {
                _speechProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _speechProcess.Dispose();
            _speechProcess = null;
        }
    }

    private bool TryReadAlarmTime(out DateTime alarmTime)
    {
        alarmTime = DateTime.Now;
        if (!int.TryParse(_alarmYearBox.Text, out var y) ||
            !int.TryParse(_alarmMonthBox.Text, out var m) ||
            !int.TryParse(_alarmDayBox.Text, out var d) ||
            !int.TryParse(_alarmHourBox.Text, out var h) ||
            !int.TryParse(_alarmMinuteBox.Text, out var mi))
        {
            MessageBox.Show("请输入有效的年月日时分！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        try
        {
            alarmTime = new DateTime(y, m, d, h, mi, 0);
            return true;
        }
        catch
        {
            MessageBox.Show("输入的日期时间不合法！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }
}
