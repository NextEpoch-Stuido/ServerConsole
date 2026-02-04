using ServerConsole.Log;
using ServerConsole.ServerManager;

public class Program
{
    private const int DEFAULT_PORT = 7777;

    public static void Main(string[] args)
    {
        int? port = null; // 使用可空类型，便于判断是否已设置

        if (OperatingSystem.IsWindows())
        {
            Console.Title = "SiteFrostfall Dedicated Server Console";
        }

        Logger.InternalLog("Copyright 2025 NextEpoch Studio and \"to0c123\". All Rights Reserved.", LogLevel.Info);
        Logger.InternalLog("The dedicated server console has been launched.", LogLevel.Info);

        // 尝试从命令行参数解析端口
        if (args.Length > 0)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("--port=", StringComparison.OrdinalIgnoreCase))
                {
                    string portStr = arg.Substring("--port=".Length);
                    if (int.TryParse(portStr, out int p) && IsValidPort(p))
                    {
                        port = p;
                        Logger.InternalLog($"Port {p} parsed from '--port=' argument.", LogLevel.Info);
                        break;
                    }
                }
                else if (arg.Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int p) && IsValidPort(p))
                    {
                        port = p;
                        Logger.InternalLog($"Port {p} parsed from '--port <value>' argument.", LogLevel.Info);
                        break;
                    }
                }
                else if (int.TryParse(arg, out int p) && IsValidPort(p))
                {
                    port = p;
                    Logger.InternalLog($"Port {p} parsed as positional argument.", LogLevel.Info);
                    break;
                }
            }
        }

        // 如果命令行未提供有效端口，则提示用户输入
        while (!port.HasValue)
        {
            Console.Write("Please enter the server port (1-65535): ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Port cannot be empty. Please try again.");
                continue;
            }

            if (int.TryParse(input, out int p) && IsValidPort(p))
            {
                port = p;
                Logger.InternalLog($"Port {p} entered by user.", LogLevel.Info);
            }
            else
            {
                Console.WriteLine("Invalid port number. Please enter a number between 1 and 65535.");
            }
        }

        Logger.InternalLog($"Server will listen on port: {port}", LogLevel.Info);


        using var server = new ServerProcess(port.Value);
        server.WaitForExit();
    }

    private static bool IsValidPort(int port)
    {
        return port >= 1 && port <= 65535;
    }
}