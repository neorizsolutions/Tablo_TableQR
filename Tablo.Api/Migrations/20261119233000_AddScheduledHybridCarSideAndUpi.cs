using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tablo.Api.Migrations
{
    public partial class AddScheduledHybridCarSideAndUpi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Orders table
            migrationBuilder.AddColumn<string>(
                name: "OrderSource",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsScheduled",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledPickupTimeUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduledPickupTimeLocal",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreparationLeadMinutes",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledReleaseTimeUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiclePlate",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleModel",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParkingBay",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveredByEmployeeId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            // 2. OrderItems table
            migrationBuilder.AddColumn<string>(
                name: "PackingIntent",
                table: "OrderItems",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DineIn");

            migrationBuilder.AddColumn<decimal>(
                name: "PackagingFeeApplied",
                table: "OrderItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPackagingWaived",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PackagingWaiverReason",
                table: "OrderItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackagingWaivedBy",
                table: "OrderItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // 3. TableBills table
            migrationBuilder.AddColumn<decimal>(
                name: "LeftoverPackingCharge",
                table: "TableBills",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VehiclePlate",
                table: "TableBills",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParkingBay",
                table: "TableBills",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // 4. RestaurantSettings table
            migrationBuilder.AddColumn<bool>(
                name: "ScheduledPickupEnabled",
                table: "RestaurantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPreparationLeadMinutes",
                table: "RestaurantSettings",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<bool>(
                name: "CarSideOrderingEnabled",
                table: "RestaurantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "HybridDineInParcelEnabled",
                table: "RestaurantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LeftoverContainerCharge",
                table: "RestaurantSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<bool>(
                name: "UpiQrEnabled",
                table: "RestaurantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UpiVpa",
                table: "RestaurantSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpiPayeeName",
                table: "RestaurantSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpiMerchantCode",
                table: "RestaurantSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OrderSource", table: "Orders");
            migrationBuilder.DropColumn(name: "IsScheduled", table: "Orders");
            migrationBuilder.DropColumn(name: "ScheduledPickupTimeUtc", table: "Orders");
            migrationBuilder.DropColumn(name: "ScheduledPickupTimeLocal", table: "Orders");
            migrationBuilder.DropColumn(name: "PreparationLeadMinutes", table: "Orders");
            migrationBuilder.DropColumn(name: "ScheduledReleaseTimeUtc", table: "Orders");
            migrationBuilder.DropColumn(name: "VehiclePlate", table: "Orders");
            migrationBuilder.DropColumn(name: "VehicleModel", table: "Orders");
            migrationBuilder.DropColumn(name: "ParkingBay", table: "Orders");
            migrationBuilder.DropColumn(name: "DeliveredAtUtc", table: "Orders");
            migrationBuilder.DropColumn(name: "DeliveredByEmployeeId", table: "Orders");

            migrationBuilder.DropColumn(name: "PackingIntent", table: "OrderItems");
            migrationBuilder.DropColumn(name: "PackagingFeeApplied", table: "OrderItems");
            migrationBuilder.DropColumn(name: "IsPackagingWaived", table: "OrderItems");
            migrationBuilder.DropColumn(name: "PackagingWaiverReason", table: "OrderItems");
            migrationBuilder.DropColumn(name: "PackagingWaivedBy", table: "OrderItems");

            migrationBuilder.DropColumn(name: "LeftoverPackingCharge", table: "TableBills");
            migrationBuilder.DropColumn(name: "VehiclePlate", table: "TableBills");
            migrationBuilder.DropColumn(name: "ParkingBay", table: "TableBills");

            migrationBuilder.DropColumn(name: "ScheduledPickupEnabled", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "DefaultPreparationLeadMinutes", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "CarSideOrderingEnabled", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "HybridDineInParcelEnabled", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "LeftoverContainerCharge", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "UpiQrEnabled", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "UpiVpa", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "UpiPayeeName", table: "RestaurantSettings");
            migrationBuilder.DropColumn(name: "UpiMerchantCode", table: "RestaurantSettings");
        }
    }
}
