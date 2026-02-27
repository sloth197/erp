using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSignupApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at_utc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by_user_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "disabled_at_utc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "disabled_by_user_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reject_reason",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at_utc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rejected_by_user_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_users_approved_by_user_id",
                table: "users",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_disabled_by_user_id",
                table: "users",
                column: "disabled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_rejected_by_user_id",
                table: "users",
                column: "rejected_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_status",
                table: "users",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_approved_by_user_id",
                table: "users",
                column: "approved_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_disabled_by_user_id",
                table: "users",
                column: "disabled_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_rejected_by_user_id",
                table: "users",
                column: "rejected_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_users_approved_by_user_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_users_disabled_by_user_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_users_rejected_by_user_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_approved_by_user_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_disabled_by_user_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_rejected_by_user_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_status",
                table: "users");

            migrationBuilder.DropColumn(
                name: "approved_at_utc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "approved_by_user_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "disabled_at_utc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "disabled_by_user_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "reject_reason",
                table: "users");

            migrationBuilder.DropColumn(
                name: "rejected_at_utc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "rejected_by_user_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "status",
                table: "users");
        }
    }
}
