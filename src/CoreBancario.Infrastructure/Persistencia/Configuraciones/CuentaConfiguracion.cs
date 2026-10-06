using CoreBancario.Domain.Clientes;
using CoreBancario.Domain.Cuentas;
using CoreBancario.Domain.Monetario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreBancario.Infrastructure.Persistencia.Configuraciones;

public sealed class CuentaConfiguracion : IEntityTypeConfiguration<Cuenta>
{
    public void Configure(EntityTypeBuilder<Cuenta> builder)
    {
        // Los CHECK son defensa en profundidad: si el dominio tuviera un error, la base lo rechaza en vez de
        // guardarlo. Su SQL usa los nombres reales de las columnas (snake_case).
        builder.ToTable("cuentas", tabla =>
        {
            tabla.HasCheckConstraint("ck_cuentas_saldo_no_negativo", "saldo >= 0");
            tabla.HasCheckConstraint("ck_cuentas_saldo_en_moneda_de_la_cuenta", "saldo_moneda = moneda");
            tabla.HasCheckConstraint(
                "ck_cuentas_fecha_cierre_solo_si_cerrada",
                "(estado = 'Cerrada') = (fecha_cierre IS NOT NULL)");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Numero)
            .HasConversion(v => v.Valor, s => NumeroDeCuenta.Crear(s))
            .HasMaxLength(10);
        builder.HasIndex(c => c.Numero)
            .IsUnique()
            .HasDatabaseName(CoreBancarioDbContext.IndiceUnicoDeNumeroDeCuenta);

        // Sin propiedad de navegación: Cuenta referencia al cliente solo por ClienteId.
        builder.HasOne<Cliente>()
            .WithMany()
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Moneda)
            .HasConversion(v => v.Codigo, s => Moneda.DesdeCodigo(s))
            .HasMaxLength(3);

        builder.Property(c => c.Estado)
            .HasConversion<string>()
            .HasMaxLength(10);

        // Dinero es un complex type de dos propiedades: monto y moneda se guardan en dos columnas.
        builder.ComplexProperty(c => c.Saldo, saldo =>
        {
            saldo.Property(d => d.Monto)
                .HasColumnName("saldo")
                .HasPrecision(19, 2);

            saldo.Property(d => d.Moneda)
                .HasColumnName("saldo_moneda")
                .HasConversion(v => v.Codigo, s => Moneda.DesdeCodigo(s))
                .HasMaxLength(3);
        });

        builder.Property(c => c.FechaApertura);
        builder.Property(c => c.FechaCierre);

        // Concurrencia optimista por fila con xmin (ADR-0012): columna de sistema de PostgreSQL que cambia
        // en cada UPDATE. Es un detalle de persistencia: el dominio no la ve (propiedad sombra).
        builder.Property<uint>("Version")
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}
