using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auth_sessions",
                columns: table => new
                {
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    team = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_sessions", x => x.session_id);
                });

            migrationBuilder.CreateTable(
                name: "team_progress",
                columns: table => new
                {
                    team_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    current_step = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_progress", x => x.team_id);
                });

            migrationBuilder.CreateTable(
                name: "timer_states",
                columns: table => new
                {
                    team_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    manual_timer_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    manual_timer_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    manual_timer_accumulated_seconds = table.Column<int>(type: "integer", nullable: false),
                    timer_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timer_states", x => x.team_name);
                });

            migrationBuilder.CreateTable(
                name: "challenge_times",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    team_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    challenge_number = table.Column<int>(type: "integer", nullable: false),
                    seconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_challenge_times", x => x.id);
                    table.ForeignKey(
                        name: "FK_challenge_times_timer_states_team_name",
                        column: x => x.team_name,
                        principalTable: "timer_states",
                        principalColumn: "team_name",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_username",
                table: "auth_sessions",
                column: "username");

            migrationBuilder.CreateIndex(
                name: "IX_challenge_times_team_name_challenge_number",
                table: "challenge_times",
                columns: new[] { "team_name", "challenge_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_sessions");

            migrationBuilder.DropTable(
                name: "challenge_times");

            migrationBuilder.DropTable(
                name: "team_progress");

            migrationBuilder.DropTable(
                name: "timer_states");
        }
    }
}
