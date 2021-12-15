using System;
namespace ASocket.Log
{
    [Flags]
    public enum LogLevel
    {
        Verbose,
        Info,
        Error
    }
    
    public static class Log
    {
        internal static bool Enabled { get; set; }
        private static LogLevel _logLevel;

        public static Action<LogLevel, string> LogEvent;

        public static void SetLogLevel(LogLevel logLevel)
        {
            _logLevel = logLevel;
        }
        
        public static void Verbose(string log)
        {
            if (_logLevel.HasFlag(LogLevel.Verbose))
            {
                Console.WriteLine(log);
                LogEvent?.Invoke(LogLevel.Verbose, log);
            }
        }
        
        public static void Info(string log)
        {
            if (_logLevel.HasFlag(LogLevel.Info))
            {
                Console.WriteLine(log);
                LogEvent?.Invoke(LogLevel.Verbose, log);
            }
        }
        
        public static void Error(string log)
        {
            if (_logLevel.HasFlag(LogLevel.Error))
            {
                Console.WriteLine(log);
                LogEvent?.Invoke(LogLevel.Verbose, log);
            }
        }
    }
}
