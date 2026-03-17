namespace Leistd.UnitOfWork.Core.Events;

/// <summary>
/// 工作单元事件处理器特性 - 声明处理器在工作单元的哪个阶段执行
/// </summary>
/// <example>
/// <code>
/// [UnitOfWorkEventHandler(Phase = UowPhase.AfterCommit)]
/// public class SendWelcomeEmailHandler : IEventHandler&lt;UserCreatedEvent&gt;
/// {
///     public async Task HandleAsync(UserCreatedEvent @event)
///     {
///         await _emailService.SendWelcomeEmail(@event.User.Email);
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class UnitOfWorkEventHandlerAttribute(UnitOfWorkPhase phase = UnitOfWorkPhase.AfterCommit) : Attribute
{
    /// <summary>
    /// 工作单元阶段（默认：AfterCommit）
    /// </summary>
    public UnitOfWorkPhase Phase { get; set; } = phase;
}
