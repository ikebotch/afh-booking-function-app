using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Infrastructure.Repositories.Interface;

public interface IApplicationLogsRepository
{
    Task AddAsync(ApplicationLogsEntity log, CancellationToken ct = default);
}