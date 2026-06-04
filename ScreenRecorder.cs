using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WinTool;

/// <summary>
/// 屏幕录制器 - 支持自定义区域录制
/// </summary>
public sealed class ScreenRecorder : IDisposable
{
    // Win32 API 声明
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    private const uint SRCCOPY = 0x00CC0020;

    private Rectangle _captureRegion;
    private bool _isRecording;
    private CancellationTokenSource? _cts;
    private Task? _recordTask;
    private string _outputPath = string.Empty;
    private int _frameRate = 30;
    private Process? _ffmpegProcess;

    // FFmpeg 路径（优先使用程序目录下的 ffmpeg.exe）
    private static readonly string FFmpegLocalPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
    private static readonly string FFmpegSubDirPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");

    public event Action<string>? OnLog;
    public event Action<Exception>? OnError;

    public bool IsRecording => _isRecording;
    public Rectangle CaptureRegion => _captureRegion;

    /// <summary>
    /// 设置录制区域
    /// </summary>
    public void SetCaptureRegion(Rectangle region)
    {
        // 确保宽高是偶数（FFmpeg 要求）
        var width = region.Width % 2 == 0 ? region.Width : region.Width - 1;
        var height = region.Height % 2 == 0 ? region.Height : region.Height - 1;
        
        _captureRegion = new Rectangle(region.X, region.Y, width, height);
        OnLog?.Invoke($"录制区域已设置: {_captureRegion.X},{_captureRegion.Y} - {_captureRegion.Width}x{_captureRegion.Height}");
    }

    /// <summary>
    /// 开始录制
    /// </summary>
    public void StartRecording(string outputPath, int frameRate = 30)
    {
        if (_isRecording)
        {
            OnLog?.Invoke("已经在录制中");
            return;
        }

        if (_captureRegion.Width <= 0 || _captureRegion.Height <= 0)
        {
            throw new InvalidOperationException("请先设置有效的录制区域");
        }

        _outputPath = outputPath;
        _frameRate = frameRate;
        _isRecording = true;
        _cts = new CancellationTokenSource();

        // 确保 FFmpeg 可用
        EnsureFFmpegAvailable();

        // 启动 FFmpeg 进程进行视频编码
        StartFFmpegProcess();

        // 启动录制任务
        _recordTask = Task.Run(() => RecordLoop(_cts.Token), _cts.Token);
        OnLog?.Invoke($"开始录制，输出路径: {outputPath}，帧率: {frameRate}fps");
    }

    /// <summary>
    /// 停止录制
    /// </summary>
    public async Task StopRecordingAsync()
    {
        if (!_isRecording)
        {
            return;
        }

        _isRecording = false;
        _cts?.Cancel();

        if (_recordTask != null)
        {
            try
            {
                await _recordTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        StopFFmpegProcess();
        OnLog?.Invoke("录制已停止");
    }

    /// <summary>
    /// 确保 FFmpeg 可用
    /// </summary>
    private void EnsureFFmpegAvailable()
    {
        var ffmpegPath = GetFFmpegPath();
        if (!string.IsNullOrEmpty(ffmpegPath))
        {
            OnLog?.Invoke($"使用 FFmpeg: {ffmpegPath}");
            return;
        }

        throw new FileNotFoundException(
            "未找到 FFmpeg。请将 ffmpeg.exe 放到程序目录下。\n" +
            "下载地址: https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip");
    }

    private string GetFFmpegPath()
    {
        // 1. 优先检查程序目录下的 ffmpeg.exe
        if (File.Exists(FFmpegLocalPath))
        {
            return FFmpegLocalPath;
        }

        // 2. 检查程序目录下的 ffmpeg 子文件夹
        if (File.Exists(FFmpegSubDirPath))
        {
            return FFmpegSubDirPath;
        }

        // 3. 检查系统 PATH
        var systemFFmpeg = FindFFmpegInPath();
        if (systemFFmpeg != null)
        {
            return systemFFmpeg;
        }

        throw new FileNotFoundException("未找到 FFmpeg");
    }

    private static string? FindFFmpegInPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var path in pathEnv.Split(Path.PathSeparator))
        {
            try
            {
                var fullPath = Path.Combine(path.Trim(), "ffmpeg.exe");
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // 忽略无效路径
            }
        }
        return null;
    }

    private void StartFFmpegProcess()
    {
        var ffmpegPath = GetFFmpegPath();
        var width = _captureRegion.Width;
        var height = _captureRegion.Height;

        // 使用 MPEG-4 编码器（所有 FFmpeg 版本都内置支持，无需额外编解码器）
        // -c:v mpeg4 是内置编码器，兼容性最好
        // 如果系统支持，也可以尝试 h264_mf (Windows Media Foundation H.264)
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-y -f rawvideo -pix_fmt bgra -s {width}x{height} -r {_frameRate} -i - " +
                        $"-c:v mpeg4 -q:v 5 -pix_fmt yuv420p \"{_outputPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };

        _ffmpegProcess = Process.Start(psi);
        if (_ffmpegProcess == null)
        {
            throw new Exception("无法启动 FFmpeg 进程");
        }

        // 异步读取 FFmpeg 错误输出
        Task.Run(async () =>
        {
            try
            {
                while (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                {
                    var line = await _ffmpegProcess.StandardError.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line))
                    {
                        if (line.Contains("frame=") || line.Contains("Error") || line.Contains("error"))
                        {
                            OnLog?.Invoke($"[FFmpeg] {line}");
                        }
                    }
                }
            }
            catch
            {
                // 忽略读取错误
            }
        });

