using ServerConsole.Log;

namespace ServerConsole.ServerManager
{
    public class ExitCommand : ConsoleCommand
    {
        private readonly Action _shutdownAction;

        public ExitCommand(Action shutdownAction)
        {
            _shutdownAction = shutdownAction ?? throw new ArgumentNullException(nameof(shutdownAction));
        }

        public override string Name => "exit";
        public override string Description => "Gracefully shut down the server.";

        public override void Execute(string[] args)
        {
            Logger.InternalLog_h("Shutdown command received from console.", LogLevel.Info);
            _shutdownAction();
        }
    }
}