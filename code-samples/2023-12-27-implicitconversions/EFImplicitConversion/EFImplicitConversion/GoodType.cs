using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EFImplicitConversion;

public class GoodType
{
    public int Id { get; set; }
    public string SomeNVarchar { get; set; }
}

public class GoodTypeConfiguration : IEntityTypeConfiguration<GoodType>
{
    public void Configure(EntityTypeBuilder<GoodType> builder)
    {
        builder.ToTable("GoodType");
    }
}