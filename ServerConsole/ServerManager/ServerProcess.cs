using ServerConsole.Log;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

        private delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

        private enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        public ServerProcess(int port)
        {
            _port = port;

            if (OperatingSystem.IsWindows())
            {
                Console.Title = $"SiteFrostfall Dedicated Server Console | Port: {port} | Windows x{Environment.OSVersion.Version.Major} " +
                                $"| (PID: {Process.GetCurrentProcess().Id})";

                // 设置控制台关闭事件处理器
                SetConsoleCtrlHandler(new ConsoleCtrlDelegate(ConsoleHandler), true);
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

        // 在 ServerProcess.cs 中添加字段
        private readonly Dictionary<string, ConsoleCommand> _dynamicCommands = new();

        // 修改 StartInputListener 方法中的循环部分：
        private void StartInputListener()
        {
            // 注册需要上下文的命令（如 exit）
            var exitCmd = new ExitCommand(Shutdown);
            _dynamicCommands["exit"] = exitCmd;
            _dynamicCommands["quit"] = exitCmd;
            _dynamicCommands["shutdown"] = exitCmd;

            Thread inputThread = new Thread(() =>
            {
                Logger.InternalLog_h("Console input listener started. Type 'help' for commands.", LogLevel.Info);
                while (!_cts.Token.IsCancellationRequested && (_process?.HasExited == false))
                {
                    try
                    {
                        string? input = Console.ReadLine();
                        if (input == null) break;

                        input = input.Trim();
                        if (string.IsNullOrEmpty(input)) continue;

                        // 分割命令和参数
                        string[] parts = input.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) continue;

                        string commandName = parts[0].ToLowerInvariant();
                        string[] args = parts.Skip(1).ToArray();

                        // 优先检查动态命令（如 exit），再查注册命令
                        if (_dynamicCommands.TryGetValue(commandName, out var dynamicCmd))
                        {
                            dynamicCmd.Execute(args);
                            continue;
                        }

                        if (CommandRegistry.TryGetCommand(commandName, out var registeredCmd))
                        {
                            registeredCmd.Execute(args);
                            continue;
                        }

                        if (_process != null && !_process.HasExited) { 
                            Logger.InternalLog_h($"Unknown command: '{commandName}'. Type 'help' for available commands.",LogLevel.Error);
                        }
                        else
                        {
                            Logger.InternalLog_h($"Unknown command: '{commandName}'. Server not running.", LogLevel.Error);
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
            _cts.Cancel(); // 取消取消令牌
            Shutdown();
        }

        private bool ConsoleHandler(CtrlTypes CtrlType)
        {
            if (CtrlType == CtrlTypes.CTRL_CLOSE_EVENT)
            {
                Logger.InternalLog_h("Received console close signal. Terminating server process...", LogLevel.Info);
                _cts.Cancel(); // 取消取消令牌
                Shutdown();
                return true;
            }
            return false;
        }

        private void Shutdown()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_process != null && !_process.HasExited)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        _process.Kill(); // Windows: 强制终止
                    }
                    else
                    {
                        // Linux: 先尝试 SIGTERM，再强制 SIGKILL
                        _process.Kill(); // 这是 SIGTERM
                        if (!_process.WaitForExit(2000))
                        {
                            // 如果 2 秒没退出，发 SIGKILL
                            try
                            {
                                using var kill = new Process();
                                kill.StartInfo.FileName = "kill";
                                kill.StartInfo.Arguments = $"-9 {_process.Id}";
                                kill.StartInfo.UseShellExecute = false;
                                kill.StartInfo.CreateNoWindow = true;
                                kill.Start();
                                kill.WaitForExit(1000);
                            }
                            catch { /* ignore */ }
                        }
                    }

                    _process.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Logger.InternalLog_h($"Error while killing server process: {ex.Message}", LogLevel.Error);
            }

            Dispose();
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
