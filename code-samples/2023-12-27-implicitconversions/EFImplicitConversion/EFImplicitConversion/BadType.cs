using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EFImplicitConversion;

public class BadType
{
    public int Id { get; set; }
    public string SomeVarchar { get; set; }
}

public class BadTypeConfiguration : IEntityTypeConfiguration<BadType>
{
    public void Configure(EntityTypeBuilder<BadType> builder)
    {
        builder.ToTable("BadType");
    }
}