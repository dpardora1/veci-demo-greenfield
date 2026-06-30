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

        builder.HasOne<PromoCode>()
            .WithMany()
            .HasForeignKey(r => r.Code)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
