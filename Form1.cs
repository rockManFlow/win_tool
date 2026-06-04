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
    private TextBox _crowdLog = null!;
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
    private Label _crowdCurrentCountLabel = null!;
    private Button _crowdStartButton = null!;
    private Button _crowdStopButton = null!;
    private CancellationTokenSource? _crowdMonitorCts;
    private readonly Dictionary<string, int> _crowdHourStats = [];

    // 图片分类页面相关字段
    private string _photoChoiceInputPath = string.Empty;
    private string _photoChoiceOutputPath = string.Empty;
    private Label _photoChoiceInputLabel = null!;
    private Label _photoChoiceOutputLabel = null!;
    private ComboBox _photoChoiceKindCombo = null!;
    private Button _photoChoiceRefreshButton = null!;
    private TextBox _photoChoiceLog = null!;

    // 屏幕录制页面相关字段
    private string _screenRecordOutputPath = string.Empty;
    private Label _screenRecordOutputLabel = null!;
    private ComboBox _screenRecordFpsCombo = null!;
    private TextBox _screenRecordLog = null!;
    private Button _screenRecordSelectRegionButton = null!;
    private Button _screenRecordStartButton = null!;
    private Button _screenRecordStopButton = null!;
    private Label _screenRecordStatusLabel = null!;
    private Label _screenRecordTimeLabel = null!;
    private Label _screenRecordRegionLabel = null!;
    private ScreenRecorder? _screenRecorder;
    private CancellationTokenSource? _screenRecordTimerCts;
    private DateTime _screenRecordStartTime;
    private Rectangle _screenRecordRegion;
    private RecordingBorder? _recordingBorder;

    public Form1()
    {
        InitializeComponent();
        BuildUi();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _alarmCts?.Cancel();
        _crowdMonitorCts?.Cancel();
        _screenRecordTimerCts?.Cancel();
        _screenRecorder?.Dispose();
        _recordingBorder?.Close();
        _recordingBorder?.Dispose();
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
        ("屏幕录制", "screen_record"),
        ("其他视频工具", "video_other")
    );
    layout.Controls.Add(_submenuVideo);

        var image = CreateMainMenuButton("图片");
        image.Click += (_, _) => ToggleSubmenu(_submenuImage, image);
        layout.Controls.Add(image);

        _submenuImage = CreateSubmenuPanel(
            ("图片去重", "image_dedup"),
            ("图片分类", "photo_choice"),
            ("图片处理", "image_process")
        );
        layout.Controls.Add(_submenuImage);

        var other = CreateMainMenuButton("其他工具");
        other.Click += (_, _) => ToggleSubmenu(_submenuOther, other);
        layout.Controls.Add(other);

        _submenuOther = CreateSubmenuPanel(
            ("文件大小工具", "file_size"),
            ("智能闹钟工具", "alarm"),
            ("实时人流统计", "crowd_monitor")
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
        AddPage("screen_record", BuildScreenRecordPage());
        AddPage("image_dedup", BuildImageDedupPage());
        AddPage("photo_choice", BuildPhotoChoicePage());
        AddPage("file_size", BuildFileSizePage());
        AddPage("alarm", BuildAlarmPage());
        AddPage("crowd_monitor", BuildCrowdMonitorPage());
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

    private Panel BuildScreenRecordPage()
    {
        var panel = CreatePageContainer();
        var layout = CreateVerticalLayout(panel);
        layout.Controls.Add(CreateTitleLabel("屏幕录制工具", _titleFont));
        layout.Controls.Add(CreateDescriptionLabel("功能说明：录制屏幕指定区域，输出 MP4 格式视频。\r\n使用步骤：1.选择输出文件夹 -> 2.选择录制区域 -> 3.设置帧率 -> 4.点击开始录制 -> 5.点击停止录制"));

        // 录制设置组
        var settingsGroup = CreateGroup("录制设置");
        settingsGroup.Height = 180;
        var settingsLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        // 输出文件夹选择
        _screenRecordOutputLabel = new Label { Text = "未选择输出文件夹", AutoSize = true, MaximumSize = new Size(600, 0) };
        settingsLayout.Controls.Add(CreateActionRow("选择输出文件夹", _screenRecordOutputLabel, SelectScreenRecordOutputFolder));

        // 录制区域选择
        var regionRow = new Panel { Width = 760, Height = 40 };
        _screenRecordSelectRegionButton = CreateActionButton("选择录制区域");
        _screenRecordSelectRegionButton.Width = 130;
        _screenRecordSelectRegionButton.Location = new Point(0, 2);
        _screenRecordSelectRegionButton.Click += (_, _) => SelectScreenRecordRegion();
        _screenRecordRegionLabel = new Label 
        { 
            Text = "未选择录制区域", 
            AutoSize = true, 
            MaximumSize = new Size(600, 0),
            Location = new Point(142, 8)
        };
        regionRow.Controls.Add(_screenRecordSelectRegionButton);
        regionRow.Controls.Add(_screenRecordRegionLabel);
        settingsLayout.Controls.Add(regionRow);

        // 帧率选择
        var fpsRow = new FlowLayoutPanel { Width = 760, Height = 40, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        fpsRow.Controls.Add(new Label { Text = "录制帧率：", AutoSize = true, Font = _descFont, Margin = new Padding(0, 10, 4, 0) });
        _screenRecordFpsCombo = new ComboBox
        {
            Width = 100,
            Font = _descFont,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _screenRecordFpsCombo.Items.AddRange(["15", "24", "30", "60"]);
        _screenRecordFpsCombo.SelectedIndex = 2; // 默认30fps
        fpsRow.Controls.Add(_screenRecordFpsCombo);
        fpsRow.Controls.Add(new Label { Text = "fps（帧率越高，文件越大）", AutoSize = true, Font = new Font("Microsoft YaHei UI", 9), ForeColor = Color.Gray, Margin = new Padding(10, 10, 0, 0) });
        settingsLayout.Controls.Add(fpsRow);

        settingsGroup.Controls.Add(settingsLayout);
        layout.Controls.Add(settingsGroup);

        // 录制状态组
        var statusGroup = CreateGroup("录制状态");
        statusGroup.Height = 100;
        var statusLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var statusTitleLabel = new Label
        {
            Text = "状态：",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold),
            Margin = new Padding(10, 15, 0, 0)
        };
        _screenRecordStatusLabel = new Label
        {
            Text = "未开始",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold),
            ForeColor = Color.Gray,
            Margin = new Padding(4, 15, 0, 0)
        };
        var timeTitleLabel = new Label
        {
            Text = "      录制时长：",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold),
            Margin = new Padding(20, 15, 0, 0)
        };
        _screenRecordTimeLabel = new Label
        {
            Text = "00:00:00",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold),
            ForeColor = Color.Black,
            Margin = new Padding(4, 13, 0, 0)
        };
        statusLayout.Controls.Add(statusTitleLabel);
        statusLayout.Controls.Add(_screenRecordStatusLabel);
        statusLayout.Controls.Add(timeTitleLabel);
        statusLayout.Controls.Add(_screenRecordTimeLabel);
        statusGroup.Controls.Add(statusLayout);
        layout.Controls.Add(statusGroup);

        // 按钮行
        var buttonRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 760, Height = 50 };
        _screenRecordStartButton = CreatePrimaryButton("开始录制");
        _screenRecordStartButton.Click += async (_, _) => await StartScreenRecordAsync();
        _screenRecordStopButton = CreateDangerButton("停止录制");
        _screenRecordStopButton.Enabled = false;
        _screenRecordStopButton.Click += async (_, _) => await StopScreenRecordAsync();
        buttonRow.Controls.Add(_screenRecordStartButton);
        buttonRow.Controls.Add(_screenRecordStopButton);
        layout.Controls.Add(buttonRow);

        // 日志区域
        _screenRecordLog = CreateLogTextBox();
        layout.Controls.Add(CreateLogGroup("录制日志", _screenRecordLog));

        return panel;
    }

    private void SelectScreenRecordRegion()
    {
        // 关闭之前的边框窗口
        _recordingBorder?.Close();
        _recordingBorder?.Dispose();
        _recordingBorder = null;

        // 最小化主窗口以便选择区域
        var previousState = WindowState;
        WindowState = FormWindowState.Minimized;
        Thread.Sleep(300); // 等待窗口最小化动画完成

        try
        {
            using var selector = new RegionSelector();
            var result = selector.ShowDialog();

            if (result == DialogResult.OK && selector.HasSelection)
            {
                _screenRecordRegion = selector.SelectedRegion;
                _screenRecordRegionLabel.Text = $"已选择: {_screenRecordRegion.Width} x {_screenRecordRegion.Height} (位置: {_screenRecordRegion.X}, {_screenRecordRegion.Y})";
                _screenRecordRegionLabel.ForeColor = Color.Green;
                AppendLog(_screenRecordLog, $"已选择录制区域: {_screenRecordRegion.Width}x{_screenRecordRegion.Height}");

                // 显示录制边框窗口
                _recordingBorder = new RecordingBorder(_screenRecordRegion);
                _recordingBorder.OnRegionChanged += region =>
                {
                    _screenRecordRegion = region;
                    BeginInvoke(() =>
                    {
                        _screenRecordRegionLabel.Text = $"已选择: {region.Width} x {region.Height} (位置: {region.X}, {region.Y})";
                        AppendLog(_screenRecordLog, $"录制区域已调整: {region.Width}x{region.Height}");
                    });
                };
                _recordingBorder.OnCloseButtonClicked += () =>
                {
                    BeginInvoke(() =>
                    {
                        _recordingBorder = null;
                        _screenRecordRegion = Rectangle.Empty;
                        _screenRecordRegionLabel.Text = "未选择录制区域";
                        _screenRecordRegionLabel.ForeColor = Color.Black;
                        AppendLog(_screenRecordLog, "录制边框已关闭");
                    });
                };
                _recordingBorder.Show();
            }
        }
        finally
        {
            // 恢复窗口状态
            WindowState = previousState;
            Activate();
        }
    }

    private void LoadScreenRecordMonitors()
    {
        // 此方法已不再需要，保留空实现以兼容
    }

    private void SelectScreenRecordOutputFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        _screenRecordOutputPath = dialog.SelectedPath;
        _screenRecordOutputLabel.Text = $"已选：{_screenRecordOutputPath}";
        AppendLog(_screenRecordLog, $"选择输出文件夹：{_screenRecordOutputPath}");
    }

    private Task StartScreenRecordAsync()
    {
        if (string.IsNullOrWhiteSpace(_screenRecordOutputPath))
        {
            MessageBox.Show("请先选择输出文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return Task.CompletedTask;
        }

        if (_screenRecordRegion.Width <= 0 || _screenRecordRegion.Height <= 0)
        {
            MessageBox.Show("请先选择录制区域！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return Task.CompletedTask;
        }

        try
        {
            _screenRecordStartButton.Enabled = false;
            _screenRecordSelectRegionButton.Enabled = false;
            _screenRecordStopButton.Enabled = true;
            _screenRecordStatusLabel.Text = "录制中";
            _screenRecordStatusLabel.ForeColor = Color.Red;

            // 生成输出文件名
            var fileName = $"screen_record_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            var outputPath = Path.Combine(_screenRecordOutputPath, fileName);
            var fps = int.Parse(_screenRecordFpsCombo.SelectedItem?.ToString() ?? "30");

            AppendLog(_screenRecordLog, "正在初始化录屏...");

            // 创建并初始化录屏器
            _screenRecorder = new ScreenRecorder();
            _screenRecorder.OnLog += msg => AppendLog(_screenRecordLog, msg);
            _screenRecorder.OnError += ex => 
            {
                AppendLog(_screenRecordLog, $"录制错误: {ex.Message}");
                Invoke(() => 
                {
                    _screenRecordStatusLabel.Text = "错误";
                    _screenRecordStatusLabel.ForeColor = Color.Red;
                });
            };

            // 设置录制区域（从边框窗口获取最新区域）
            if (_recordingBorder != null)
            {
                _screenRecordRegion = _recordingBorder.CaptureRegion;
            }
            _screenRecorder.SetCaptureRegion(_screenRecordRegion);

            // 禁用边框调整（录制时不能改变区域）
            _recordingBorder?.SetAllowResize(false);

            // 开始录制
            _screenRecorder.StartRecording(outputPath, fps);
            _screenRecordStartTime = DateTime.Now;

            // 启动计时器更新录制时长
            _screenRecordTimerCts = new CancellationTokenSource();
            _ = UpdateRecordTimeAsync(_screenRecordTimerCts.Token);

            AppendLog(_screenRecordLog, $"开始录制，输出文件：{outputPath}");
        }
        catch (Exception ex)
        {
            _screenRecordStartButton.Enabled = true;
            _screenRecordSelectRegionButton.Enabled = true;
            _screenRecordStopButton.Enabled = false;
            _screenRecordStatusLabel.Text = "错误";
            _screenRecordStatusLabel.ForeColor = Color.Red;
            AppendLog(_screenRecordLog, $"启动录制失败：{ex.Message}");
            MessageBox.Show($"启动录制失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        return Task.CompletedTask;
    }

    private async Task StopScreenRecordAsync()
    {
        try
        {
            _screenRecordStopButton.Enabled = false;
            _screenRecordStatusLabel.Text = "正在停止...";
            _screenRecordStatusLabel.ForeColor = Color.Orange;

            _screenRecordTimerCts?.Cancel();

            if (_screenRecorder != null)
            {
                await _screenRecorder.StopRecordingAsync();
                _screenRecorder.Dispose();
                _screenRecorder = null;
            }

            // 重新启用边框调整
            _recordingBorder?.SetAllowResize(true);

            _screenRecordStatusLabel.Text = "已停止";
            _screenRecordStatusLabel.ForeColor = Color.Green;
            _screenRecordStartButton.Enabled = true;
            _screenRecordSelectRegionButton.Enabled = true;

            AppendLog(_screenRecordLog, "录制已停止，视频文件已保存");
            MessageBox.Show("录制完成！视频文件已保存到输出文件夹。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _screenRecordStartButton.Enabled = true;
            _screenRecordSelectRegionButton.Enabled = true;
            _recordingBorder?.SetAllowResize(true);
            _screenRecordStatusLabel.Text = "错误";
            _screenRecordStatusLabel.ForeColor = Color.Red;
            AppendLog(_screenRecordLog, $"停止录制失败：{ex.Message}");
            MessageBox.Show($"停止录制失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task UpdateRecordTimeAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var elapsed = DateTime.Now - _screenRecordStartTime;
                var timeStr = elapsed.ToString(@"hh\:mm\:ss");
                if (InvokeRequired)
                {
                    Invoke(() => _screenRecordTimeLabel.Text = timeStr);
                }
                else
                {
                    _screenRecordTimeLabel.Text = timeStr;
                }
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
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

    private Panel BuildPhotoChoicePage()
    {
        var panel = CreatePageContainer();
        var layout = CreateVerticalLayout(panel);
        layout.Controls.Add(CreateTitleLabel("图片分类", _titleFont));
        layout.Controls.Add(CreateDescriptionLabel("功能说明：支持选择指定文件夹中的图片，根据图片内容来筛选不同的图片到不同的文件夹下。\r\n使用步骤：1.选择输入文件夹 -> 2.选择输出文件夹 -> 3.选择分类类型 -> 4.开始执行 -> 5.查看日志"));

        var group = CreateGroup("分类设置");
        group.Height = 180;
        var inside = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        _photoChoiceInputLabel = new Label { Text = "未选择文件夹", AutoSize = true, MaximumSize = new Size(600, 0) };
        inside.Controls.Add(CreateActionRow("input", _photoChoiceInputLabel, SelectPhotoChoiceInputFolder, "选择需要处理的图片文件夹"));

        _photoChoiceOutputLabel = new Label { Text = "未选择文件夹", AutoSize = true, MaximumSize = new Size(600, 0) };
        inside.Controls.Add(CreateActionRow("output", _photoChoiceOutputLabel, SelectPhotoChoiceOutputFolder, "选择处理后图片保存到文件夹"));

        var kindRow = new FlowLayoutPanel { Width = 760, Height = 40, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        kindRow.Controls.Add(new Label { Text = "类型：", AutoSize = true, Font = _descFont, Margin = new Padding(0, 10, 4, 0) });
        _photoChoiceKindCombo = new ComboBox
        {
            Width = 200,
            Font = _descFont,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        kindRow.Controls.Add(_photoChoiceKindCombo);
        
        // 刷新按钮
        _photoChoiceRefreshButton = new Button
        {
            Text = "刷新",
            Width = 60,
            Height = 28,
            Font = _descFont,
            BackColor = ColorTranslator.FromHtml("#3498DB"),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(10, 6, 0, 0),
            Cursor = Cursors.Hand
        };
        _photoChoiceRefreshButton.FlatAppearance.BorderSize = 0;
        _photoChoiceRefreshButton.Click += async (_, _) => await RefreshPhotoChoiceKindsAsync();
        kindRow.Controls.Add(_photoChoiceRefreshButton);
        
        // 加载状态提示
        var kindStatusLabel = new Label
        {
            Text = "",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.Gray,
            Margin = new Padding(10, 10, 0, 0),
            Name = "kindStatusLabel"
        };
        kindRow.Controls.Add(kindStatusLabel);
        
        inside.Controls.Add(kindRow);

        group.Controls.Add(inside);
        layout.Controls.Add(group);

        var runButton = CreatePrimaryButton("开始执行");
        runButton.Click += async (_, _) => await RunPhotoChoiceAsync(runButton);
        layout.Controls.Add(runButton);

        _photoChoiceLog = CreateLogTextBox();
        layout.Controls.Add(CreateLogGroup("分类日志", _photoChoiceLog));

        // 异步加载类别列表
        _ = LoadPhotoChoiceKindsAsync();

        return panel;
    }

    private Panel CreateActionRow(string buttonText, Label targetLabel, Action onClick, string description)
    {
        var row = new Panel { Width = 760, Height = 40 };
        var button = CreateActionButton(buttonText);
        button.Location = new Point(0, 2);
        button.Click += (_, _) => onClick();
        var descLabel = new Label
        {
            Text = description,
            AutoSize = true,
            Font = _descFont,
            ForeColor = ColorTranslator.FromHtml("#666666"),
            Location = new Point(button.Width + 12, 8)
        };
        targetLabel.Location = new Point(button.Width + 12 + descLabel.PreferredWidth + 12, 8);
        row.Controls.Add(button);
        row.Controls.Add(descLabel);
        row.Controls.Add(targetLabel);
        return row;
    }

    private void SelectPhotoChoiceInputFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        _photoChoiceInputPath = dialog.SelectedPath;
        _photoChoiceInputLabel.Text = $"已选：{_photoChoiceInputPath}";
        AppendLog(_photoChoiceLog, $"✅ 选择输入文件夹：{_photoChoiceInputPath}");
    }

    private void SelectPhotoChoiceOutputFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        _photoChoiceOutputPath = dialog.SelectedPath;
        _photoChoiceOutputLabel.Text = $"已选：{_photoChoiceOutputPath}";
        AppendLog(_photoChoiceLog, $"✅ 选择输出文件夹：{_photoChoiceOutputPath}");
    }

    private async Task LoadPhotoChoiceKindsAsync()
    {
        await LoadPhotoChoiceKindsInternalAsync(isManualRefresh: false);
    }

    private async Task RefreshPhotoChoiceKindsAsync()
    {
        await LoadPhotoChoiceKindsInternalAsync(isManualRefresh: true);
    }

    private async Task LoadPhotoChoiceKindsInternalAsync(bool isManualRefresh)
    {
        // 禁用刷新按钮，显示加载状态
        if (_photoChoiceRefreshButton != null)
        {
            _photoChoiceRefreshButton.Enabled = false;
            _photoChoiceRefreshButton.Text = "加载中";
        }

        var actionText = isManualRefresh ? "刷新" : "加载";
        AppendLog(_photoChoiceLog, $"📌 正在{actionText}分类类型列表...");

        try
        {
            var result = await _pythonBridge.RunAsync("get_photo_kinds", msg => AppendLog(_photoChoiceLog, msg));

            if (result.Success && result.RawJson.HasValue && result.RawJson.Value.TryGetProperty("kinds", out var kindsElement))
            {
                var kinds = new List<string>();
                foreach (var item in kindsElement.EnumerateArray())
                {
                    var kind = item.GetString();
                    if (!string.IsNullOrEmpty(kind))
                    {
                        kinds.Add(kind);
                    }
                }

                if (InvokeRequired)
                {
                    Invoke(() => PopulateKindCombo(kinds, isManualRefresh));
                }
                else
                {
                    PopulateKindCombo(kinds, isManualRefresh);
                }
                AppendLog(_photoChoiceLog, $"✅ 已{actionText} {kinds.Count} 个分类类型");
            }
            else
            {
                AppendLog(_photoChoiceLog, $"❌ {actionText}分类类型失败：{result.Message}");
                if (isManualRefresh)
                {
                    MessageBox.Show($"刷新失败：{result.Message}\n\n请检查 Python 环境是否正常。", "刷新失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog(_photoChoiceLog, $"❌ {actionText}分类类型异常：{ex.Message}");
            if (isManualRefresh)
            {
                MessageBox.Show($"刷新异常：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            // 恢复刷新按钮状态
            if (_photoChoiceRefreshButton != null)
            {
                if (InvokeRequired)
                {
                    Invoke(() =>
                    {
                        _photoChoiceRefreshButton.Enabled = true;
                        _photoChoiceRefreshButton.Text = "刷新";
                    });
                }
                else
                {
                    _photoChoiceRefreshButton.Enabled = true;
                    _photoChoiceRefreshButton.Text = "刷新";
                }
            }
        }
    }

    private void PopulateKindCombo(List<string> kinds, bool showMessage = false)
    {
        _photoChoiceKindCombo.Items.Clear();
        foreach (var kind in kinds)
        {
            _photoChoiceKindCombo.Items.Add(kind);
        }
        if (_photoChoiceKindCombo.Items.Count > 0)
        {
            _photoChoiceKindCombo.SelectedIndex = 0;
        }
    }

    private async Task RunPhotoChoiceAsync(Button runButton)
    {
        if (string.IsNullOrWhiteSpace(_photoChoiceInputPath))
        {
            MessageBox.Show("请先选择输入文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_photoChoiceOutputPath))
        {
            MessageBox.Show("请先选择输出文件夹！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_photoChoiceKindCombo.SelectedItem == null)
        {
            MessageBox.Show("请先选择分类类型！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedKind = _photoChoiceKindCombo.SelectedItem.ToString();
        runButton.Enabled = false;
        runButton.Text = "执行中...";
        AppendLog(_photoChoiceLog, "⏳ 正在执行图片分类，请耐心等待...");

        var args = $"photo_choice --input \"{_photoChoiceInputPath}\" --output \"{_photoChoiceOutputPath}\" --class \"{selectedKind}\"";
        var result = await _pythonBridge.RunAsync(args, msg => AppendLog(_photoChoiceLog, msg));
        
        runButton.Enabled = true;
        runButton.Text = "开始执行";

        AppendLog(_photoChoiceLog, result.Success ? $"🎉 {result.Message}" : $"❌ {result.Message}");
        MessageBox.Show(result.Message, result.Success ? "完成" : "失败", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
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

    private Panel BuildCrowdMonitorPage()
    {
        var panel = CreatePageContainer();
        var layout = CreateVerticalLayout(panel);
        layout.Controls.Add(CreateTitleLabel("实时人流统计", _titleFont));
        layout.Controls.Add(CreateDescriptionLabel("调用本机摄像头，实时统计人数\r\n请保证摄像头是可用的，会按小时把统计人数数据，保存到当前项目所在路径data/monitor.txt中"));

        var statusGroup = CreateGroup("实时状态");
        statusGroup.Height = 120;
        var statusLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var titleLabel = new Label
        {
            Text = "当前人数：",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold),
            Margin = new Padding(10, 20, 0, 0)
        };
        _crowdCurrentCountLabel = new Label
        {
            Text = "0",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 24, FontStyle.Bold),
            ForeColor = Color.Green,
            Margin = new Padding(8, 8, 0, 0)
        };
        statusLayout.Controls.Add(titleLabel);
        statusLayout.Controls.Add(_crowdCurrentCountLabel);
        statusGroup.Controls.Add(statusLayout);
        layout.Controls.Add(statusGroup);

        var buttonRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 760, Height = 50 };
        _crowdStartButton = CreatePrimaryButton("开始统计");
        _crowdStartButton.Click += async (_, _) => await StartCrowdMonitorAsync();
        _crowdStopButton = CreateDangerButton("停止统计");
        _crowdStopButton.Enabled = false;
        _crowdStopButton.Click += (_, _) => StopCrowdMonitor();
        buttonRow.Controls.Add(_crowdStartButton);
        buttonRow.Controls.Add(_crowdStopButton);
        layout.Controls.Add(buttonRow);

        _crowdLog = CreateLogTextBox();
        layout.Controls.Add(CreateLogGroup("统计日志", _crowdLog));
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

    private async Task StartCrowdMonitorAsync()
    {
        var probe = await _pythonBridge.RunAsync("camera_probe --camera 0", msg => AppendLog(_crowdLog, msg));
        if (!probe.Success)
        {
            AppendLog(_crowdLog, $"❌ 摄像头校验失败：{probe.Message}");
            MessageBox.Show($"摄像头不可用：{probe.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _crowdMonitorCts?.Cancel();
        _crowdMonitorCts = new CancellationTokenSource();
        _crowdHourStats.Clear();
        _crowdStartButton.Enabled = false;
        _crowdStopButton.Enabled = true;
        AppendLog(_crowdLog, "📷 摄像头可用，已开始实时统计，每10秒刷新一次。");

        try
        {
            while (!_crowdMonitorCts.Token.IsCancellationRequested)
            {
                var args = "crowd_count --camera 0";
                var result = await _pythonBridge.RunAsync(args, msg => AppendLog(_crowdLog, msg), _crowdMonitorCts.Token);
                if (result.Success && int.TryParse(result.Message, out var count))
                {
                    _crowdCurrentCountLabel.Text = count.ToString();
                    var hourKey = DateTime.Now.ToString("yyyyMMddHH");
                    _crowdHourStats[hourKey] = _crowdHourStats.GetValueOrDefault(hourKey) + count;
                    AppendLog(_crowdLog, $"✅ 当前人数：{count}");
                }
                else
                {
                    AppendLog(_crowdLog, $"❌ 统计失败：{result.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), _crowdMonitorCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(_crowdLog, "🛑 已停止实时统计。");
        }
        finally
        {
            _crowdStartButton.Enabled = true;
            _crowdStopButton.Enabled = false;
        }
    }

    private void StopCrowdMonitor()
    {
        _crowdMonitorCts?.Cancel();
        SaveCrowdMonitorData();
    }

    private void SaveCrowdMonitorData()
    {
        try
        {
            var dataPath = GetMonitorDataPath();
            var dir = Path.GetDirectoryName(dataPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var merged = new Dictionary<string, int>(StringComparer.Ordinal);
            if (File.Exists(dataPath))
            {
                foreach (var line in File.ReadAllLines(dataPath))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && int.TryParse(parts[1], out var value))
                    {
                        merged[parts[0]] = value;
                    }
                }
            }

            foreach (var pair in _crowdHourStats)
            {
                merged[pair.Key] = merged.GetValueOrDefault(pair.Key) + pair.Value;
            }

            if (merged.Count == 0)
            {
                merged[DateTime.Now.ToString("yyyyMMddHH")] = 0;
            }

            var lines = merged
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key} {x.Value}")
                .ToArray();
            File.WriteAllLines(dataPath, lines);
            AppendLog(_crowdLog, $"💾 已写入统计数据：{dataPath}");
        }
        catch (Exception ex)
        {
            AppendLog(_crowdLog, $"❌ 写入 monitor.txt 失败：{ex.Message}");
        }
        finally
        {
            _crowdHourStats.Clear();
        }
    }

    private static string GetMonitorDataPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir is not null)
        {
            var csproj = dir.GetFiles("*.csproj").FirstOrDefault();
            if (csproj is not null)
            {
                return Path.Combine(dir.FullName, "data", "monitor.txt");
            }
            dir = dir.Parent;
        }
        return Path.Combine(baseDir, "data", "monitor.txt");
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
