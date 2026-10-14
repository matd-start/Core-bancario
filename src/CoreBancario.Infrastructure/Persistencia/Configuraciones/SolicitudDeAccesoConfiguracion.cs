using CoreBancario.Domain.AccesosDigitales;
using CoreBancario.Domain.Clientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreBancario.Infrastructure.Persistencia.Configuraciones;

public sealed class SolicitudDeAccesoConfiguracion : IEntityTypeConfiguration<SolicitudDeAcceso>
{
    public void Configure(EntityTypeBuilder<SolicitudDeAcceso> builder)
    {
        // El CHECK es defensa en profundidad: la base rechaza una acción que el dominio no conoce.
        builder.ToTable("solicitudes_de_acceso", tabla =>
            tabla.HasCheckConstraint(
                "ck_solicitudes_de_acceso_accion",
                "accion IN ('Habilitar', 'Restablecer', 'Deshabilitar')"));

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        // Sin propiedad de navegación: la solicitud referencia al cliente solo por ClienteId.
        builder.HasOne<Cliente>()
            .WithMany()
            .HasForeignKey(s => s.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.ClienteId).HasDatabaseName("ix_solicitudes_de_acceso_cliente_id");

        builder.Property(s => s.Operador).HasMaxLength(SolicitudDeAcceso.LongitudMaximaDelOperador);
        builder.Property(s => s.Accion).HasConversion<string>().HasMaxLength(12);
        builder.Property(s => s.Fecha);
    }
}