        OnLog?.Invoke("FFmpeg 进程已启动");
    }

    private void StopFFmpegProcess()
    {
        if (_ffmpegProcess == null)
        {
            return;
        }

        try
        {
            _ffmpegProcess.StandardInput.Close();

            if (!_ffmpegProcess.WaitForExit(15000))
            {
                _ffmpegProcess.Kill();
                OnLog?.Invoke("FFmpeg 进程被强制终止");
            }
            else
            {
                OnLog?.Invoke("FFmpeg 编码完成");
            }
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"停止 FFmpeg 时出错: {ex.Message}");
        }
        finally
        {
            _ffmpegProcess.Dispose();
            _ffmpegProcess = null;
        }
    }

    private void RecordLoop(CancellationToken token)
    {
        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _frameRate);
        var stopwatch = Stopwatch.StartNew();
        var frameCount = 0;
        var lastFrameTime = TimeSpan.Zero;

        var bufferSize = _captureRegion.Width * _captureRegion.Height * 4;
        var buffer = new byte[bufferSize];

        while (!token.IsCancellationRequested && _isRecording)
        {
            try
            {
                var elapsed = stopwatch.Elapsed;
                var timeSinceLastFrame = elapsed - lastFrameTime;

                if (timeSinceLastFrame < frameInterval)
                {
                    var sleepTime = (int)(frameInterval - timeSinceLastFrame).TotalMilliseconds;
                    if (sleepTime > 0)
                    {
                        Thread.Sleep(Math.Min(sleepTime, 10));
                    }
                    continue;
                }

                lastFrameTime = elapsed;
                CaptureAndWriteFrame(buffer);
                frameCount++;

                if (frameCount % (_frameRate * 5) == 0)
                {
                    OnLog?.Invoke($"已录制 {frameCount / _frameRate} 秒");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                break;
            }
        }

        OnLog?.Invoke($"录制循环结束，共录制 {frameCount} 帧");
    }

    private void CaptureAndWriteFrame(byte[] buffer)
    {
        if (_ffmpegProcess == null || _ffmpegProcess.HasExited)
        {
            return;
        }

        var width = _captureRegion.Width;
        var height = _captureRegion.Height;
        var left = _captureRegion.Left;
        var top = _captureRegion.Top;

        var hdcScreen = GetDC(IntPtr.Zero);
        var hdcMem = CreateCompatibleDC(hdcScreen);
        var hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
        var hOld = SelectObject(hdcMem, hBitmap);

        BitBlt(hdcMem, 0, 0, width, height, hdcScreen, left, top, SRCCOPY);

        using (var bitmap = Image.FromHbitmap(hBitmap))
        {
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                var stride = bitmapData.Stride;
                var expectedStride = width * 4;

                if (stride == expectedStride)
                {
                    Marshal.Copy(bitmapData.Scan0, buffer, 0, buffer.Length);
                }
                else
                {
                    for (var y = 0; y < height; y++)
                    {
                        var srcPtr = IntPtr.Add(bitmapData.Scan0, y * stride);
                        Marshal.Copy(srcPtr, buffer, y * expectedStride, expectedStride);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        SelectObject(hdcMem, hOld);
        DeleteObject(hBitmap);
        DeleteDC(hdcMem);
        ReleaseDC(IntPtr.Zero, hdcScreen);

        try
        {
            _ffmpegProcess.StandardInput.BaseStream.Write(buffer, 0, buffer.Length);
            _ffmpegProcess.StandardInput.BaseStream.Flush();
        }
        catch (IOException)
        {
            // FFmpeg 进程可能已关闭
        }
    }

    /// <summary>
    /// 获取可用的显示器列表
    /// </summary>
    public static List<string> GetAvailableMonitors()
    {
        var monitors = new List<string>();
        try
        {
            var screens = Screen.AllScreens;
            for (var i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                var primary = screen.Primary ? " (主显示器)" : "";
                monitors.Add($"显示器 {i + 1}: {screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height}){primary}");
            }
        }
        catch
        {
            monitors.Add("主显示器");
        }

        if (monitors.Count == 0)
        {
            monitors.Add("主显示器");
        }

        return monitors;
    }

    /// <summary>
    /// 检查 FFmpeg 是否可用
    /// </summary>
    public static bool IsFFmpegAvailable()
    {
        // 检查程序目录
        if (File.Exists(FFmpegLocalPath))
        {
            return true;
        }

        // 检查 ffmpeg 子目录
        if (File.Exists(FFmpegSubDirPath))
        {
            return true;
        }

        // 检查系统 PATH
        return FindFFmpegInPath() != null;
    }

    public void Dispose()
    {
        StopRecordingAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
    }
}
