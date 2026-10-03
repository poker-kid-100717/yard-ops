using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Yard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialYard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GateEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrailerNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrailerNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    Tires = table.Column<bool>(type: "bit", nullable: false),
                    Lights = table.Column<bool>(type: "bit", nullable: false),
                    DoorsAndSeal = table.Column<bool>(type: "bit", nullable: false),
                    Floor = table.Column<bool>(type: "bit", nullable: false),
                    ReeferUnit = table.Column<bool>(type: "bit", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Moves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrailerNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FromSpot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ToSpot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TrailerNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Spots",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spots", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Trailers",
                columns: table => new
                {
                    TrailerNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Equipment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PalletCapacity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SpotCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CurrentLoadNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Carrier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HoldReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trailers", x => x.TrailerNumber);
                    table.ForeignKey(
                        name: "FK_Trailers_Spots_SpotCode",
                        column: x => x.SpotCode,
                        principalTable: "Spots",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_OccurredAt",
                table: "GateEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_GateEvents_TrailerNumber_OccurredAt",
                table: "GateEvents",
                columns: new[] { "TrailerNumber", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_OccurredAt",
                table: "Inspections",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_TrailerNumber_OccurredAt",
                table: "Inspections",
                columns: new[] { "TrailerNumber", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Moves_TrailerNumber_OccurredAt",
                table: "Moves",
                columns: new[] { "TrailerNumber", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_SentAt_NextAttemptAt",
                table: "Outbox",
                columns: new[] { "SentAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Trailers_SpotCode",
                table: "Trailers",
                column: "SpotCode",
                unique: true,
                filter: "[SpotCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Trailers_Status",
                table: "Trailers",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GateEvents");

            migrationBuilder.DropTable(
                name: "Inspections");

            migrationBuilder.DropTable(
                name: "Moves");

            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "Trailers");

            migrationBuilder.DropTable(
                name: "Spots");
        }
    }
}
