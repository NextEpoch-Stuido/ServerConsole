// ConsoleCommand.cs
using System;

namespace ServerConsole.ServerManager
{
    public abstract class ConsoleCommand
    {
        public abstract string Name { get; }
        public virtual string Description => "";
        public abstract void Execute(string[] args);
    }
}