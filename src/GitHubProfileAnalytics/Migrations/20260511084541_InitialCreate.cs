using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitHubProfileAnalytics.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.CreateTable(
            name: "AnalyticsCaches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GitHubUserName = table.Column<string>(type: "text", nullable: false),
                Data = table.Column<string>(type: "text", nullable: false),
                CachedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table => _ = table.PrimaryKey("PK_AnalyticsCaches", x => x.Id)
        );

        _ = migrationBuilder.CreateTable(
            name: "ProfileCaches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GitHubUserName = table.Column<string>(type: "text", nullable: false),
                Data = table.Column<string>(type: "text", nullable: false),
                CachedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table => _ = table.PrimaryKey("PK_ProfileCaches", x => x.Id)
        );

        _ = migrationBuilder.CreateTable(
            name: "SearchHistories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                GitHubUserName = table.Column<string>(type: "text", nullable: false),
                SearchedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table => _ = table.PrimaryKey("PK_SearchHistories", x => x.Id)
        );

        _ = migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "text", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table => _ = table.PrimaryKey("PK_Users", x => x.Id)
        );

        _ = migrationBuilder.CreateTable(
            name: "RefreshTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Token = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                CreatedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                RevokedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_RefreshTokens_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        _ = migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_UserId",
            table: "RefreshTokens",
            column: "UserId"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.DropTable(name: "AnalyticsCaches");

        _ = migrationBuilder.DropTable(name: "ProfileCaches");

        _ = migrationBuilder.DropTable(name: "RefreshTokens");

        _ = migrationBuilder.DropTable(name: "SearchHistories");

        _ = migrationBuilder.DropTable(name: "Users");
    }
}
