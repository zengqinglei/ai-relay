using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiRelay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageStatsToAccountTokenAndApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostToday",
                table: "ApiKeys",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostTotal",
                table: "ApiKeys",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatsDate",
                table: "ApiKeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SuccessToday",
                table: "ApiKeys",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SuccessTotal",
                table: "ApiKeys",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TokensToday",
                table: "ApiKeys",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TokensTotal",
                table: "ApiKeys",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "UsageToday",
                table: "ApiKeys",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "UsageTotal",
                table: "ApiKeys",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "CostToday",
                table: "AccountTokens",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostTotal",
                table: "AccountTokens",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatsDate",
                table: "AccountTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SuccessToday",
                table: "AccountTokens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SuccessTotal",
                table: "AccountTokens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TokensToday",
                table: "AccountTokens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TokensTotal",
                table: "AccountTokens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "UsageToday",
                table: "AccountTokens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "UsageTotal",
                table: "AccountTokens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostToday",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "CostTotal",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "StatsDate",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "SuccessToday",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "SuccessTotal",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "TokensToday",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "TokensTotal",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "UsageToday",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "UsageTotal",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "CostToday",
                table: "AccountTokens");

            migrationBuilder.DropColumn(
                name: "CostTotal",
                table: "AccountTokens");

            migrationBuilder.DropColumn(
                name: "StatsDate",
                table: "AccountTokens");

            migrationBuilder.DropColumn(
                name: "SuccessToday",
                table: "AccountTokens");

            migrationBuilder.DropColumn(
                name: "SuccessTotal",
                table: "AccountTokens");

            migrationBuilder.DropColumn(
                name: "TokensToday",
                table: "AccountTokens");

            migrationBuilder.DropColumn(
                name: "TokensTotal",
                table: "AccountTokens");

            migrationBuilder.DropColumn(
                name: "UsageToday",
                table: "AccountTokens");

            migrationBuilder.DropColumn(
                name: "UsageTotal",
                table: "AccountTokens");
        }
    }
}
