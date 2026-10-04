using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RadarTalentos.API.Data;

/// <summary>Usado só pelo `dotnet ef` para gerar migrations sem precisar de configuração de runtime.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=radar_design;Username=postgres")
            .Options);
}
