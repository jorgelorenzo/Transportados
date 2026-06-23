using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Transportados.Persistence.DataAccess;

public sealed class TransportadosDbContextFactory : IDesignTimeDbContextFactory<TransportadosDbContext>
{
    public TransportadosDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TRANSPORTADOS_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
            "Server=(localdb)\\mssqllocaldb;Database=transportados-dev;Trusted_Connection=True;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<TransportadosDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new TransportadosDbContext(optionsBuilder.Options);
    }
}
