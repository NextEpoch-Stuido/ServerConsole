using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerConsole.Server
{
    public class Server
    {
        public bool IsRunning { get; private set; }
        private Process? ServerProcess { get; set; }
        public Logger? Logger { get; set; }

        public void Start(string path, int port)
        {
            Process server = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = path,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardInputEncoding = Encoding.UTF8,
                    Arguments = $"-port {port}",
                },
                EnableRaisingEvents = true,

            };
            server.Exited += Server_Exited;
            AppDomain.CurrentDomain.ProcessExit += Process_Exit;
            server.Start();
            Logger = new(port);
            ServerProcess = server;
            IsRunning = true;
            Logger.Log("服务器开始运行");
        }
        private void Server_Exited(object? sender, EventArgs e)
        {
            IsRunning = false;
            Environment.Exit(0);
        }
        private void Process_Exit(object? sender, EventArgs e)
        {
            if (IsRunning)
                ServerProcess.Kill();
        }
    }
}
