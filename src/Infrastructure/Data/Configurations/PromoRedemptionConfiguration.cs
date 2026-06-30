using greenfield_checkout.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace greenfield_checkout.Infrastructure.Data.Configurations;

public class PromoRedemptionConfiguration : IEntityTypeConfiguration<PromoRedemption>
{
    public void Configure(EntityTypeBuilder<PromoRedemption> builder)
    {
        builder.ToTable("promo_redemptions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Code)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(r => r.UserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.ReservationId)
            .HasMaxLength(64);

        builder.Property(r => r.Result)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(r => r.AmountDiscounted)
            .HasPrecision(10, 2);

        builder.HasIndex(r => new { r.Code, r.UserId });
        builder.HasIndex(r => r.ReservationId);

        // SPEC-2026-0043 slice 2C — no FK to promo_codes on purpose.
        // RN10 demands an immutable trace for EVERY redemption attempt, including those
        // where the code does not exist (escenario "Código inexistente" §6). A relational
        // FK would either reject those inserts or require a sentinel row. The audit log
        // also has to outlive catalogue clean-ups. The (Code, UserId) index keeps the
        // anti-replay lookups fast without coupling the two aggregates at the DB layer.
    }
}
