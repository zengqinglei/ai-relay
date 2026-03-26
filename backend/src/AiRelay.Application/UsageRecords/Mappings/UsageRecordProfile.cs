using AiRelay.Application.UsageRecords.Dtos.Query;
using AiRelay.Domain.UsageRecords.Entities;
using AutoMapper;

namespace AiRelay.Application.UsageRecords.Mappings;

public class UsageRecordProfile : Profile
{
    public UsageRecordProfile()
    {
        CreateMap<UsageRecord, UsageRecordOutputDto>();

        CreateMap<UsageRecordAttempt, UsageRecordAttemptOutputDto>()
            .ForMember(d => d.UpRequestHeaders, opt => opt.MapFrom(s => s.Detail != null ? s.Detail.UpRequestHeaders : null))
            .ForMember(d => d.UpRequestBody, opt => opt.MapFrom(s => s.Detail != null ? s.Detail.UpRequestBody : null))
            .ForMember(d => d.UpResponseBody, opt => opt.MapFrom(s => s.Detail != null ? s.Detail.UpResponseBody : null));

        CreateMap<UsageRecord, UsageRecordDetailOutputDto>()
            .ForMember(d => d.UsageRecordId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.DownRequestUrl, opt => opt.MapFrom(s => s.DownRequestUrl))
            .ForMember(d => d.DownRequestHeaders, opt => opt.MapFrom(s => s.Detail != null ? s.Detail.DownRequestHeaders : null))
            .ForMember(d => d.DownRequestBody, opt => opt.MapFrom(s => s.Detail != null ? s.Detail.DownRequestBody : null))
            .ForMember(d => d.DownResponseBody, opt => opt.MapFrom(s => s.Detail != null ? s.Detail.DownResponseBody : null))
            .ForMember(d => d.Attempts, opt => opt.MapFrom(s => s.Attempts));
    }
}
