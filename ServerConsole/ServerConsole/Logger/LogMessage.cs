using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[Serializable]
public struct LogMessage
{
    public string Message { get; set; }
    public ConsoleColor Color { get; set; }
    public LogMessage(string message, ConsoleColor color)
    {
        Message = message;
        Color = color;
    }
}
