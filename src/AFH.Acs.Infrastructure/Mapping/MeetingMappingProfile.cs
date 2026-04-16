using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Models;
using AutoMapper;
using System;

namespace AFH.Acs.Recorder.Infrastructure.Mapping;

public class MeetingMappingProfile : Profile
{
    public MeetingMappingProfile()
    {
        // ============================
        // MeetingEntity ↔ MeetingDetailsDto
        // ============================
        CreateMap<MeetingEntity, MeetingDetailsDto>()
            .ForMember(d => d.Start,
                opt => opt.MapFrom(s => new DateTimeOffset(s.StartUtc, TimeSpan.Zero)))
            .ForMember(d => d.End,
                opt => opt.MapFrom(s => new DateTimeOffset(s.EndUtc, TimeSpan.Zero)))
            .ForMember(d => d.AdviserName,
                opt => opt.MapFrom(s =>
                    s.Adviser == null
                        ? null
                        : s.Adviser.FullName))
            .ForMember(d => d.ClientName,
                opt => opt.MapFrom(s =>
                    s.Lead == null
                        ? null
                        : s.Lead.ClientName))
            .ForMember(d => d.ConsentTimestampUtc,
                opt => opt.MapFrom(s =>
                    s.ConsentTimestampUtc == null
                        ? (DateTimeOffset?)null
                        : new DateTimeOffset(s.ConsentTimestampUtc.Value, TimeSpan.Zero)))
            .ForMember(d => d.Attendees,
                opt => opt.MapFrom(s => s.Attendees))
            .ForMember(d => d.Recordings,
                opt => opt.MapFrom(s => s.Recordings))
            .ForMember(d => d.Transcription,
                opt => opt.MapFrom(s => s.Transcription));

        CreateMap<MeetingDetailsDto, MeetingEntity>()
            .ForMember(d => d.StartUtc,
                opt => opt.MapFrom(s => s.Start.UtcDateTime))
            .ForMember(d => d.EndUtc,
                opt => opt.MapFrom(s => s.End.UtcDateTime))
            .ForMember(d => d.ConsentTimestampUtc,
                opt => opt.MapFrom(s =>
                    s.ConsentTimestampUtc == null
                        ? (DateTime?)null
                        : s.ConsentTimestampUtc.Value.UtcDateTime))
            // navigation + system fields handled in services/repos
            .ForMember(d => d.Adviser, opt => opt.Ignore())
            .ForMember(d => d.Lead, opt => opt.Ignore())
            .ForMember(d => d.Attendees, opt => opt.Ignore())
            .ForMember(d => d.Recordings, opt => opt.Ignore())
            .ForMember(d => d.Transcription, opt => opt.Ignore())
            .ForMember(d => d.AtrAnalysis, opt => opt.Ignore())
            .ForMember(d => d.CreatedAtUtc, opt => opt.Ignore())
            .ForMember(d => d.UpdatedAtUtc, opt => opt.Ignore())
            .ForMember(d => d.GraphEventId, opt => opt.Ignore());

        // ============================
        // MeetingScheduleRequest → MeetingEntity
        // (used when booking a new meeting)
        // ============================
        CreateMap<MeetingScheduleRequest, MeetingEntity>()
            .ForMember(d => d.MeetingId, opt => opt.Ignore())
            .ForMember(d => d.GroupId, opt => opt.Ignore())
            .ForMember(d => d.GraphEventId, opt => opt.Ignore())
            .ForMember(d => d.StartUtc,
                opt => opt.MapFrom(s => s.Start.UtcDateTime))
            .ForMember(d => d.EndUtc,
                opt => opt.MapFrom(s => s.End.UtcDateTime))
            .ForMember(d => d.ClientEmail,
                opt => opt.MapFrom(s => s.ClientEmail))
            .ForMember(d => d.Status,
                opt => opt.MapFrom(_ => "SCHEDULED"))
            .ForMember(d => d.ConsentToRecording,
                opt => opt.MapFrom(_ => false))
            .ForMember(d => d.ConsentTimestampUtc,
                opt => opt.MapFrom(_ => (DateTime?)null))
            .ForMember(d => d.CreatedAtUtc, opt => opt.Ignore())
            .ForMember(d => d.UpdatedAtUtc, opt => opt.Ignore())
            .ForMember(d => d.Adviser, opt => opt.Ignore())
            .ForMember(d => d.Lead, opt => opt.Ignore())
            .ForMember(d => d.Attendees, opt => opt.Ignore())
            .ForMember(d => d.Recordings, opt => opt.Ignore())
            .ForMember(d => d.Transcription, opt => opt.Ignore())
            .ForMember(d => d.AtrAnalysis, opt => opt.Ignore());

        // ============================
        // MeetingEntity → MeetingScheduleResponse
        // (used to return booking result)
        // ============================
        CreateMap<MeetingEntity, MeetingScheduleResponse>()
            .ForMember(d => d.Start,
                opt => opt.MapFrom(s => new DateTimeOffset(s.StartUtc, TimeSpan.Zero)))
            .ForMember(d => d.End,
                opt => opt.MapFrom(s => new DateTimeOffset(s.EndUtc, TimeSpan.Zero)))
            .ForMember(d => d.ClientName,
                opt => opt.MapFrom(s =>
                    s.Lead == null
                        ? null
                        : s.Lead.ClientName))
            // these are built by the service (ACS + frontend)
            .ForMember(d => d.ClientJoinUrl, opt => opt.Ignore())
            .ForMember(d => d.AdviserJoinUrl, opt => opt.Ignore())
            .ForMember(d => d.JoinCode, opt => opt.Ignore());

        // ============================
        // MeetingAttendeeEntity ↔ MeetingAttendeeDto
        // ============================
        CreateMap<MeetingAttendeeEntity, MeetingAttendeeDto>()
            .ForMember(d => d.ResponseTimeUtc,
                opt => opt.MapFrom(s =>
                    s.ResponseTimeUtc.HasValue
                        ? new DateTimeOffset(s.ResponseTimeUtc.Value, TimeSpan.Zero)
                        : (DateTimeOffset?)null));

        CreateMap<MeetingAttendeeDto, MeetingAttendeeEntity>()
            .ForMember(d => d.ResponseTimeUtc,
                opt => opt.MapFrom(s =>
                    s.ResponseTimeUtc == null
                        ? (DateTime?)null
                        : s.ResponseTimeUtc.Value.UtcDateTime))
            .ForMember(d => d.Meeting, opt => opt.Ignore());

        // ============================
        // MeetingRecordingEntity ↔ MeetingRecordingDto
        // ============================
        CreateMap<MeetingRecordingEntity, MeetingRecordingDto>()
       .ForMember(d => d.RecordingStartUtc,
           opt => opt.MapFrom(s =>
               new DateTimeOffset(s.RecordingStartUtc, TimeSpan.Zero)))

       .ForMember(d => d.RecordingEndUtc,
           opt => opt.MapFrom(s =>
               s.RecordingEndUtc.HasValue
                   ? new DateTimeOffset(s.RecordingEndUtc.Value, TimeSpan.Zero)
                   : (DateTimeOffset?)null));

        CreateMap<MeetingRecordingDto, MeetingRecordingEntity>()
            .ForMember(d => d.RecordingStartUtc,
                opt => opt.MapFrom(s => s.RecordingStartUtc.UtcDateTime))
            .ForMember(d => d.RecordingEndUtc,
                opt => opt.MapFrom(s => s.RecordingEndUtc.UtcDateTime))
            .ForMember(d => d.Meeting, opt => opt.Ignore())
            .ForMember(d => d.GroupId, opt => opt.Ignore())
            .ForMember(d => d.CreatedAtUtc, opt => opt.Ignore());

        // ============================
        // MeetingTranscriptionEntity ↔ MeetingTranscriptionDto
        // ============================
        CreateMap<MeetingTranscriptionEntity, MeetingTranscriptionDto>();

        CreateMap<MeetingTranscriptionDto, MeetingTranscriptionEntity>()
            .ForMember(d => d.Meeting, opt => opt.Ignore())
            .ForMember(d => d.Recording, opt => opt.Ignore())
            .ForMember(d => d.MeetingId, opt => opt.Ignore())
            .ForMember(d => d.RecordingId, opt => opt.Ignore())
            .ForMember(d => d.RawJson, opt => opt.Ignore())
            .ForMember(d => d.CreatedAtUtc, opt => opt.Ignore());
    }
}