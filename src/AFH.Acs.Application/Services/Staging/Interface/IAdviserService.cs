using AFH.Acs.Recorder.DTOs;

namespace AFH.Acs.Recorder.Services.Interface;

public interface IAdviserService
{
    Task<IReadOnlyList<AdviserListItemDto>> SearchAdvisersAsync(
        string? region,
        bool leadTechOnly,
        CancellationToken ct = default);

    Task<AdviserListItemDto?> GetAdviserAsync(
        string adviserId,
        CancellationToken ct = default);


    Task<IReadOnlyList<AdviserListItemDto>> SearchAdvisersFromSharePointAsync(
        string? region,
        bool leadTechOnly,
        CancellationToken ct = default);
}