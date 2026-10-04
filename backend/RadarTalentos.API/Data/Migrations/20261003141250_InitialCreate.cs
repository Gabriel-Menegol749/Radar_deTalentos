using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadarTalentos.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CurrentCompany = table.Column<string>(type: "text", nullable: true),
                    CurrentPosition = table.Column<string>(type: "text", nullable: true),
                    Salary = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    LinkedIn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    PhoneOriginal = table.Column<string>(type: "text", nullable: true),
                    DiversityTags = table.Column<List<string>>(type: "text[]", nullable: false),
                    Restrictions = table.Column<List<string>>(type: "text[]", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Options", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CandidateOffLimits",
                columns: table => new
                {
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateOffLimits", x => new { x.CandidateId, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_CandidateOffLimits_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateOffLimits_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Modality = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TravelAvailability = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Positions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OpenedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReopenedFromJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_Jobs_ReopenedFromJobId",
                        column: x => x.ReopenedFromJobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jobs_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jobs_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CurrentStage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RejectionReasons = table.Column<List<string>>(type: "text[]", nullable: false),
                    OfferedSalary = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    RequestedSalary = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    AccessedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    OffLimitsOverride = table.Column<bool>(type: "boolean", nullable: false),
                    OffLimitsOverrideByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OffLimitsOverrideAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Applications_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationHistory_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationHistory_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationHistory_ActorUserId",
                table: "ApplicationHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationHistory_ApplicationId",
                table: "ApplicationHistory",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CandidateId_JobId",
                table: "Applications",
                columns: new[] { "CandidateId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_JobId",
                table: "Applications",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Status",
                table: "Applications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateOffLimits_CompanyId",
                table: "CandidateOffLimits",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_LinkedIn",
                table: "Candidates",
                column: "LinkedIn",
                unique: true,
                filter: "\"LinkedIn\" IS NOT NULL AND \"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_Name",
                table: "Candidates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_NormalizedName",
                table: "Companies",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Code",
                table: "Jobs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_OwnerUserId",
                table: "Jobs",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_PositionId_Sequence",
                table: "Jobs",
                columns: new[] { "PositionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ReopenedFromJobId",
                table: "Jobs",
                column: "ReopenedFromJobId");

            migrationBuilder.CreateIndex(
                name: "IX_Options_Category_Value",
                table: "Options",
                columns: new[] { "Category", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_CompanyId_NormalizedName",
                table: "Positions",
                columns: new[] { "CompanyId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationHistory");

            migrationBuilder.DropTable(
                name: "CandidateOffLimits");

            migrationBuilder.DropTable(
                name: "Options");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "Candidates");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
