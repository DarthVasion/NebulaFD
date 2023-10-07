﻿namespace SapphireD.Core.Utilities
{
    public static class Logger
    {
        static bool _doLog = true;

        public static void Log(object parent, object message, int type = 0, ConsoleColor color = ConsoleColor.Black)
        {
            if (_doLog)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                string info = $"[{DateTime.Now.ToString("HH:mm:ss")}] ";
                Console.Write(info);
                Console.ForegroundColor = color;
                if (color == ConsoleColor.Black)
                    Console.ResetColor();
                Console.WriteLine(message);
            }

            string outInfo = $"[{(type != 0 ? type + "\\" : "")}{parent.GetType().Name}\\{DateTime.Now.ToString("HH:mm:ss.ff")}] ";
            File.AppendAllText("Latest.log", outInfo + message + "\n");

            Console.ResetColor();
        }

        public static void Log(object message, int type = 0, ConsoleColor color = ConsoleColor.Black)
        {
            if (_doLog)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                string info = $"[{DateTime.Now.ToString("HH:mm:ss")}] ";
                Console.Write(info);
                Console.ForegroundColor = color;
                if (color == ConsoleColor.Black)
                    Console.ResetColor();
                Console.WriteLine(message);
            }

            string outInfo = $"[{(type != 0 ? type + "\\" : "")}{DateTime.Now.ToString("HH:mm:ss.ff")}] ";
            File.AppendAllText("Latest.log", outInfo + message + "\n");

            Console.ResetColor();
        }
    }
}
