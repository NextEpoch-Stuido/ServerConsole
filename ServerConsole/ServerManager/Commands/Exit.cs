using ServerConsole.Log;

namespace ServerConsole.ServerManager
{
    public class ExitCommand : ConsoleCommand
    {
        public override string Name => "exit";
        public override string Description => "Gracefully shut down the server.";

        public override void Execute(string[] args)
        {
            Logger.InternalLog_h("Shutting down via console command...", LogLevel.Warning);
            Program.ServerInstance?.Stop();
        }
    }
}