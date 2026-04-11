namespace DealMe.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class DealMeDbContextFactory : IDesignTimeDbContextFactory<DealMeDbContext>
{
    public DealMeDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("DATABASE_PROVIDER") ?? "sqlite";
        var connStr = Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? "Data Source=/tmp/dealme-design.db";

        var opts = new DbContextOptionsBuilder<DealMeDbContext>();

        if (provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
            opts.UseNpgsql(connStr);
        else
            opts.UseSqlite(connStr);

        return new DealMeDbContext(opts.Options);
    }
}
