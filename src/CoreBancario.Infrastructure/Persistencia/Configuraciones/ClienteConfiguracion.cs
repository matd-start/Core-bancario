using CoreBancario.Domain.Clientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreBancario.Infrastructure.Persistencia.Configuraciones;

public sealed class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("clientes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // Owned type y no complex type: EF Core 10 no admite índices sobre complex types, y el índice único
        // es justo lo que protege RN-17. El owned vive en la misma tabla.
        builder.OwnsOne(c => c.Documento, documento =>
        {
            documento.Property(d => d.Tipo)
                .HasColumnName("tipo_documento")
                .HasConversion<string>()
                .HasMaxLength(2);

            documento.Property(d => d.Numero)
                .HasColumnName("numero_documento")
                .HasMaxLength(15);

            documento.HasIndex(d => new { d.Tipo, d.Numero })
                .IsUnique()
                .HasDatabaseName(CoreBancarioDbContext.IndiceUnicoDeDocumento);
        });
        builder.Navigation(c => c.Documento).IsRequired();

        // Los datos guardados ya pasaron las reglas de cada value object: al leer se usa Crear.
        builder.Property(c => c.Nombres)
            .HasConversion(v => v.Valor, s => NombreDePersona.Crear(s))
            .HasMaxLength(100);

        builder.Property(c => c.Apellidos)
            .HasConversion(v => v.Valor, s => NombreDePersona.Crear(s))
            .HasMaxLength(100);

        builder.Property(c => c.Correo)
            .HasConversion(v => v.Valor, s => Correo.Crear(s))
            .HasMaxLength(254);

        builder.Property(c => c.Telefono)
            .HasConversion(v => v.Valor, s => Telefono.Crear(s))
            .HasMaxLength(16);

        builder.Property(c => c.FechaRegistro);
    }
}
