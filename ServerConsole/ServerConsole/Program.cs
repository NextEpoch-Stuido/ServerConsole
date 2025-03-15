using ServerConsole;
using ServerConsole.Server;

public class Program
{
    public static Server? Server { get; set; }
    public static void Main(string[] args)
    {
        Console.Title = "NextEpoch - ServerConsole";
        Logger.Log("ServerConsole 已启动", ConsoleColor.DarkGreen);
        Logger.Log("欢迎使用 NextEpoch 服务器控制台！", ConsoleColor.DarkGreen);
        Logger.Log("请输入端口号：");
        Start();
    }
    public static void Start()
    {
        string? port = Console.ReadLine();
        if (port == null)
        {
            throw new ArgumentException("端口号不能为空！");
        }
        else if (int.TryParse(port, out int portNum))
        {
            if(portNum < 1024 || portNum > 65535)
            {
                Error("端口号必须在 1024 到 65535 之间！");
            }
            Logger.Log("服务器已启动，端口号：" + portNum, ConsoleColor.DarkGreen);
            Server = new();
            Server.Start("SiteWinter.exe",portNum);
        }
    }
    public static void Error(string? message = null)
    {
        if (message!= null)
        {
            Logger.Log(message, ConsoleColor.Red);
        }
        Logger.Log("发生错误，按任意键退出...", ConsoleColor.Red);
        Console.ReadLine();
        Environment.Exit(0);
    }
}
