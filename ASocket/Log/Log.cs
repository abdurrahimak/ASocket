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
        private static LogLevel _logLevel = LogLevel.Error | LogLevel.Info;
        private static int _dispatcherId;
        private static bool _hasUpdater;

        public static Action<LogLevel, string> LogEvent;

        internal static void SetUpdater(bool hasUpdater)
        {
            _hasUpdater = hasUpdater;
            if (_hasUpdater)
            {

                if (_dispatcherId != 0)
                {
                    MainThreadDispatcher.UnregisterDispatcher(_dispatcherId);
                }
                _dispatcherId = MainThreadDispatcher.CreateDispatcherId();
                MainThreadDispatcher.RegisterDispatcher(_dispatcherId);
            }
        }

        public static void SetLogLevel(LogLevel logLevel)
        {
            _logLevel = logLevel;
        }
        
        internal static void Verbose(string log)
        {
            if (_logLevel.HasFlag(LogLevel.Verbose))
            {
                Console.WriteLine(log);
                LogWithDispatcher(LogLevel.Verbose, log);
            }
        }
        
        internal static void Info(string log)
        {
            if (_logLevel.HasFlag(LogLevel.Info))
            {
                Console.WriteLine(log);
                LogWithDispatcher(LogLevel.Info, log);
            }
        }
        
        internal static void Error(string log)
        {
            if (_logLevel.HasFlag(LogLevel.Error))
            {
                Console.WriteLine(log);
                LogWithDispatcher(LogLevel.Error, log);
            }
        }

        internal static void Update()
        {
            MainThreadDispatcher.Update(_dispatcherId);
        }

        private static void LogWithDispatcher(LogLevel logLevel, string log)
        {
            if (_hasUpdater)
            {
                LogEvent?.Invoke(logLevel, log);
            }
            else
            {
                MainThreadDispatcher.Enqueue(_dispatcherId, () =>
                {
                    LogEvent?.Invoke(logLevel, log);
                });
            }
        }
    }
}
