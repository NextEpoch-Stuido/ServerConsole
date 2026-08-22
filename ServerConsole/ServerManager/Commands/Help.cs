namespace ServerConsole.ServerManager
{
    public class HelpCommand : ConsoleCommand
    {
        public override string Name => "help";
        public override string Description => "Show available commands.";

        public override void Execute(string[] args)
        {
            Console.WriteLine("Available commands:");
            foreach (var cmd in CommandRegistry.GetAllCommands())
            {
                Console.WriteLine($"  {cmd.Name,-12} - {cmd.Description}");
            }

            // help is intentionally handled by both consoles: show the
            // wrapper commands above, then ask the game for its command list.
            Program.ServerInstance?.SendRemoteCommand(Name, args);
        }
    }
}
