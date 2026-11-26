using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Tablo.Api.Controllers;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public class StaffLifecycleTests
{
    [Fact]
    public void SoftDeleteMigrationTargetRetainsStaffAndHistoricalAuditMetadata()
    {
        var model = new Tablo.Api.Migrations.AddStaffSoftDeleteAndAudit().TargetModel;
        var employee = model.FindEntityType(typeof(Employee).FullName!)!;
        Assert.NotNull(employee.FindProperty("IsDeleted"));
        Assert.NotNull(employee.FindProperty("DeletedAtUtc"));
        Assert.Contains(employee.GetIndexes(), index => index.Properties.Select(p => p.Name).SequenceEqual(new[] { "Email" }) && index.IsUnique && index.GetFilter() == "\"IsDeleted\" = false");
        var audit = model.FindEntityType(typeof(EmployeeAuditLog).FullName!)!;
        Assert.NotNull(audit);
        Assert.Equal(2, audit.GetForeignKeys().Count(fk => fk.PrincipalEntityType == employee && fk.DeleteBehavior == DeleteBehavior.Restrict));
    }

    [Fact]
    public void NpgsqlSnapshotMatchesCurrentModel()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=unused;Username=unused").Options);
        var migrations = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>();
        var snapshot = db.GetService<Microsoft.EntityFrameworkCore.Infrastructure.IModelRuntimeInitializer>()
            .Initialize(migrations.ModelSnapshot!.Model, designTime: true);
        var current = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;
        var changes = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsModelDiffer>()
            .GetDifferences(snapshot.GetRelationalModel(), current.GetRelationalModel());
        var details = string.Join("\n", changes.Select(op => op.GetType().Name + ": " +
            string.Join(", ", op.GetType().GetProperties().Where(p => p.Name is "Name" or "Table" or "Column" or "Columns" or "PrincipalTable" or "PrincipalColumns")
                .Select(p => p.Name + "=" + (p.GetValue(op) is string[] a ? string.Join("/", a) : p.GetValue(op))))));
        Assert.True(changes.Count == 0, details);
    }

    private static async Task<SupportTestHost> CreateSeededHostAsync()
    {
        var host = new SupportTestHost();
        await host.SeedAsync();
        return host;
    }

    private static HttpClient CreateAuthedClient(SupportTestHost host, string role, Guid? restaurantId, Guid userId, string? stamp = null)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(role, restaurantId, userId, stamp));
        return client;
    }

    [Fact]
    public async Task StatusFilter_And_Counts_AreTenantScoped_AndExcludeDeletedEmployees()
    {
        using var host = await CreateSeededHostAsync();
        Guid inactiveWaiterId;
        Guid deletedCashierId;

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var waiterRole = await db.Roles.FirstAsync(r => r.Name == "Waiter");
            var cashierRole = await db.Roles.FirstAsync(r => r.Name == "Cashier");

            var inactive = new Employee
            {
                Id = Guid.NewGuid(),
                RestaurantId = host.RestaurantA,
                RoleId = waiterRole.Id,
                FirstName = "Inactive",
                LastName = "Waiter",
                Email = "inactive.waiter@city.test",
                IsActive = false,
                IsDeleted = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass#123")
            };
            var deleted = new Employee
            {
                Id = Guid.NewGuid(),
                RestaurantId = host.RestaurantA,
                RoleId = cashierRole.Id,
                FirstName = "Archived",
                LastName = "Cashier",
                Email = "archived.cashier@city.test",
                IsActive = false,
                IsDeleted = true,
                DeletedAtUtc = DateTime.UtcNow,
                DeletedByEmployeeId = host.AdminA,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass#123")
            };
            inactiveWaiterId = inactive.Id;
            deletedCashierId = deleted.Id;

            db.Employees.AddRange(inactive, deleted);
            await db.SaveChangesAsync();
        }

        using var adminClient = CreateAuthedClient(host, "Admin", host.RestaurantA, host.AdminA, "stamp-a1");

        var counts = await adminClient.GetFromJsonAsync<EmployeeStatusCountsDto>("/api/employees/counts");
        Assert.NotNull(counts);
        Assert.Equal(5, counts!.Active);
        Assert.Equal(1, counts.Inactive);
        Assert.Equal(6, counts.All);

        var activeList = await adminClient.GetFromJsonAsync<List<EmployeeDto>>("/api/employees?status=active");
        Assert.NotNull(activeList);
        Assert.Equal(5, activeList!.Count);
        Assert.All(activeList, e =>
        {
            Assert.True(e.IsActive);
            Assert.Equal(host.RestaurantA, e.RestaurantId);
            Assert.NotEqual(deletedCashierId, e.Id);
        });

        var inactiveList = await adminClient.GetFromJsonAsync<List<EmployeeDto>>("/api/employees?status=inactive");
        Assert.NotNull(inactiveList);
        Assert.Single(inactiveList!);
        Assert.Equal(inactiveWaiterId, inactiveList[0].Id);
        Assert.False(inactiveList[0].IsActive);

        var allList = await adminClient.GetFromJsonAsync<List<EmployeeDto>>("/api/employees?status=all");
        Assert.NotNull(allList);
        Assert.Equal(6, allList!.Count);
        Assert.DoesNotContain(allList, e => e.Id == deletedCashierId);

        var invalidFilter = await adminClient.GetAsync("/api/employees?status=bogus");
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);

        // Tenant isolation checks
        using var foreignAdmin = CreateAuthedClient(host, "Admin", host.RestaurantB, host.AdminB, "stamp-b1");
        var foreignCounts = await foreignAdmin.GetFromJsonAsync<EmployeeStatusCountsDto>("/api/employees/counts");
        Assert.Equal(1, foreignCounts!.All);
        Assert.Equal(HttpStatusCode.NotFound, (await foreignAdmin.DeleteAsync($"/api/employees/{host.WaiterA}")).StatusCode);

        // Non-admin role restriction
        using var waiterClient = CreateAuthedClient(host, "Waiter", host.RestaurantA, host.WaiterA);
        Assert.Equal(HttpStatusCode.Forbidden, (await waiterClient.GetAsync("/api/employees?status=active")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await waiterClient.DeleteAsync($"/api/employees/{host.CashierA}")).StatusCode);
    }

    [Fact]
    public async Task SoftDelete_PreservesHistoricalAttribution_RevokesAccess_LogsAudit_AndIsIdempotent()
    {
        using var host = await CreateSeededHostAsync();
        Guid waiterBillId;
        Guid waiterGroupId;
        Guid waiterSupportConvId;
        Guid waiterSupportMsgId;

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var waiter = await db.Employees.FirstAsync(e => e.Id == host.WaiterA);
            waiter.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass#123");
            waiter.ResetOtpHash = "otp-hash";
            waiter.ResetTokenHash = "token-hash";
            waiter.ManagerPinHash = "pin-hash";

            var group = new DiningGroup
            {
                Id = Guid.NewGuid(),
                RestaurantId = host.RestaurantA,
                TableId = Guid.NewGuid(),
                Label = "T1-A",
                CreatedByEmployeeId = host.WaiterA,
                CreationOperationId = Guid.NewGuid(),
                CreationHash = "hash-1"
            };
            var table = new RestaurantTable
            {
                Id = group.TableId,
                RestaurantId = host.RestaurantA,
                TableNumber = 1,
                QRToken = "qr-1"
            };
            var bill = new TableBill
            {
                Id = Guid.NewGuid(),
                RestaurantId = host.RestaurantA,
                TableId = table.Id,
                TableNumberSnapshot = 1,
                SettledByEmployeeId = host.WaiterA,
                SettledAtUtc = DateTime.UtcNow,
                TotalAmount = 500m
            };
            var payment = new BillPayment
            {
                Id = Guid.NewGuid(),
                RestaurantId = host.RestaurantA,
                TableBillId = bill.Id,
                ActorEmployeeId = host.WaiterA,
                Amount = 500m,
                Method = "Cash",
                OperationId = Guid.NewGuid()
            };
            var conv = new SupportConversation
            {
                Id = Guid.NewGuid(),
                RestaurantId = host.RestaurantA,
                Subject = "Printer Help",
                Status = SupportConversationStatus.AwaitingSupport,
                CreatedByEmployeeId = host.WaiterA,
                LastActivityAtUtc = DateTime.UtcNow
            };
            var msg = new SupportMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conv.Id,
                SequenceNumber = 1,
                SenderType = SupportSenderType.Restaurant,
                SenderEmployeeId = host.WaiterA,
                SenderName = "Waiter One",
                SenderEmail = waiter.Email,
                Message = "Need printer paper setup."
            };
            db.RestaurantTables.Add(table);
            db.DiningGroups.Add(group);
            db.TableBills.Add(bill);
            db.BillPayments.Add(payment);
            db.SupportConversations.Add(conv);
            db.SupportMessages.Add(msg);
            await db.SaveChangesAsync();

            waiterGroupId = group.Id;
            waiterBillId = bill.Id;
            waiterSupportConvId = conv.Id;
            waiterSupportMsgId = msg.Id;
        }

        using var adminClient = CreateAuthedClient(host, "Admin", host.RestaurantA, host.AdminA, "stamp-a1");
        using var waiterClient = CreateAuthedClient(host, "Waiter", host.RestaurantA, host.WaiterA);

        // Waiter can call navigation-access before deletion
        Assert.Equal(HttpStatusCode.OK, (await waiterClient.GetAsync("/api/navigation-access")).StatusCode);

        // Delete waiter
        var delRes = await adminClient.DeleteAsync($"/api/employees/{host.WaiterA}");
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);
        var delBody = await delRes.Content.ReadFromJsonAsync<DeleteEmployeeResponseDto>();
        Assert.NotNull(delBody);
        Assert.False(delBody!.AlreadyDeleted);
        Assert.Equal(host.WaiterA, delBody.EmployeeId);

        // Idempotent second DELETE
        var delRepeat = await adminClient.DeleteAsync($"/api/employees/{host.WaiterA}");
        Assert.Equal(HttpStatusCode.OK, delRepeat.StatusCode);
        var repeatBody = await delRepeat.Content.ReadFromJsonAsync<DeleteEmployeeResponseDto>();
        Assert.True(repeatBody!.AlreadyDeleted);

        // Existing JWT rejected immediately
        var afterDeleteNav = await waiterClient.GetAsync("/api/navigation-access");
        Assert.Equal(HttpStatusCode.Unauthorized, afterDeleteNav.StatusCode);

        // Excluded from lists, counts, direct lookup, menu-access GET, and rejected on mutations
        var counts = await adminClient.GetFromJsonAsync<EmployeeStatusCountsDto>("/api/employees/counts");
        Assert.Equal(4, counts!.Active);
        Assert.Equal(0, counts.Inactive);
        Assert.Equal(4, counts.All);

        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.GetAsync($"/api/employees/{host.WaiterA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.GetAsync($"/api/employees/{host.WaiterA}/menu-access")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PatchAsJsonAsync($"/api/employees/{host.WaiterA}/status", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PutAsJsonAsync($"/api/employees/{host.WaiterA}", new UpdateEmployeeDto
        {
            FirstName = "Waiter",
            LastName = "One",
            Email = "city@waiter.com",
            RoleName = "Waiter"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PostAsJsonAsync($"/api/employees/{host.WaiterA}/reset-password", new ResetPasswordDto { NewPassword = "NewPass#1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PatchAsJsonAsync($"/api/employees/{host.WaiterA}/password", new ResetPasswordDto { NewPassword = "NewPass#1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PutAsJsonAsync($"/api/employees/{host.WaiterA}/menu-access", new MenuAccessDto(["overview"]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PostAsJsonAsync($"/api/employees/{host.WaiterA}/pin", new SetManagerPinRequestDto { Pin = "1234" })).StatusCode);

        // Historical FKs, employee attribution, audit log, and auth rejection checks
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var archived = await db.Employees.FirstAsync(e => e.Id == host.WaiterA);
            Assert.True(archived.IsDeleted);
            Assert.False(archived.IsActive);
            Assert.NotNull(archived.DeletedAtUtc);
            Assert.Equal(host.AdminA, archived.DeletedByEmployeeId);
            Assert.Null(archived.ResetOtpHash);
            Assert.Null(archived.ResetTokenHash);
            Assert.Null(archived.ManagerPinHash);

            var auditLogs = await db.EmployeeAuditLogs.Where(l => l.TargetEmployeeId == host.WaiterA).ToListAsync();
            Assert.Single(auditLogs);
            Assert.Equal("Deleted", auditLogs[0].Action);
            Assert.Equal("city@waiter.com", auditLogs[0].TargetEmail);
            Assert.Equal(host.AdminA, auditLogs[0].ActorEmployeeId);

            var conv = await db.SupportConversations.Include(c => c.CreatedByEmployee).FirstAsync(c => c.Id == waiterSupportConvId);
            Assert.Equal("city@waiter.com", conv.CreatedByEmployee.Email);
            var msg = await db.SupportMessages.Include(m => m.SenderEmployee).FirstAsync(m => m.Id == waiterSupportMsgId);
            Assert.Equal("city@waiter.com", msg.SenderEmployee!.Email);
            Assert.True(await db.DiningGroups.AnyAsync(g => g.Id == waiterGroupId));
            Assert.True(await db.TableBills.AnyAsync(b => b.Id == waiterBillId));
            Assert.True(await db.BillPayments.AnyAsync(p => p.TableBillId == waiterBillId && p.ActorEmployeeId == host.WaiterA));

            // Email reuse creates a clean active account without breaking login
            var createReplacement = await adminClient.PostAsJsonAsync("/api/employees", new CreateEmployeeDto
            {
                FirstName = "Waiter",
                LastName = "Replacement",
                Email = "city@waiter.com",
                Password = "NewUser#123",
                RoleName = "Waiter"
            });
            Assert.Equal(HttpStatusCode.Created, createReplacement.StatusCode);
            var replacement = await createReplacement.Content.ReadFromJsonAsync<EmployeeDto>();
            Assert.NotNull(replacement);
            Assert.NotEqual(host.WaiterA, replacement!.Id);

            var auth = new AuthService(
                db,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = SupportTestHost.TestKey,
                    ["Jwt:Issuer"] = "support-tests",
                    ["Jwt:Audience"] = "support-tests"
                }).Build(),
                host.Emails);

            // Old deleted employee password cannot log in, while replacement can
            await Assert.ThrowsAsync<Exception>(() => auth.LoginAsync(new LoginRequestDto { Email = "city@waiter.com", Password = "Pass#123" }));
            var replacementLogin = await auth.LoginAsync(new LoginRequestDto { Email = "city@waiter.com", Password = "NewUser#123" });
            Assert.Equal(replacement.Id, replacementLogin.User.Id);
        }
    }

    [Fact]
    public async Task SelfDelete_LastActiveAdminProtection_AndConcurrentDeleteRace_AreEnforced()
    {
        using var host = await CreateSeededHostAsync();
        using var adminClient = CreateAuthedClient(host, "Admin", host.RestaurantA, host.AdminA, "stamp-a1");
        using var ownerClient = CreateAuthedClient(host, "Owner", host.RestaurantA, host.OwnerA, "stamp-o1");

        // Self-delete blocked
        var selfDelete = await adminClient.DeleteAsync($"/api/employees/{host.AdminA}");
        Assert.Equal(HttpStatusCode.BadRequest, selfDelete.StatusCode);

        // Concurrent delete race: AdminA tries deleting OwnerA while OwnerA tries deleting AdminA.
        // Serialization guarantees one succeeds and one is rejected as the last active admin/owner.
        var t1 = adminClient.DeleteAsync($"/api/employees/{host.OwnerA}");
        var t2 = ownerClient.DeleteAsync($"/api/employees/{host.AdminA}");
        var responses = await Task.WhenAll(t1, t2);

        var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var deniedCount = responses.Count(r => r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        var details = string.Join("; ", await Task.WhenAll(responses.Select(async r => $"{r.StatusCode}: {await r.Content.ReadAsStringAsync()}")));
        Assert.True(okCount == 1, details);
        Assert.True(deniedCount == 1, details);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var remainingActiveAdmins = await db.Employees.CountAsync(e =>
            e.RestaurantId == host.RestaurantA &&
            !e.IsDeleted &&
            e.IsActive &&
            (e.Role.Name == "Admin" || e.Role.Name == "Owner"));
        Assert.Equal(1, remainingActiveAdmins);
    }

    [Fact]
    public async Task DeletedAdmin_CannotUseForgotPassword_OrManagerPin()
    {
        using var host = await CreateSeededHostAsync();
        using var adminClient = CreateAuthedClient(host, "Admin", host.RestaurantA, host.AdminA, "stamp-a1");

        // OwnerA deletes AdminA
        using var ownerClient = CreateAuthedClient(host, "Owner", host.RestaurantA, host.OwnerA, "stamp-o1");
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.DeleteAsync($"/api/employees/{host.AdminA}")).StatusCode);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emails = new RecordingEmailService();
        var auth = new AuthService(
            db,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = SupportTestHost.TestKey,
                ["Jwt:Issuer"] = "support-tests",
                ["Jwt:Audience"] = "support-tests"
            }).Build(),
            emails);

        await auth.ForgotPasswordAsync("city@admin.com");
        Assert.Empty(emails.Sent);
        await Assert.ThrowsAsync<Exception>(() => auth.ChangePasswordAsync(host.AdminA, "anything", "NewPass#99"));

        var managerAuth = new ManagerAuthService(db, NullLogger<ManagerAuthService>.Instance);
        Assert.False(await managerAuth.IsManagerOrAdminAsync(host.RestaurantA, host.AdminA));
        Assert.False(await managerAuth.SetPinAsync(host.RestaurantA, host.AdminA, "9999"));
    }

    [SupportPostgresFact]
    public async Task Postgres_FilteredUniqueEmailIndex_AndMigration_AllowRehiringArchivedEmail()
    {
        var baseConn = Environment.GetEnvironmentVariable("TABLO_SUPPORT_TEST_CONNECTION")!;
        var dbName = $"tablo_staff_pg_{Guid.NewGuid():N}"[..28];
        var adminBuilder = new NpgsqlConnectionStringBuilder(baseConn) { Database = "postgres" };
        await using (var adminConn = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await adminConn.OpenAsync();
            await using var cmd = adminConn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{dbName}\";";
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            var testConn = new NpgsqlConnectionStringBuilder(baseConn) { Database = dbName }.ConnectionString;
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(testConn).Options;

            await using var db = new ApplicationDbContext(opts);
            await db.Database.MigrateAsync();

            var waiterRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Waiter")
                ?? new Role { Id = Guid.NewGuid(), Name = "Waiter" };
            if (db.Entry(waiterRole).State == EntityState.Detached) db.Roles.Add(waiterRole);

            var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "PG Staff Cafe", ContactEmail = "pg@cafe.test" };
            db.Restaurants.Add(restaurant);

            var oldEmp = new Employee
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                RoleId = waiterRole.Id,
                FirstName = "First",
                LastName = "Hire",
                Email = "rehire@cafe.test",
                IsActive = false,
                IsDeleted = true,
                DeletedAtUtc = DateTime.UtcNow,
                PasswordHash = "hash1"
            };
            var newEmp = new Employee
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                RoleId = waiterRole.Id,
                FirstName = "Second",
                LastName = "Hire",
                Email = "rehire@cafe.test",
                IsActive = true,
                IsDeleted = false,
                PasswordHash = "hash2"
            };
            db.Employees.AddRange(oldEmp, newEmp);
            await db.SaveChangesAsync();

            // Adding a second non-deleted employee with same email must violate partial unique index
            db.Employees.Add(new Employee
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                RoleId = waiterRole.Id,
                FirstName = "Dup",
                LastName = "Active",
                Email = "rehire@cafe.test",
                IsActive = true,
                IsDeleted = false,
                PasswordHash = "hash3"
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await using var adminConn = new NpgsqlConnection(adminBuilder.ConnectionString);
            await adminConn.OpenAsync();
            await using var dropCmd = adminConn.CreateCommand();
            dropCmd.CommandText = $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{dbName}' AND pid <> pg_backend_pid()";
            await dropCmd.ExecuteNonQueryAsync();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{dbName}\"";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }
}
