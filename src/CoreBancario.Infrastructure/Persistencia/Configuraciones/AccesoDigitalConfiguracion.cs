using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreBancario.Infrastructure.Persistencia.Configuraciones;

public sealed class AccesoDigitalConfiguracion : IEntityTypeConfiguration<AccesoDigital>
{
    public void Configure(EntityTypeBuilder<AccesoDigital> builder)
    {
        builder.ToTable("accesos_digitales");

        // La clave primaria es el propio cliente: un cliente tiene como máximo un vínculo (ADR-0016).
        builder.HasKey(a => a.ClienteId).HasName("pk_accesos_digitales");
        builder.Property(a => a.ClienteId).ValueGeneratedNever();

        // Sin propiedad de navegación: el vínculo referencia al cliente solo por ClienteId.
        builder.HasOne<Cliente>()
            .WithMany()
            .HasForeignKey(a => a.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // El índice único evita que dos clientes compartan el mismo usuario del sistema de identidad.
        builder.Property(a => a.Sub).HasMaxLength(AccesoDigital.LongitudMaximaDelSub);
        builder.HasIndex(a => a.Sub)
            .IsUnique()
            .HasDatabaseName(CoreBancarioDbContext.IndiceUnicoDeSubDeAcceso);

        builder.Property(a => a.FechaDeVinculo);
    }
}
