using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerConsole.Log
{
    public enum LogLevel
    {
        Info = 11,
        Success = 2,
        Error = 12,
        Warning = 14,
        Debug = 13,
    }
    public class Logger
    {
        public readonly static Dictionary<LogLevel, string> LogHeader = new Dictionary<LogLevel, string>()
        {
            {LogLevel.Success,"[Success] [{0}]: " },
            {LogLevel.Info,"[Info] [{0}]:" },
            {LogLevel.Warning,"[Warning] [{0}]: " },
            {LogLevel.Debug,"[Debug] [{0}]: " },
            {LogLevel.Error,"[Error] [{0}]: " }
        };
        internal static void InternalLog_h(string message, LogLevel level)
        {
            StandardOutput.Printfln_c(LogHeader[level] + message, (ConsoleColor)level, new object[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
        }
        internal static void InternalLog(string message, LogLevel level)
        {
            StandardOutput.Printfln_c(message, (ConsoleColor)level);
        }
        public static void Print(string message)
        {
            StandardOutput.Println(message);
        }
        public static void Print(string message, ConsoleColor color)
        {
            StandardOutput.Println_c(message, color);
        }
        public static void Print(string message, ConsoleColor color, params object[] args)
        {
            StandardOutput.Printfln_c(message, color, args);
        }
    }
    public class StandardOutput
    {
        public static void Println(string message)
        {
            Console.WriteLine(message);
        }
        public static void Println_c(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public static void Printfln(string message, params object[] args)
        {
            Console.WriteLine(message,args);
        }
        public static void Printfln_c(string message,ConsoleColor color, params object[] args)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message, args);
            Console.ResetColor();
            }

        public static void Print(string message)
        {
            Console.Write(message);
        }
        public static void Print_c(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(message);
            Console.ResetColor();
        }
        public static void Printf(string message, params object[] args)
        {
            Console.Write(message, args);
        }
        public static void Printf_c(string message, ConsoleColor color, params object[] args)
        {
            Console.ForegroundColor = color;
            Console.Write(message, args);
            Console.ResetColor();
        }
    }
}
