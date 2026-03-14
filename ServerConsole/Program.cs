using ServerConsole.Log;
using ServerConsole.ServerManager;
using System.Runtime.InteropServices;
using System.Text;

public class Program
{
    public static string EXE_PATH = "SiteFrostfall.exe";
    private const int DEFAULT_PORT = 7777;
    // 设为静态方便指令集调用（如 ExitCommand）
    public static ServerProcess? ServerInstance { get; private set; }

    public static void Main(string[] args)
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

        // 启动进程
        using (ServerInstance = new ServerProcess(EXE_PATH, $"--port {port}"))
        {
            ServerInstance.Start();
            while (ServerInstance.IsRunning)
            {
                Thread.Sleep(500);
            }
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
}