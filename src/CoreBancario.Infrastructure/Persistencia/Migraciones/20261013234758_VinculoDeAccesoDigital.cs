using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBancario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class VinculoDeAccesoDigital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accesos_digitales",
                columns: table => new
                {
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    fecha_de_vinculo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accesos_digitales", x => x.cliente_id);
                    table.ForeignKey(
                        name: "fk_accesos_digitales_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_accesos_digitales_sub",
                table: "accesos_digitales",
                column: "sub",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accesos_digitales");
        }
    }
}
