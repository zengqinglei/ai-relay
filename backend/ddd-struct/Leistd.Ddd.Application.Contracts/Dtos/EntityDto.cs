namespace Leistd.Ddd.Application.Contracts.Dtos;

/// <summary>
/// 实体 DTO 基类（可选）
/// </summary>
public abstract record EntityDto<TKey>
{
    public required TKey Id { get; init; }
}

public abstract record EntityDto : EntityDto<Guid>
{

}