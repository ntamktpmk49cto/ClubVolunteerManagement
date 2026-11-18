using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubVolunteerManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxParticipantsToEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxParticipants",
                table: "Events",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxParticipants",
                table: "Events");
        }
    }
}
