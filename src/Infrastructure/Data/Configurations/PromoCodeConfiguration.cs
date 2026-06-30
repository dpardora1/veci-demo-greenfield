using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace greenfield_checkout.Infrastructure.Data.Configurations;

public class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.ToTable("promo_codes");

        builder.HasKey(p => p.Code);

        builder.Property(p => p.Code)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Value)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(p => p.MaxDiscount)
            .HasPrecision(10, 2);

        builder.Property(p => p.ValidFrom).IsRequired();
        builder.Property(p => p.ValidTo).IsRequired();
        builder.Property(p => p.MaxTotalRedemptions).IsRequired();
        builder.Property(p => p.TotalRedemptions).IsRequired();
    }
}
