using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Tablo.Api.Constants;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class PlanAuditPostgresTests
{
    [SupportPostgresFact]
    public async Task GlobalPlanEdit_CommitsWithSystemAudit_AndPreservesTenantForeignKey()
    {
        var connection = Environment.GetEnvironmentVariable("TABLO_SUPPORT_TEST_CONNECTION")!;
        var database = "plan_audit_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            await create.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(connection) { Database = database };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString).Options;
        try
        {
            Guid planId;
            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
                var plan = new Plan { Code = "audit-test", Name = "Audit test", FeaturesJson = "[]" };
                db.Plans.Add(plan);
                await db.SaveChangesAsync();
                planId = plan.Id;
                var service = new SubscriptionService(db, NullLogger<SubscriptionService>.Instance);
                var result = await service.UpdatePlanFeaturesAsync(plan.Id, new UpdatePlanFeaturesRequestDto
                {
                    Features = Capabilities.PremiumDefaults.ToList(), MaxKitchenStations = 2,
                    Reason = "Verify global plan audit without a fictitious restaurant"
                }, "superadmin@test.local");
                Assert.Equal(2, result.MaxKitchenStations);
                Assert.Contains(Capabilities.ThermalPrinting, result.Features);
            }
            await using (var verify = new ApplicationDbContext(options))
            {
                Assert.Equal(2, (await verify.Plans.FindAsync(planId))!.MaxKitchenStations);
                var audit = await verify.SubscriptionAuditLogs.SingleAsync();
                Assert.Null(audit.RestaurantId);
                Assert.Equal(planId, audit.ToPlanId);
                Assert.Equal("FeaturesUpdated", audit.Action);
                Assert.Equal("superadmin@test.local", audit.ActorEmail);
                var service = new SubscriptionService(verify, NullLogger<SubscriptionService>.Instance);
                var logs = await service.GetAuditLogsAsync(null);
                Assert.Equal("System", Assert.Single(logs).RestaurantName);
                Assert.Empty(await service.GetAuditLogsAsync(Guid.NewGuid()));
                verify.SubscriptionAuditLogs.Add(new SubscriptionAuditLog
                {
                    RestaurantId = Guid.NewGuid(), ActorEmail = "test@test.local", Action = "InvalidTenant", Reason = "FK must remain enforced"
                });
                await Assert.ThrowsAsync<DbUpdateException>(() => verify.SaveChangesAsync());
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
