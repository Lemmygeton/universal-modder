using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace Reclaimed.Core
{
    /// <summary>Static logger shared by Core and every survivor plugin ("[Reclaimed]" source).</summary>
    public static class Log
    {
        private static ManualLogSource source;
        private static readonly HashSet<string> onceKeys = new HashSet<string>();

        internal static void Init(ManualLogSource logSource)
        {
            source = logSource;
        }

        private static ManualLogSource Source
        {
            get
            {
                if (source == null) source = Logger.CreateLogSource("Reclaimed");
                return source;
            }
        }

        public static void Debug(string message) { Source.LogDebug(message); }
        public static void Info(string message) { Source.LogInfo(message); }
        public static void Warn(string message) { Source.LogWarning(message); }
        public static void Error(string message) { Source.LogError(message); }

        public static void Exception(Exception exception, string context)
        {
            Source.LogError(context + ": " + exception);
        }

        /// <summary>Logs a warning the first time <paramref name="key"/> is seen; later calls are dropped.</summary>
        public static void WarnOnce(string key, string message)
        {
            lock (onceKeys)
            {
                if (!onceKeys.Add(key)) return;
            }
            Source.LogWarning(message);
        }

        /// <summary>Logs an info line the first time <paramref name="key"/> is seen.</summary>
        public static void InfoOnce(string key, string message)
        {
            lock (onceKeys)
            {
                if (!onceKeys.Add(key)) return;
            }
            Source.LogInfo(message);
        }
    }
}
