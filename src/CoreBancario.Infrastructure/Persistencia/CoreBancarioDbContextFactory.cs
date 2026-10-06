using Microsoft.EntityFrameworkCore.Design;

namespace CoreBancario.Infrastructure.Persistencia;

/// <summary>Para dotnet ef: lee ConnectionStrings__CoreBancario o usa una cadena de relleno.</summary>
public sealed class CoreBancarioDbContextFactory : IDesignTimeDbContextFactory<CoreBancarioDbContext>
{
    // SDD: esqueleto creado por test-writer
    public CoreBancarioDbContext CreateDbContext(string[] args) => throw new NotImplementedException();
}
