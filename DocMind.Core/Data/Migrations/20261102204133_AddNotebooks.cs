using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMind.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotebooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_UserId_Sha256",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_UserId",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_UserId_UpdatedAt",
                table: "Conversations");

            migrationBuilder.CreateTable(
                name: "Notebooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notebooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notebooks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Written by hand: EF would add these columns as NOT NULL with an empty Guid, which breaks
            // the foreign keys below as soon as a row exists. So they start out nullable, each user's
            // existing documents and conversations move into one notebook, and only then do the
            // columns become required. Everyone keeps what they had and answers don't change, since
            // a user's single notebook holds all of their documents.
            migrationBuilder.AddColumn<Guid>(
                name: "NotebookId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NotebookId",
                table: "DocumentChunks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NotebookId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO "Notebooks" ("Id", "UserId", "Title", "CreatedAt")
                SELECT gen_random_uuid(), "Id", 'My notebook', now()
                FROM "AspNetUsers";

                UPDATE "Documents" AS document
                SET "NotebookId" = notebook."Id"
                FROM "Notebooks" AS notebook
                WHERE notebook."UserId" = document."UserId";

                UPDATE "DocumentChunks" AS chunk
                SET "NotebookId" = document."NotebookId"
                FROM "Documents" AS document
                WHERE document."Id" = chunk."DocumentId";

                UPDATE "Conversations" AS conversation
                SET "NotebookId" = notebook."Id"
                FROM "Notebooks" AS notebook
                WHERE notebook."UserId" = conversation."UserId";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "NotebookId",
                table: "Documents",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "NotebookId",
                table: "DocumentChunks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "NotebookId",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_NotebookId_Sha256",
                table: "Documents",
                columns: new[] { "NotebookId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId",
                table: "Documents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_NotebookId",
                table: "DocumentChunks",
                column: "NotebookId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_NotebookId_UpdatedAt",
                table: "Conversations",
                columns: new[] { "NotebookId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UserId",
                table: "Conversations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_UserId_CreatedAt",
                table: "Notebooks",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Notebooks_NotebookId",
                table: "Conversations",
                column: "NotebookId",
                principalTable: "Notebooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Notebooks_NotebookId",
                table: "Documents",
                column: "NotebookId",
                principalTable: "Notebooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The unique index on (UserId, Sha256) comes back at the end: this fails if a user has the
            // same file in two notebooks, which the old schema can't represent.
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Notebooks_NotebookId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Notebooks_NotebookId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "Notebooks");

            migrationBuilder.DropIndex(
                name: "IX_Documents_NotebookId_Sha256",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UserId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_NotebookId",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_NotebookId_UpdatedAt",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_UserId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "NotebookId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "NotebookId",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "NotebookId",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId_Sha256",
                table: "Documents",
                columns: new[] { "UserId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_UserId",
                table: "DocumentChunks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UserId_UpdatedAt",
                table: "Conversations",
                columns: new[] { "UserId", "UpdatedAt" });
        }
    }
}
