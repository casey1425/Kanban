using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Kanban.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Labels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CardLabel",
                columns: table => new
                {
                    CardsId = table.Column<int>(type: "INTEGER", nullable: false),
                    LabelsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardLabel", x => new { x.CardsId, x.LabelsId });
                    table.ForeignKey(
                        name: "FK_CardLabel_Cards_CardsId",
                        column: x => x.CardsId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardLabel_Labels_LabelsId",
                        column: x => x.LabelsId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Labels",
                columns: new[] { "Id", "Color", "Name" },
                values: new object[,]
                {
                    { 1, "#E5484D", "긴급" },
                    { 2, "#F76B15", "중요" },
                    { 3, "#3E63DD", "기능" },
                    { 4, "#8E4EC6", "버그" },
                    { 5, "#30A46C", "문서" },
                    { 6, "#B7791F", "아이디어" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardLabel_LabelsId",
                table: "CardLabel",
                column: "LabelsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardLabel");

            migrationBuilder.DropTable(
                name: "Labels");
        }
    }
}
