using Microsoft.EntityFrameworkCore;

namespace EFImplicitConversion;

public class ImplicitCoversionDbContext : DbContext
{
    public ImplicitCoversionDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ImplicitCoversionDbContext).Assembly);
    }
    
    public DbSet<GoodType> GoodTypes { get; set; }
    public DbSet<BadType> BadTypes { get; set; }

    public string DbPath { get; }
}
