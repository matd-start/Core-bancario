using CoreBancario.Domain.AccesosDigitales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreBancario.Infrastructure.Persistencia.Configuraciones;

public sealed class ResultadoDeSolicitudConfiguracion : IEntityTypeConfiguration<ResultadoDeSolicitud>
{
    public void Configure(EntityTypeBuilder<ResultadoDeSolicitud> builder)
    {
        builder.ToTable("resultados_de_acceso", tabla =>
            tabla.HasCheckConstraint(
                "ck_resultados_de_acceso_resultado",
                "resultado IN ('Completado', 'Fallido')"));

        // La clave primaria es la propia solicitud: como máximo un resultado por solicitud (ADR-0017).
        // Dos reconciliaciones simultáneas no pueden cerrar dos veces la misma; la segunda choca con esta PK.
        builder.HasKey(r => r.SolicitudId).HasName("pk_resultados_de_acceso");
        builder.Property(r => r.SolicitudId).ValueGeneratedNever();

        builder.HasOne<SolicitudDeAcceso>()
            .WithOne()
            .HasForeignKey<ResultadoDeSolicitud>(r => r.SolicitudId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.Resultado).HasConversion<string>().HasMaxLength(10);
        builder.Property(r => r.Fecha);
        builder.Property(r => r.Motivo).HasMaxLength(ResultadoDeSolicitud.LongitudMaximaDelMotivo);
    }
}
