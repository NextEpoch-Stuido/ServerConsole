using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using ServerConsole.Log;

namespace ServerConsole.ServerManager
{
    public class ServerProcess : IDisposable
    {
        private Process? _process;
        private readonly string _exePath;
        private readonly string _args;
        private bool _isShuttingDown;

        public bool IsRunning => _process != null && !_process.HasExited && !_isShuttingDown;

        public ServerProcess(string exePath, string arguments = "")
        {
            _exePath = exePath;
            _args = arguments;
        }

        public void Start()
        {
            if (!File.Exists(_exePath))
            {
                Logger.InternalLog_h($"Target executable not found: '{_exePath}'", LogLevel.Error);
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = _exePath,
                    Arguments = _args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                _process = new Process { StartInfo = psi };
                _process.EnableRaisingEvents = true;

                _process.Exited += (s, e) =>
                {
                    if (!_isShuttingDown)
                    {
                        Logger.InternalLog_h("Game process has exited. Terminating console...", LogLevel.Warning);
                        _isShuttingDown = true;
                        // 结束控制台进程
                        Environment.Exit(0);
                    }
                };

                _process.OutputDataReceived += (s, e) => ParseUnityOutput(e.Data);
                _process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Logger.InternalLog_h($"[Unity-Error] {e.Data}", LogLevel.Error);
                };

                if (_process.Start())
                {
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();

                    // 启动控制台输入监听
                    Thread inputThread = new Thread(InputLoop)
                    {
                        IsBackground = true,
                        Name = "ConsoleInputHandler"
                    };
                    inputThread.Start();

                    Logger.InternalLog_h($"Process linked successfully. PID: {_process.Id}", LogLevel.Success);
                }
            }
            catch (Exception ex)
            {
                Logger.InternalLog_h($"Failed to start server process: {ex.Message}", LogLevel.Error);
            }
        }
        private void InputLoop()
        {
            while (IsRunning)
            {
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input) || !IsRunning) continue;

                string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string cmdName = parts[0].ToLowerInvariant();
                string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

                if (CommandRegistry.TryGetCommand(cmdName, out var command))
                {
                    command!.Execute(args);
                }
                else
                {
                    SendRemoteCommand(cmdName, args);
                }
            }
        }

        public void SendRemoteCommand(string cmdName, string[] args)
        {
            if (!IsRunning) return;
            string payload = args.Length > 0 ? $"CMD:{cmdName}|{string.Join("|", args)}" : $"CMD:{cmdName}";
            try
            {
                _process?.StandardInput.WriteLine(payload);
            }
            catch { /* 忽略写入错误 */ }
        }

        private void ParseUnityOutput(string? data)
        {
            if (string.IsNullOrEmpty(data)) return;
            Logger.InternalLog_h(data, LogLevel.Debug);
        }

        public void Stop()
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            try
            {
                if (_process != null && !_process.HasExited)
                {
                    Logger.InternalLog_h("Killing Unity process...", LogLevel.Warning);
                    _process.Kill(true);
                    _process.WaitForExit(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.InternalLog_h($"Error during process kill: {ex.Message}", LogLevel.Debug);
            }
        }

        public void Dispose()
        {
            Stop();
            _process?.Dispose();
        }
    }
}