using ServerConsole.Log;
using ServerConsole.ServerManager;
using System.Runtime.InteropServices;
using System.Text;

public class Program
{
    public static readonly string EXE_PATH = "SiteFrostfall.exe";
    private const int DEFAULT_PORT = 7777;
    // 设为静态方便指令集调用（如 ExitCommand）
    public static ServerProcess? ServerInstance { get; private set; }
    private static Exception? _fatalError;
    private static int _fatalErrorReported;

    public static void Main(string[] args)
    {
        try
        {
            InitializeConsole();
            PrintBanner();

            int? port = ParsePortFromArgs(args);

            // 如果命令行未提供有效端口，则提示用户输入
            while (!port.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write($"{GetTimeTag()} [Prompt] Enter server port (1-65535): ");
                Console.ResetColor();
                string? input = Console.ReadLine();
                if (int.TryParse(input, out int p) && IsValidPort(p)) port = p;
                else Logger.Print("Invalid port number. Please enter a number between 1 and 65535.",ConsoleColor.Red);
            }

            Logger.InternalLog_h($"Initializing site on PORT: {port}", LogLevel.Info);

            Console.CancelKeyPress += OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            // 启动进程
            using (ServerInstance = new ServerProcess(EXE_PATH, $"--port {port}", port.Value))
            {
                ServerInstance.Start();
                while (ServerInstance.IsRunning && Volatile.Read(ref _fatalError) == null)
                {
                    Thread.Sleep(500);
                }
            }

            Exception? fatalError = Volatile.Read(ref _fatalError);
            if (fatalError != null)
            {
                ShowFatalError(fatalError);
            }
        }
        catch (Exception ex)
        {
            ReportFatalError(ex);
            ShowFatalError(Volatile.Read(ref _fatalError) ?? ex);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
    }

    /// <summary>
    /// Records a console-side failure and stops the game before the error is shown.
    /// It is safe to call from background threads and more than once.
    /// </summary>
    internal static void ReportFatalError(Exception exception)
    {
        if (Interlocked.Exchange(ref _fatalErrorReported, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref _fatalError, exception);
        try
        {
            ServerInstance?.Stop();
        }
        catch (Exception stopException)
        {
            try
            {
                Console.Error.WriteLine($"Failed to stop game process after an error: {stopException.Message}");
            }
            catch
            {
                // Console output may already be unavailable during process shutdown.
            }
        }
    }

    private static void ShowFatalError(Exception exception)
    {
        try
        {
            Logger.InternalLog_h("ServerConsole encountered an unexpected error:", LogLevel.Error);
            Logger.Print(exception.ToString(), ConsoleColor.Red);
            Logger.Print("游戏已关闭。请按 Enter 键退出控制台。", ConsoleColor.Yellow);
            Console.ReadLine();
        }
        catch
        {
            try { Console.Error.WriteLine(exception); } catch { }
        }
    }

    private static void InitializeConsole()
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (OperatingSystem.IsWindows())
        {
            Console.Title = "SiteFrostfall | Dedicated Server Console";
        }
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
 ███████╗██╗████████╗███████╗          ███████╗██████╗  ██████╗ ███████╗████████╗███████╗ █████╗ ██╗     ██╗     
 ██╔════╝██║╚══██╔══╝██╔════╝          ██╔════╝██╔══██╗██╔═══██╗██╔════╝╚══██╔══╝██╔════╝██╔══██╗██║     ██║     
 ███████╗██║   ██║   █████╗   ███████╗ █████╗  ██████╔╝██║   ██║███████╗   ██║   █████╗  ███████║██║     ██║     
 ╚════██║██║   ██║   ██╔══╝   ╚══════╝ ██╔══╝  ██╔══██╗██║   ██║╚════██║   ██║   ██╔══╝  ██╔══██║██║     ██║     
 ███████║██║   ██║   ███████╗          ██║     ██║  ██║╚██████╔╝███████║   ██║   ██║     ██║  ██║███████╗███████╗
 ╚══════╝╚═╝   ╚═╝   ╚══════╝          ╚═╝     ╚═╝  ╚═╝ ╚═════╝ ╚══════╝   ╚═╝   ╚═╝     ╚═╝  ╚═╝╚══════╝╚══════╝");

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(" -----------------------------------------------------------------------------------------------------------------");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Copyright 2025 NextEpoch Studio & to0c123. All Rights Reserved.");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(" -----------------------------------------------------------------------------------------------------------------\n");
        Console.ResetColor();
    }

    private static string GetTimeTag() => $"[{DateTime.Now:HH:mm:ss}]";

    private static int? ParsePortFromArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--port=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(args[i].Substring(7), out int p) && IsValidPort(p)) return p;
            }
            else if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int p) && IsValidPort(p)) return p;
            }
        }
        return null;
    }

    private static bool IsValidPort(int port) => port >= 1 && port <= 65535;

    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        ServerInstance?.Stop();
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        ServerInstance?.Stop();
    }
}
