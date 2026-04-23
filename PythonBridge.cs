using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace WinTool;

public sealed class PythonBridge
{
    private const string ResultPrefix = "RESULT_JSON:";

    public string PythonExePath { get; }
    public string ScriptPath { get; }

    public PythonBridge()
    {
        var exeBase = AppContext.BaseDirectory;
        PythonExePath = ResolvePythonExe(exeBase);
        ScriptPath = ResolveBridgeScript(exeBase);
    }

    public async Task<PythonRunResult> RunAsync(
        string arguments,
        Action<string>? onLog,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(PythonExePath))
        {
            return new PythonRunResult(false, $"未找到 Python 解释器：{PythonExePath}");
        }

        if (!File.Exists(ScriptPath))
        {
            return new PythonRunResult(false, $"未找到 Python 桥接脚本：{ScriptPath}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = PythonExePath,
            Arguments = $"\"{ScriptPath}\" {arguments}",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        string? resultJson = null;

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            if (e.Data.StartsWith(ResultPrefix, StringComparison.Ordinal))
            {
                resultJson = e.Data[ResultPrefix.Length..];
                return;
            }

            onLog?.Invoke(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                onLog?.Invoke($"[stderr] {e.Data}");
            }
        };

        process.Exited += (_, _) =>
        {
            tcs.TrySetResult(process.ExitCode);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore
            }
        });

        var exitCode = await tcs.Task.ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            return new PythonRunResult(false, "任务已取消。");
        }

        if (!string.IsNullOrWhiteSpace(resultJson))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<PythonPayload>(resultJson);
                if (payload is not null)
                {
                    return new PythonRunResult(payload.success, payload.message ?? string.Empty);
                }
            }
            catch
            {
                // fallback to exit code below
            }
        }

        return exitCode == 0
            ? new PythonRunResult(true, "任务执行完成。")
            : new PythonRunResult(false, $"任务执行失败，退出码：{exitCode}");
    }

    private static string ResolvePythonExe(string baseDir)
    {
        return Path.Combine(baseDir, "python310", "python.exe");
    }

    private static string ResolveBridgeScript(string baseDir)
    {
        var dir = new DirectoryInfo(baseDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "python_bridge.py");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return Path.Combine(baseDir, "python_bridge.py");
    }

    private sealed class PythonPayload
    {
        public bool success { get; set; }
        public string? message { get; set; }
    }
}

public readonly record struct PythonRunResult(bool Success, string Message);
