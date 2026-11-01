using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDoneList",
                table: "Lists",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WipLimit",
                table: "Lists",
                type: "INTEGER",
                nullable: true);

            // 기존 데이터 이전: 기본 보드 구성의 "Done" 리스트는 완료 리스트로 표시한다.
            // (이름이 정확히 Done인 것만, 대소문자 무시. 그 외 리스트는 기본값 그대로 둔다.)
            migrationBuilder.Sql("UPDATE Lists SET IsDoneList = 1 WHERE lower(Name) = 'done';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDoneList",
                table: "Lists");

            migrationBuilder.DropColumn(
                name: "WipLimit",
                table: "Lists");
        }
    }
}
