using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Interface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Logging
{
    public sealed class SqlLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Func<string, LogLevel, bool> _filter;

        public SqlLogger(
            string categoryName,
            IServiceScopeFactory scopeFactory,
            Func<string, LogLevel, bool> filter)
        {
            _categoryName = categoryName;
            _scopeFactory = scopeFactory;
            _filter = filter;
        }

        public IDisposable BeginScope<TState>(TState state) => default!;

        public bool IsEnabled(LogLevel logLevel)
            => _filter(_categoryName, logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            if (formatter == null) return;

            var message = formatter(state, exception);

            // Attempt to extract structured state as JSON if possible
            string? payloadJson = null;
            string? eventType = null;
            string? correlationId = null;
            string? requestId = null;

            if (state is IEnumerable<KeyValuePair<string, object>> kvpState)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var kv in kvpState)
                {
                    dict[kv.Key] = kv.Value;

                    if (kv.Key.Equals("EventType", StringComparison.OrdinalIgnoreCase))
                        eventType = kv.Value?.ToString();

                    if (kv.Key.Equals("CorrelationId", StringComparison.OrdinalIgnoreCase))
                        correlationId = kv.Value?.ToString();

                    if (kv.Key.Equals("RequestId", StringComparison.OrdinalIgnoreCase))
                        requestId = kv.Value?.ToString();
                }

                try
                {
                    payloadJson = JsonSerializer.Serialize(dict);
                }
                catch
                {
                    // ignore serialization issues – we still log the message
                }
            }

            var entity = new ApplicationLogsEntity
            {
                TimestampUtc = DateTime.UtcNow,
                FunctionName = _categoryName,
                LogLevel = logLevel.ToString(),
                Message = message,
                ExceptionMessage = exception?.Message,
                ExceptionStack = exception?.ToString(),
                CorrelationId = correlationId,
                RequestId = requestId,
                EventType = eventType,
                PayloadJson = payloadJson
            };

            // Fire-and-forget, don’t block the logging call
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IApplicationLogsRepository>();
                    await repo.AddAsync(entity);
                }
                catch
                {
                    // swallow – never let logging throw
                }
            });
        }
    }
}
