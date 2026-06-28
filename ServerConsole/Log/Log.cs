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
        /// <summary>
        /// 将常见颜色名称字符串转换为 ConsoleColor 枚举。
        /// 不区分大小写，支持中文别名。找不到时返回 null。
        /// </summary>
        public static ConsoleColor? GetColor(string colorName)
        {
            if (string.IsNullOrEmpty(colorName))
                return null;

            return colorName.ToLowerInvariant() switch
            {
                "black" or "黑色" => ConsoleColor.Black,
                "darkblue" or "深蓝色" => ConsoleColor.DarkBlue,
                "darkgreen" or "深绿色" => ConsoleColor.DarkGreen,
                "darkcyan" or "深青色" => ConsoleColor.DarkCyan,
                "darkred" or "深红色" => ConsoleColor.DarkRed,
                "darkmagenta" or "深洋红" => ConsoleColor.DarkMagenta,
                "darkyellow" or "深黄色" => ConsoleColor.DarkYellow,
                "gray" or "灰色" => ConsoleColor.Gray,
                "darkgray" or "深灰色" => ConsoleColor.DarkGray,
                "blue" or "蓝色" => ConsoleColor.Blue,
                "green" or "绿色" => ConsoleColor.Green,
                "cyan" or "青色" => ConsoleColor.Cyan,
                "red" or "红色" => ConsoleColor.Red,
                "magenta" or "洋红" => ConsoleColor.Magenta,
                "yellow" or "黄色" => ConsoleColor.Yellow,
                "white" or "白色" => ConsoleColor.White,
                _ => null
            };
        }

        /// <summary>
        /// 使用字符串颜色名输出文本（不换行）。
        /// </summary>
        public static void Print(string message, string colorName)
        {
            ConsoleColor? color = GetColor(colorName);
            if (color.HasValue)
                StandardOutput.Print_c(message, color.Value);
            else
                StandardOutput.Print(message); // 未识别颜色时正常输出
        }

        /// <summary>
        /// 使用字符串颜色名输出格式化文本（不换行）。
        /// </summary>
        public static void Print(string message, string colorName, params object[] args)
        {
            ConsoleColor? color = GetColor(colorName);
            if (color.HasValue)
                StandardOutput.Printf_c(message, color.Value, args);
            else
                StandardOutput.Printf(message, args);
        }

        // 以下为便捷的自定义颜色日志输出（带时间戳和标签）
        public static void Log(string message, string colorName, string tag = null)
        {
            string header = string.IsNullOrEmpty(tag) ? "" : $"[{tag}] ";
            ConsoleColor? color = GetColor(colorName);
            if (color.HasValue)
                StandardOutput.Printfln_c($"{header}{message}", color.Value);
            else
                StandardOutput.Println($"{header}{message}");
        }

        public static void Log(string message, string colorName, string tag, params object[] args)
        {
            string header = string.IsNullOrEmpty(tag) ? "" : $"[{tag}] ";
            ConsoleColor? color = GetColor(colorName);
            if (color.HasValue)
                StandardOutput.Printfln_c($"{header}{message}", color.Value, args);
            else
                StandardOutput.Printfln($"{header}{message}", args);
        }
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

        public static void Out(string message, ConsoleColor color, string info = "[Unity]")
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string prefix = string.IsNullOrWhiteSpace(info) ? "[Unity]" : info;

            StandardOutput.Printfln_c($"[{time}] {prefix} {message}", color);
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
