using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Interface;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Infrastructure.Repositories;

public class ApplicationLogsRepositoryEf : IApplicationLogsRepository
{
    private readonly MeetingDbContext _db;
    private readonly ILogger<ApplicationLogsRepositoryEf> _logger;

    public ApplicationLogsRepositoryEf(MeetingDbContext db, ILogger<ApplicationLogsRepositoryEf> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AddAsync(ApplicationLogsEntity log, CancellationToken ct = default)
    {
        try
        {
            await _db.ApplicationLogs.AddAsync(log, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Last-resort logging – do NOT throw or you’ll risk log loops
            _logger.LogError(ex, "Failed to persist FunctionLogEntity.");
        }
    }
}