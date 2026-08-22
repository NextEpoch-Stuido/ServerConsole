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
        private const int GracefulShutdownTimeoutMilliseconds = 15000;

        private Process? _process;
        private readonly string _exePath;
        private readonly string _args;
        private readonly int _serverPort;
        private readonly object _shutdownLock = new();
        private volatile bool _isShuttingDown;
        private bool _serverReady;

        public bool IsRunning
        {
            get
            {
                try
                {
                    return _process != null && !_process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }

        public ServerProcess(string exePath, string arguments = "", int serverPort = 0)
        {
            _exePath = exePath;
            _args = arguments;
            _serverPort = serverPort;
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
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                _process = new Process { StartInfo = psi };
                _process.EnableRaisingEvents = true;

                _process.Exited += (s, e) =>
                {
                    Logger.InternalLog_h(
                        _isShuttingDown
                            ? "Game server process has fully exited."
                            : "Game server process exited unexpectedly.",
                        _isShuttingDown ? LogLevel.Success : LogLevel.Warning);
                };

                _process.OutputDataReceived += (s, e) => ParseUnityOutput(e.Data);

                // Unity writes its own Debug output to both stdout and stderr.
                // Parse only the explicit game-console protocol on either stream.
                _process.ErrorDataReceived += (s, e) => ParseUnityOutput(e.Data);

                if (_process.Start())
                {
                    UpdateConsoleTitle(0, 0, _serverPort);
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();

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

                if (input == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(input) || !IsRunning)
                {
                    continue;
                }

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
            if (!IsRunning)
            {
                return;
            }

            string payload = args.Length > 0
                ? $"CMD:{cmdName}|{string.Join("|", args)}"
                : $"CMD:{cmdName}";

            try
            {
                _process?.StandardInput.WriteLine(payload);
                _process?.StandardInput.Flush();
            }
            catch (Exception ex)
            {
                Logger.InternalLog_h(
                    $"Failed to send command '{cmdName}' to the game server: {ex.Message}",
                    LogLevel.Error);
            }
        }

        private void ParseUnityOutput(string? data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return;
            }

            if (data.StartsWith("OUT:", StringComparison.OrdinalIgnoreCase))
            {
                ParseOutMessage(data.Substring(4));
                return;
            }

            if (data.StartsWith("RESPONSE:", StringComparison.OrdinalIgnoreCase))
            {
                string response = data.Substring("RESPONSE:".Length);
                Logger.Print($"[Server.Response] {response}", ConsoleColor.Green);
                return;
            }

            if (data.StartsWith("STATUS:", StringComparison.OrdinalIgnoreCase))
            {
                ParseServerStatus(data.Substring("STATUS:".Length));
                return;
            }

            // 兼容旧协议：LOG:Tag|Message
            if (data.StartsWith("LOG:", StringComparison.OrdinalIgnoreCase))
            {
                ParseLegacyLogMessage(data.Substring(4));
                return;
            }

            // Deliberately ignore Unity's own Debug/engine output. Only OUT,
            // RESPONSE and the legacy LOG protocol belong in this console.
        }

        private void ParseServerStatus(string payload)
        {
            string[] parts = payload.Split('|', 4);
            if (parts.Length < 4 ||
                !int.TryParse(parts[1], out int playerCount) ||
                !int.TryParse(parts[2], out int playerLimit) ||
                !int.TryParse(parts[3], out int port))
            {
                return;
            }

            UpdateConsoleTitle(playerCount, playerLimit, port > 0 ? port : _serverPort);

            if (parts[0].Equals("READY", StringComparison.OrdinalIgnoreCase) && !_serverReady)
            {
                _serverReady = true;
                Logger.Print("等待玩家加入...", ConsoleColor.Green);
            }
        }

        private static void UpdateConsoleTitle(int playerCount, int playerLimit, int port)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                string limit = playerLimit > 0 ? playerLimit.ToString() : "?";
                Console.Title = $"SiteFrostfall | 玩家: {playerCount}/{limit} | 端口: {port}";
            }
            catch (Exception)
            {
                // A redirected/no-window process may not expose a console title.
            }
        }

        private void ParseOutMessage(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            // OUT:颜色枚举数值|信息前缀|内容
            // Split('|', 3) 可以保证 message 里继续包含 | 时不会被继续切开
            string[] parts = payload.Split('|', 3);

            if (parts.Length < 3)
            {
                Logger.Print($"[Unity.Out.Invalid] {payload}", ConsoleColor.Yellow);
                return;
            }

            string colorRaw = parts[0];
            string info = parts[1];
            string message = parts[2];

            ConsoleColor color = ConsoleColor.White;

            if (int.TryParse(colorRaw, out int colorValue) &&
                Enum.IsDefined(typeof(ConsoleColor), colorValue))
            {
                color = (ConsoleColor)colorValue;
            }

            Logger.Out(message, color, info);
        }

        private void ParseLegacyLogMessage(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            string[] parts = payload.Split('|', 2);

            if (parts.Length >= 2)
            {
                Logger.Out(parts[1], ConsoleColor.White, parts[0]);
            }
            else
            {
                Logger.Out(payload, ConsoleColor.White, "[Unity.Log]");
            }
        }

        public void Stop()
        {
            lock (_shutdownLock)
            {
                if (_process == null)
                {
                    return;
                }

                try
                {
                    if (_process.HasExited)
                    {
                        return;
                    }

                    _isShuttingDown = true;
                    Logger.InternalLog_h(
                        "Requesting graceful shutdown from the game server...",
                        LogLevel.Warning);
                    SendRemoteCommand("shutdown", Array.Empty<string>());

                    if (!_process.WaitForExit(GracefulShutdownTimeoutMilliseconds))
                    {
                        Logger.InternalLog_h(
                            "Game server did not exit in time; terminating its process tree...",
                            LogLevel.Warning);
                        _process.Kill(true);
                        _process.WaitForExit();
                    }

                    // Drain redirected streams only after the process has exited.
                    _process.WaitForExit();
                }
                catch (InvalidOperationException)
                {
                    // The process exited between the state check and shutdown request.
                }
                catch (Exception ex)
                {
                    Logger.InternalLog_h($"Error while stopping game server: {ex.Message}", LogLevel.Error);

                    if (!_process.HasExited)
                    {
                        _process.Kill(true);
                        _process.WaitForExit();
                    }
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _process?.Dispose();
        }
    }
}
