using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tablo.Api.Data;

namespace Tablo.Api.Migrations;

public partial class AddSupportModuleAndEscalations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SupportConversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastActivityAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ResolvedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                AwaitingSupportSinceUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CurrentWaitingPeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                EscalationDeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsEscalated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                EscalatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RestaurantUnreadCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                SuperAdminUnreadCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                ClientOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                LastMessagePreview = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                LastMessageSenderType = table.Column<int>(type: "integer", nullable: true),
                LastMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                RestaurantLastReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SuperAdminLastReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportConversations", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupportConversations_Restaurants_RestaurantId",
                    column: x => x.RestaurantId,
                    principalTable: "Restaurants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SupportConversations_Employees_RestaurantId_CreatedByEmployeeId",
                    columns: x => new { x.RestaurantId, x.CreatedByEmployeeId },
                    principalTable: "Employees",
                    principalColumns: new[] { "RestaurantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SupportMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                SenderType = table.Column<int>(type: "integer", nullable: false),
                SenderEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                SenderName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                SenderEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                ClientOperationId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupportMessages_SupportConversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "SupportConversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SupportMessages_Employees_SenderEmployeeId",
                    column: x => x.SenderEmployeeId,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SupportEscalationLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                WaitingPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                EscalationTargetEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                ScheduledDeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                NextRetryAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ClaimedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                ProviderMessageId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportEscalationLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupportEscalationLogs_SupportConversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "SupportConversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SupportAuditLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ActorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportAuditLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupportAuditLogs_SupportConversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "SupportConversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SupportConversations_RestaurantId",
            table: "SupportConversations",
            column: "RestaurantId");

        migrationBuilder.CreateIndex(
            name: "IX_SupportConversations_Status",
            table: "SupportConversations",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_SupportConversations_EscalationDeadlineUtc",
            table: "SupportConversations",
            column: "EscalationDeadlineUtc");

        migrationBuilder.CreateIndex(
            name: "IX_SupportConversations_RestaurantId_LastActivityAtUtc",
            table: "SupportConversations",
            columns: new[] { "RestaurantId", "LastActivityAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportConversations_RestaurantId_ClientOperationId",
            table: "SupportConversations",
            columns: new[] { "RestaurantId", "ClientOperationId" },
            unique: true,
            filter: "\"ClientOperationId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_SupportConversations_RestaurantId_CreatedByEmployeeId",
            table: "SupportConversations",
            columns: new[] { "RestaurantId", "CreatedByEmployeeId" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportMessages_ConversationId",
            table: "SupportMessages",
            column: "ConversationId");

        migrationBuilder.CreateIndex(
            name: "IX_SupportMessages_ClientOperationId",
            table: "SupportMessages",
            column: "ClientOperationId");

        migrationBuilder.CreateIndex(
            name: "IX_SupportMessages_SenderEmployeeId",
            table: "SupportMessages",
            column: "SenderEmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_SupportMessages_ConversationId_SequenceNumber",
            table: "SupportMessages",
            columns: new[] { "ConversationId", "SequenceNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SupportMessages_ConversationId_ClientOperationId",
            table: "SupportMessages",
            columns: new[] { "ConversationId", "ClientOperationId" },
            unique: true,
            filter: "\"ClientOperationId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_SupportEscalationLogs_ConversationId",
            table: "SupportEscalationLogs",
            column: "ConversationId");

        migrationBuilder.CreateIndex(
            name: "IX_SupportEscalationLogs_WaitingPeriodId",
            table: "SupportEscalationLogs",
            column: "WaitingPeriodId");

        migrationBuilder.CreateIndex(
            name: "IX_SupportEscalationLogs_ConversationId_WaitingPeriodId",
            table: "SupportEscalationLogs",
            columns: new[] { "ConversationId", "WaitingPeriodId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SupportEscalationLogs_Status_ScheduledDeadlineUtc",
            table: "SupportEscalationLogs",
            columns: new[] { "Status", "ScheduledDeadlineUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportAuditLogs_ConversationId",
            table: "SupportAuditLogs",
            column: "ConversationId");

        migrationBuilder.CreateIndex(
            name: "IX_SupportAuditLogs_ConversationId_TimestampUtc",
            table: "SupportAuditLogs",
            columns: new[] { "ConversationId", "TimestampUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SupportAuditLogs");
        migrationBuilder.DropTable(name: "SupportEscalationLogs");
        migrationBuilder.DropTable(name: "SupportMessages");
        migrationBuilder.DropTable(name: "SupportConversations");
    }
}
