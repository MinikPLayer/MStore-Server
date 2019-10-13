using System;
using System.Collections.Generic;
using System.Text;

namespace MStoreServer
{
    public static class Debug
    {
        public static void Log(object data, ConsoleColor color = ConsoleColor.White, bool newLine = true)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (newLine)
            {
                Console.WriteLine(data);
            }
            else
            {
                Console.Write(data);
            }
            Console.ForegroundColor = originalColor;
        }

        public static void LogWarning(object data, bool newLine = true)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            if (newLine)
            {
                Console.WriteLine(data);
            }
            else
            {
                Console.Write(data);
            }
            Console.ForegroundColor = originalColor;
        }

        public static void LogError(object data, bool newLine = true)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            if (newLine)
            {
                Console.WriteLine(data);
            }
            else
            {
                Console.Write(data);
            }
            Console.ForegroundColor = originalColor;
        }

        public static void ConversionError(string src, string dstName = "", object dst = null)
        {
            if (dst == null)
            {
                LogError("Cannot convert \"" + src + "\"");
                return;
            }

            LogError("Cannot convert \"" + src + "\" to " + dstName + " ( " + dst.GetType().Name + " )");
        }
    }
}
