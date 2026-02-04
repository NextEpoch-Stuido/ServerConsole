using ServerConsole.Log;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ServerConsole.ServerManager
{
    public class ServerProcess : IDisposable
    {
        private static readonly string SERVER_EXECUTABLE_NAME =
            OperatingSystem.IsWindows() ? "SiteFrostfall.exe" : "SiteFrostfall";

        private readonly Process? _process;
        private readonly int _port;
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed = false;

        public ServerProcess(int port)
        {
            _port = port;

            if (OperatingSystem.IsWindows())
            {
                Console.Title = $"SiteFrostfall Dedicated Server Console | Port: {port} | Windows x{Environment.OSVersion.Version.Major} " +
                                $"| (PID: {Process.GetCurrentProcess().Id})";
            }

            if (!File.Exists(SERVER_EXECUTABLE_NAME))
            {
                var currentDir = Directory.GetCurrentDirectory();
                throw new FileNotFoundException(
                    $"Server executable '{SERVER_EXECUTABLE_NAME}' not found in: {currentDir}");
            }

            if (OperatingSystem.IsLinux())
            {
                EnsureExecutablePermission(SERVER_EXECUTABLE_NAME);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = SERVER_EXECUTABLE_NAME,
                Arguments = $"-batchmode -nographics -port {port}",
                UseShellExecute = false,
                CreateNoWindow = true,

                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            try
            {
                _process = new Process { StartInfo = startInfo };
                _process.EnableRaisingEvents = true;
                _process.Exited += OnChildProcessExited;

                _process.Start();

                StandardStreamManager.AttachToProcess(_process);

                StandardStreamManager.OnReceive += msg =>
                {
                    if (!string.IsNullOrEmpty(msg))
                        Logger.InternalLog_h($"[Unity Server] {msg}", LogLevel.Info);
                };

                StandardStreamManager.Send("handshake");

                Logger.InternalLog_h($"Launched server on port {_port} with PID {_process.Id}.", LogLevel.Info);

                Console.CancelKeyPress += OnMainProcessExit;
                StartInputListener();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to start server process '{SERVER_EXECUTABLE_NAME}' with args '-port {port}'.", ex);
            }
        }

        private void StartInputListener()
        {
            Thread inputThread = new Thread(() =>
            {
                Logger.InternalLog_h("Console input listener started. Type 'help' for commands.", LogLevel.Info);
                while (!_cts.Token.IsCancellationRequested && (_process?.HasExited == false))
                {
                    try
                    {
                        string? input = Console.ReadLine();
                        if (input == null) break; // EOF (e.g., Ctrl+Z on Windows)

                        input = input.Trim();
                        if (string.IsNullOrEmpty(input)) continue;

                        // 处理内置控制台命令
                        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                            input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                            input.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.InternalLog_h("Shutdown command received from console.", LogLevel.Info);
                            Shutdown();
                            break;
                        }

                        if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("Available commands:");
                            Console.WriteLine("  help       - Show this help");
                            Console.WriteLine("  exit/quit  - Gracefully shut down the server");
                            Console.WriteLine("  <any text> - Send command to the Unity server");
                            continue;
                        }

                        // 发送给 Unity 子进程
                        if (_process != null && !_process.HasExited)
                        {
                            StandardStreamManager.Send(input);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.InternalLog_h($"Error in input thread: {ex.Message}", LogLevel.Error);
                    }
                }
            })
            {
                IsBackground = true,
                Name = "ConsoleInputThread"
            };

            inputThread.Start();
        }

        private void OnChildProcessExited(object sender, EventArgs e)
        {
            Logger.InternalLog_h("Server process has exited. Shutting down console...", LogLevel.Warning);
            Shutdown();
        }

        private void OnMainProcessExit(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; 
            Logger.InternalLog_h("Received shutdown signal. Terminating server process...", LogLevel.Info);
            Shutdown();
        }

        private void Shutdown()
        {
            if (_disposed) return;

            // 取消任何等待
            _cts.Cancel();

            // 终止子进程（如果还在运行）
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(); // 强制终止
                    _process.WaitForExit(3000); // 等待最多3秒
                }
            }
            catch (Exception ex)
            {
                Logger.InternalLog_h($"Error while killing server process: {ex.Message}", LogLevel.Error);
            }

            // 清理自身
            Dispose();

            // 退出主程序
            Environment.Exit(0);
        }

        public void WaitForExit()
        {
            _process?.WaitForExit();
        }

        public bool HasExited => _process?.HasExited ?? true;
        public int? ExitCode => _process?.ExitCode;

        private static void EnsureExecutablePermission(string filePath)
        {
            if (!OperatingSystem.IsLinux()) return;
            try
            {
                using var chmod = new Process();
                chmod.StartInfo.FileName = "chmod";
                chmod.StartInfo.Arguments = $"+x \"{filePath}\"";
                chmod.StartInfo.UseShellExecute = false;
                chmod.StartInfo.CreateNoWindow = true;
                chmod.Start();
                chmod.WaitForExit();
                if (chmod.ExitCode != 0)
                {
                    Logger.InternalLog_h($"[Warning] Failed to chmod +x '{filePath}'", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.InternalLog_h($"[Warning] chmod failed: {ex.Message}", LogLevel.Warning);
            }
        }

        // --- IDisposable ---
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Console.CancelKeyPress -= OnMainProcessExit;
            if (_process != null)
            {
                _process.Exited -= OnChildProcessExited;
                _process.Dispose();
            }
            _cts.Dispose();

            GC.SuppressFinalize(this);
        }

        ~ServerProcess()
        {
            Dispose();
        }
    }
}