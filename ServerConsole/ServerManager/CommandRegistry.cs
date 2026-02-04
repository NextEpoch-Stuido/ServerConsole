// CommandRegistry.cs
using ServerConsole.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ServerConsole.ServerManager
{
    public static class CommandRegistry
    {
        private static readonly Dictionary<string, ConsoleCommand> _commands = new();

        static CommandRegistry()
        {
            RegisterAllCommands();
        }

        public static void RegisterCommand(ConsoleCommand command)
        {
            if (command == null) return;
            _commands[command.Name.ToLowerInvariant()] = command;
        }

        private static void RegisterAllCommands()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var commandTypes = assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(ConsoleCommand)) && !t.IsAbstract);

            foreach (var type in commandTypes)
            {
                try
                {
                    var instance = (ConsoleCommand)Activator.CreateInstance(type)!;
                    RegisterCommand(instance);
                }
                catch (Exception ex)
                {
                    Logger.InternalLog_h($"Failed to instantiate command {type.Name}: {ex.Message}", LogLevel.Error);
                }
            }
        }

        public static bool TryGetCommand(string name, out ConsoleCommand? command)
        {
            return _commands.TryGetValue(name.ToLowerInvariant(), out command);
        }

        public static IEnumerable<ConsoleCommand> GetAllCommands() => _commands.Values;
    }
}