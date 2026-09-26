// <copyright file="20260925232025_AddGameConfigurationClassAwareDrops.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddGameConfigurationClassAwareDrops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClassAwareDropMode",
                schema: "config",
                table: "GameConfiguration",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "ClassAwareDropWeight",
                schema: "config",
                table: "GameConfiguration",
                type: "real",
                nullable: false,
                defaultValue: 1f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassAwareDropMode",
                schema: "config",
                table: "GameConfiguration");

            migrationBuilder.DropColumn(
                name: "ClassAwareDropWeight",
                schema: "config",
                table: "GameConfiguration");
        }
    }
}
