using AiRelay.Application.UsageRecords.Dtos.Query;
using AiRelay.Domain.UsageRecords.Entities;
using Leistd.ObjectMapping.Mapster;

namespace AiRelay.Application.UsageRecords.Mappings;

public class UsageRecordProfile : MapsterProfile
{
    protected override void ConfigureMappings()
    {
        CreateMap<UsageRecord, UsageRecordOutputDto>();

        CreateMap<UsageRecordAttempt, UsageRecordAttemptOutputDto>()
            .Map(d => d.UpRequestHeaders, s => s.Detail != null ? s.Detail.UpRequestHeaders : null)
            .Map(d => d.UpRequestBody, s => s.Detail != null ? s.Detail.UpRequestBody : null)
            .Map(d => d.UpResponseBody, s => s.Detail != null ? s.Detail.UpResponseBody : null);

        CreateMap<UsageRecord, UsageRecordDetailOutputDto>()
            .Map(d => d.UsageRecordId, s => s.Id)
            .Map(d => d.DownRequestUrl, s => s.DownRequestUrl)
            .Map(d => d.DownRequestHeaders, s => s.Detail != null ? s.Detail.DownRequestHeaders : null)
            .Map(d => d.DownRequestBody, s => s.Detail != null ? s.Detail.DownRequestBody : null)
            .Map(d => d.DownResponseBody, s => s.Detail != null ? s.Detail.DownResponseBody : null)
            .Map(d => d.Attempts, s => s.Attempts);
    }
}
