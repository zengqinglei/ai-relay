using Leistd.EventBus.Core.Event;

namespace Leistd.Ddd.Domain.Entities;

public interface IEntity
{
    object?[] GetKeys();
}

public interface IEntity<TKey> : IEntity
{
    TKey Id { get; }
}

[Serializable]
public abstract class Entity : IEntity
{
    private readonly List<ILocalEvent> _localEvents = new();

    public abstract object?[] GetKeys();

    /// <summary>
    /// 添加本地事件
    /// </summary>
    protected void AddLocalEvent(ILocalEvent @event)
    {
        _localEvents.Add(@event);
    }

    /// <summary>
    /// 获取待发布的本地事件（仅供 BaseDbContext 使用）
    /// </summary>
    public IReadOnlyCollection<ILocalEvent> GetLocalEvents() => _localEvents.AsReadOnly();

    /// <summary>
    /// 清空本地事件（仅供 BaseDbContext 使用）
    /// </summary>
    public void ClearLocalEvents() => _localEvents.Clear();

    public override string ToString()
    {
        return $"[ENTITY: {GetType().Name}] Keys = {string.Join(", ", GetKeys())}";
    }
}

/// <inheritdoc cref="IEntity{TKey}" />
public abstract class Entity<TKey> : Entity, IEntity<TKey>
{
    public virtual TKey Id { get; protected set; } = default!;

    protected Entity() { }

    protected Entity(TKey id)
    {
        Id = id;
    }

    public override object?[] GetKeys() => [Id];

    public override string ToString()
    {
        return $"[ENTITY: {GetType().Name}] Id = {Id}";
    }
}