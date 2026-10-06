using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBancario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    numero_documento = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    nombres = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    apellidos = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    telefono = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    fecha_registro = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cuentas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    estado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    fecha_apertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    saldo_moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    saldo = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuentas", x => x.id);
                    table.CheckConstraint("ck_cuentas_fecha_cierre_solo_si_cerrada", "(estado = 'Cerrada') = (fecha_cierre IS NOT NULL)");
                    table.CheckConstraint("ck_cuentas_saldo_en_moneda_de_la_cuenta", "saldo_moneda = moneda");
                    table.CheckConstraint("ck_cuentas_saldo_no_negativo", "saldo >= 0");
                    table.ForeignKey(
                        name: "fk_cuentas_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_clientes_documento",
                table: "clientes",
                columns: new[] { "tipo_documento", "numero_documento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cuentas_cliente_id",
                table: "cuentas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ux_cuentas_numero",
                table: "cuentas",
                column: "numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cuentas");

            migrationBuilder.DropTable(
                name: "clientes");
        }
    }
}
