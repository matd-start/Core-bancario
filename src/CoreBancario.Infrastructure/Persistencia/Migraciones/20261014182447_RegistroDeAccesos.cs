using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBancario.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class RegistroDeAccesos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "solicitudes_de_acceso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operador = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    accion = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitudes_de_acceso", x => x.id);
                    table.CheckConstraint("ck_solicitudes_de_acceso_accion", "accion IN ('Habilitar', 'Restablecer', 'Deshabilitar')");
                    table.ForeignKey(
                        name: "fk_solicitudes_de_acceso_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resultados_de_acceso",
                columns: table => new
                {
                    solicitud_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resultado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resultados_de_acceso", x => x.solicitud_id);
                    table.CheckConstraint("ck_resultados_de_acceso_resultado", "resultado IN ('Completado', 'Fallido')");
                    table.ForeignKey(
                        name: "fk_resultados_de_acceso_solicitudes_de_acceso_solicitud_id",
                        column: x => x.solicitud_id,
                        principalTable: "solicitudes_de_acceso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_de_acceso_cliente_id",
                table: "solicitudes_de_acceso",
                column: "cliente_id");

            // RN-20: el registro de accesos es inmutable. El código nunca edita ni borra filas; este trigger
            // lo garantiza en la base (defensa en profundidad) si alguien lo intenta a mano o por error.
            migrationBuilder.Sql("""
                CREATE FUNCTION impedir_cambios_en_registro_de_accesos() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'El registro de accesos es inmutable: no se permite % en %.', TG_OP, TG_TABLE_NAME;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER tr_solicitudes_de_acceso_inmutable
                BEFORE UPDATE OR DELETE ON solicitudes_de_acceso
                FOR EACH ROW EXECUTE FUNCTION impedir_cambios_en_registro_de_accesos();
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER tr_resultados_de_acceso_inmutable
                BEFORE UPDATE OR DELETE ON resultados_de_acceso
                FOR EACH ROW EXECUTE FUNCTION impedir_cambios_en_registro_de_accesos();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER tr_resultados_de_acceso_inmutable ON resultados_de_acceso;");
            migrationBuilder.Sql("DROP TRIGGER tr_solicitudes_de_acceso_inmutable ON solicitudes_de_acceso;");

            migrationBuilder.DropTable(
                name: "resultados_de_acceso");

            migrationBuilder.DropTable(
                name: "solicitudes_de_acceso");

            migrationBuilder.Sql("DROP FUNCTION impedir_cambios_en_registro_de_accesos();");
        }
    }
}
