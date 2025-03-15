using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ServerConsole
{
    /// <summary>
    /// 表示一个连接到TCP服务器进行日志记录的日志器。
    /// </summary>
    public class Logger
    {
        private TcpListener loggerListener;
        /// <summary>
        /// 初始化一个新的Logger实例并开始监听指定端口。
        /// </summary>
        /// <param name="port">日志服务器监听的端口号。</param>
        public Logger(int port)
        {
            try
            {
                loggerListener = new TcpListener(IPAddress.Any, port);
                loggerListener.Start();
                Log($"日志服务器已启动，在端口 {port} 上监听...");
                Task.Run(AcceptClients).Wait();
            }
            catch (SocketException e)
            {
                Program.Error(e.Message);
            }
        }

        /// <summary>
        /// 接受客户端连接并处理日志接收。
        /// </summary>
        private async Task AcceptClients()
        {
            while (true)
            {
                var client = await loggerListener.AcceptTcpClientAsync();
                Log("客户端已连接");
                _ = HandleClient(client); 
            }
        }

        /// <summary>
        /// 处理单个客户端的连接和日志接收。
        /// </summary>
        private async Task HandleClient(TcpClient client)
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[2048];
                int read;
                try
                {
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, read);
                        var log = JsonConvert.DeserializeObject<LogMessage>(message);
                        Log(log.Message, log.Color);
                    }
                }
                catch (Exception e)
                {
                    Log($"接收日志时发生错误: {e.Message}", ConsoleColor.Red);
                }
                finally
                {
                    Log("客户端断开连接", ConsoleColor.Yellow);
                }
            }
        }

        /// <summary>
        /// 将消息记录到控制台，并可以选择颜色。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        /// <param name="color">显示消息的颜色，默认为DarkBlue。</param>
        public static void Log(object message, ConsoleColor color = ConsoleColor.DarkBlue)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}