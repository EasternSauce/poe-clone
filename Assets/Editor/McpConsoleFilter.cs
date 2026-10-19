using System;
using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    // MCP can resend a command after an assembly reload or a long build. Its transport
    // safely reuses the original result; that recovery message needs no user action.
    [InitializeOnLoad]
    internal sealed class McpConsoleFilter : ILogHandler
    {
        private const string DuplicateCommandWarning =
            "<b><color=#cc7a00>MCP-FOR-UNITY</color></b>: Suppressed duplicate command resent on a new connection; " +
            "awaiting the in-flight result instead of running it twice.";

        private readonly ILogHandler original;

        static McpConsoleFilter()
        {
            var logger = Debug.unityLogger;
            if (logger.logHandler is McpConsoleFilter) return;

            var filter = new McpConsoleFilter(logger.logHandler);
            logger.logHandler = filter;
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (ReferenceEquals(logger.logHandler, filter))
                    logger.logHandler = filter.original;
            };
        }

        private McpConsoleFilter(ILogHandler original)
        {
            this.original = original;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            if (logType == LogType.Warning &&
                (format == DuplicateCommandWarning ||
                 (format == "{0}" && args != null && args.Length == 1 &&
                  args[0] is string message && message == DuplicateCommandWarning)))
                return;

            original.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            original.LogException(exception, context);
        }
    }
}
