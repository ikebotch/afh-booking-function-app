using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace AFH.Acs.Recorder.Logging
{
    public class SqlLoggerProvider : ILoggerProvider
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Func<string, LogLevel, bool> _filter;
        private readonly ConcurrentDictionary<string, SqlLogger> _loggers = new();

        public SqlLoggerProvider(
            IServiceScopeFactory scopeFactory,
            Func<string, LogLevel, bool>? filter = null)
        {
            _scopeFactory = scopeFactory;
            _filter = filter ?? ((_, __) => true);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName,
                name => new SqlLogger(name, _scopeFactory, _filter));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }
}
