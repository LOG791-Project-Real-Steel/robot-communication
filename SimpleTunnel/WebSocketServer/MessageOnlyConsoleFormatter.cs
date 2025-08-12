using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace WebSocketServer
{
    public sealed class MessageOnlyConsoleFormatter : ConsoleFormatter
    {
        public const string Name = "messageOnly";
        public MessageOnlyConsoleFormatter() : base(Name) { }

        public override void Write<TState>(
            in LogEntry<TState> logEntry,
            IExternalScopeProvider? scopeProvider,
            TextWriter writer)
        {
            var text = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception)
                       ?? logEntry.State?.ToString();

            if (string.IsNullOrEmpty(text)) return;

            writer.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff "));
            writer.Write(GetLevel(logEntry.LogLevel));
            writer.Write(' ');

            writer.WriteLine(text);

            if (logEntry.Exception is { } ex)
                writer.WriteLine(ex);
        }

        private static string GetLevel(LogLevel l) => l switch
        {
            LogLevel.Trace       => "trce",
            LogLevel.Debug       => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning     => "warn",
            LogLevel.Error       => "fail",
            LogLevel.Critical    => "crit",
            _                    => "none"
        };
    }
}
