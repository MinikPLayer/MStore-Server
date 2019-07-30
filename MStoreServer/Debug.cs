using System;
using System.Collections.Generic;
using System.Text;

namespace MStoreServer
{
    public static class Debug
    {
        public static void Log(object data, ConsoleColor color = ConsoleColor.White)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(data);
            Console.ForegroundColor = originalColor;
        }

        public static void LogWarning(object data)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(data);
            Console.ForegroundColor = originalColor;
        }

        public static void LogError(object data)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(data);
            Console.ForegroundColor = originalColor;
        }
    }
}
