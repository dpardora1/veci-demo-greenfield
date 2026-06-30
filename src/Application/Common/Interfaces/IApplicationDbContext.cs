using greenfield_checkout.Domain.Entities;

namespace greenfield_checkout.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TodoList> TodoLists { get; }

    DbSet<TodoItem> TodoItems { get; }

    DbSet<PromoCode> PromoCodes { get; }

    DbSet<PromoRedemption> PromoRedemptions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
