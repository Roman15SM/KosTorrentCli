using System;

namespace KosTorrentCli
{
    /// <summary>
    /// Console logging with timestamps. Errors are red, warnings (usual peer problems: timeouts, refused connections) are yellow.
    /// </summary>
    public static class Log
    {
        //peers log from different threads: color change and write must not interleave
        private static readonly object Locker = new object();

        public static void Info(string message)
        {
            Write(message, null);
        }

        public static void Warning(string message)
        {
            Write(message, ConsoleColor.Yellow);
        }

        public static void Error(string message)
        {
            Write(message, ConsoleColor.Red);
        }

        private static void Write(string message, ConsoleColor? color)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

            lock (Locker)
            {
                if (color == null)
                {
                    Console.WriteLine(line);
                    return;
                }

                Console.ForegroundColor = color.Value;
                Console.WriteLine(line);
                Console.ResetColor();
            }
        }
    }
}
