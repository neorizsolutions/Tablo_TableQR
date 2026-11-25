using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Npgsql;
using Tablo.Api.Controllers;
using Tablo.Api.Data;
using Tablo.Api.DTOs;
using Tablo.Api.Entities;
using Tablo.Api.Hubs;
using Tablo.Api.Interfaces;
using Tablo.Api.Services;

namespace Tablo.Api.Tests;

public sealed class ControllableTimeProvider(DateTimeOffset startUtc) : TimeProvider
{
    private DateTimeOffset _utcNow = startUtc;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
}

public sealed class SupportPostgresFactAttribute : FactAttribute
{
    public SupportPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TABLO_SUPPORT_TEST_CONNECTION")))
            Skip = "Set TABLO_SUPPORT_TEST_CONNECTION to an isolated loopback PostgreSQL test instance.";
    }
}

public sealed class RecordingEmailService : IEmailService
{
    public ConcurrentQueue<(string To, string Subject, string Body, string? IdempotencyKey, DateTimeOffset At)> Sent { get; } = new();
    public Func<int, Exception?>? FailureRule { get; set; }
    private int _attempts;

    public Task SendEmailAsync(string to, string subject, string body)
        => SendEmailWithResultAsync(to, subject, body).AsTaskCompat();

    public Task<EmailDispatchResult> SendEmailWithResultAsync(
        string to,
        string subject,
        string body,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        int attempt = Interlocked.Increment(ref _attempts);
        var ex = FailureRule?.Invoke(attempt);
        if (ex != null)
            return Task.FromException<EmailDispatchResult>(ex);

        Sent.Enqueue((to, subject, body, idempotencyKey, DateTimeOffset.UtcNow));
        return Task.FromResult(new EmailDispatchResult($"rec-msg-{attempt}"));
    }
}

internal static class TaskExtensions
{
    public static Task AsTaskCompat(this Task t) => t;
}

public sealed class SupportTestHost : WebApplicationFactory<Program>
{
    private readonly DbContextOptions<ApplicationDbContext> _options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase("SupportModule_" + Guid.NewGuid())
        .Options;

    internal const string TestKey = "support-module-isolated-test-signing-key-2026-x";

    public ControllableTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));
    public RecordingEmailService Emails { get; } = new();
    public List<SupportRealtimeEventDto> PublishedEvents { get; } = new();

    public Guid RestaurantA { get; } = Guid.NewGuid();
    public Guid RestaurantB { get; } = Guid.NewGuid();
    public Guid AdminA { get; } = Guid.NewGuid();
    public Guid OwnerA { get; } = Guid.NewGuid();
    public Guid WaiterA { get; } = Guid.NewGuid();
    public Guid CashierA { get; } = Guid.NewGuid();
    public Guid KitchenA { get; } = Guid.NewGuid();
    public Guid AdminB { get; } = Guid.NewGuid();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = TestKey,
            ["Jwt:Issuer"] = "support-tests",
            ["Jwt:Audience"] = "support-tests",
            ["AllowedOrigins:0"] = "http://localhost"
        }));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestKey,
                ["Jwt:Issuer"] = "support-tests",
                ["Jwt:Audience"] = "support-tests",
                ["AllowedOrigins:0"] = "http://localhost",
                ["SeedDummyData"] = "false",
                ["AutoMigrate"] = "false",
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=unused;Username=unused"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.AddScoped(_ => new ApplicationDbContext(_options));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);

            // Prevent background timer loop interference when testing controllable clock manually
            var hostedWorkers = services.Where(d => d.ServiceType == typeof(IHostedService) &&
                                                    d.ImplementationType == typeof(SupportEscalationWorker)).ToList();
            foreach (var w in hostedWorkers) services.Remove(w);
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Restaurants.AnyAsync(r => r.Id == RestaurantA)) return;

        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin") ?? new Role { Id = Guid.NewGuid(), Name = "Admin" };
        var ownerRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Owner") ?? new Role { Id = Guid.NewGuid(), Name = "Owner" };
        var waiterRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Waiter") ?? new Role { Id = Guid.NewGuid(), Name = "Waiter" };
        var cashierRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Cashier") ?? new Role { Id = Guid.NewGuid(), Name = "Cashier" };
        var kitchenRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Kitchen") ?? new Role { Id = Guid.NewGuid(), Name = "Kitchen" };

        if (await db.Roles.AllAsync(r => r.Name != "Owner")) db.Roles.Add(ownerRole);

        db.Restaurants.AddRange(
            new Restaurant { Id = RestaurantA, Name = "Hotel City", ContactEmail = "city@admin.com" },
            new Restaurant { Id = RestaurantB, Name = "Spice Garden", ContactEmail = "spice@admin.com" }
        );

        db.Employees.AddRange(
            new Employee { Id = AdminA, RestaurantId = RestaurantA, RoleId = adminRole.Id, FirstName = "City", LastName = "Admin", Email = "city@admin.com", IsActive = true, SecurityStamp = "stamp-a1" },
            new Employee { Id = OwnerA, RestaurantId = RestaurantA, RoleId = ownerRole.Id, FirstName = "City", LastName = "Owner", Email = "city@owner.com", IsActive = true, SecurityStamp = "stamp-o1" },
            new Employee { Id = WaiterA, RestaurantId = RestaurantA, RoleId = waiterRole.Id, FirstName = "Waiter", LastName = "One", Email = "city@waiter.com", IsActive = true },
            new Employee { Id = CashierA, RestaurantId = RestaurantA, RoleId = cashierRole.Id, FirstName = "Cashier", LastName = "One", Email = "city@cashier.com", IsActive = true },
            new Employee { Id = KitchenA, RestaurantId = RestaurantA, RoleId = kitchenRole.Id, FirstName = "Kitchen", LastName = "One", Email = "city@kitchen.com", IsActive = true },
            new Employee { Id = AdminB, RestaurantId = RestaurantB, RoleId = adminRole.Id, FirstName = "Spice", LastName = "Admin", Email = "spice@admin.com", IsActive = true, SecurityStamp = "stamp-b1" }
        );

        await db.SaveChangesAsync();
    }

    public string Token(string role, Guid? restaurantId, Guid? userId, string? securityStamp = null, string? subOverride = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subOverride ?? (userId?.ToString() ?? "superadmin")),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Email, role == "SuperAdmin" ? "superadmin@tablo.local" : "user@example.test"),
            new("name", role == "SuperAdmin" ? "Tablo Support" : role)
        };
        if (restaurantId.HasValue) claims.Add(new Claim("RestaurantId", restaurantId.Value.ToString()));
        if (!string.IsNullOrEmpty(securityStamp)) claims.Add(new Claim("SecurityStamp", securityStamp));

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "support-tests",
            "support-tests",
            claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
                SecurityAlgorithms.HmacSha256)));
    }
}

public class SupportModuleTests
{
    [Fact]
    public async Task Authorization_TenantIsolation_And_MenuAccessOverride_EnforcedAcrossEndpoints()
    {
        using var host = new SupportTestHost();
        await host.SeedAsync();

        var anonClient = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/support/conversations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.GetAsync("/api/sysadmin/support/conversations")).StatusCode);

        // Waiter, Kitchen, and Cashier cannot access /api/support or /api/sysadmin/support
        foreach (var (role, empId) in new[] { ("Waiter", host.WaiterA), ("Kitchen", host.KitchenA), ("Cashier", host.CashierA) })
        {
            using var staff = host.CreateClient();
            staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(role, host.RestaurantA, empId));
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/support/conversations")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/support/conversations", new { subject = "x", message = "y" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/sysadmin/support/conversations")).StatusCode);
        }

        // Restaurant A Admin & Owner succeed; Restaurant A cannot touch SuperAdmin endpoints
        using var adminA = host.CreateClient();
        adminA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("Admin", host.RestaurantA, host.AdminA, "stamp-a1"));
        Assert.Equal(HttpStatusCode.Forbidden, (await adminA.GetAsync("/api/sysadmin/support/conversations")).StatusCode);

        var opId = Guid.NewGuid();
        var createRes = await adminA.PostAsJsonAsync("/api/support/conversations", new
        {
            subject = "Printer connection lost",
            message = "Kitchen printer keeps dropping.",
            clientOperationId = opId
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<SupportConversationMutationResultDto>();
        Assert.NotNull(created);
        Assert.False(created!.Deduplicated);
        var convId = created.Conversation.Id;

        // Deduplicated create retry returns original record without duplicate
        var dupRes = await adminA.PostAsJsonAsync("/api/support/conversations", new
        {
            subject = "Printer connection lost",
            message = "Kitchen printer keeps dropping.",
            clientOperationId = opId
        });
        Assert.Equal(HttpStatusCode.OK, dupRes.StatusCode);
        var dupDto = await dupRes.Content.ReadFromJsonAsync<SupportConversationMutationResultDto>();
        Assert.True(dupDto!.Deduplicated);
        Assert.Equal(convId, dupDto.Conversation.Id);

        // Restaurant B cannot view, reply, resolve, reopen, or mark-read Restaurant A's conversation
        using var adminB = host.CreateClient();
        adminB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("Admin", host.RestaurantB, host.AdminB, "stamp-b1"));
        Assert.Equal(HttpStatusCode.NotFound, (await adminB.GetAsync($"/api/support/conversations/{convId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminB.PostAsJsonAsync($"/api/support/conversations/{convId}/messages", new { message = "steal" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminB.PostAsJsonAsync($"/api/support/conversations/{convId}/resolve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminB.PostAsJsonAsync($"/api/support/conversations/{convId}/reopen", new { message = "reopen" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminB.PostAsync($"/api/support/conversations/{convId}/read", null)).StatusCode);

        // Restaurant B list shows 0 conversations from A
        var listB = await adminB.GetFromJsonAsync<SupportSyncResponseDto>("/api/support/conversations");
        Assert.Empty(listB!.Conversations);

        // Custom AllowedMenuIds omitting "support" immediately revokes HTTP access
        Assert.Equal(HttpStatusCode.OK, (await adminA.PutAsJsonAsync($"/api/employees/{host.OwnerA}/menu-access", new MenuAccessDto(["staff-management"]))).StatusCode);
        using var ownerA = host.CreateClient();
        ownerA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token("Owner", host.RestaurantA, host.OwnerA, "stamp-o1"));
        Assert.Equal(HttpStatusCode.Forbidden, (await ownerA.GetAsync("/api/support/conversations")).StatusCode);
    }

    [Fact]
    public async Task ExactTenMinuteEscalationTimingRules_And_ControllableClockLifecycle()
    {
        using var host = new SupportTestHost();
        await host.SeedAsync();

        var worker = new SupportEscalationWorker(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            host.Clock,
            NullLogger<SupportEscalationWorker>.Instance);

        using var scope = host.Services.CreateScope();
        var support = scope.ServiceProvider.GetRequiredService<ISupportService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. T+0m: First restaurant message starts waiting period (deadline = T+10m)
        var t0 = host.Clock.GetUtcNow().UtcDateTime;
        var created = await support.CreateConversationAsync(host.RestaurantA, host.AdminA, new CreateSupportConversationDto
        {
            Subject = "Urgent POS freeze",
            Message = "Need immediate help at counter 1.",
            ClientOperationId = Guid.NewGuid()
        });
        var convId = created.Conversation.Id;
        var period1 = created.Conversation.CurrentWaitingPeriodId!.Value;
        Assert.Equal("AwaitingSupport", created.Conversation.Status);
        Assert.Equal(t0.AddMinutes(10), created.Conversation.EscalationDeadlineUtc);

        // 2. T+5m: Follow-up restaurant message preserves ORIGINAL deadline T+10m
        host.Clock.Advance(TimeSpan.FromMinutes(5));
        var followUp = await support.AddRestaurantReplyAsync(convId, host.RestaurantA, host.AdminA, new CreateSupportMessageDto
        {
            Message = "Counter 2 also impacted.",
            ClientOperationId = Guid.NewGuid()
        });
        Assert.Equal(period1, followUp.Conversation.CurrentWaitingPeriodId);
        Assert.Equal(t0.AddMinutes(10), followUp.Conversation.EscalationDeadlineUtc);
        Assert.Equal(2, followUp.Conversation.SuperAdminUnreadCount);

        // 3. T+9m59s: Worker runs -> NO email yet
        host.Clock.SetUtcNow(t0.AddMinutes(9).AddSeconds(59));
        Assert.Equal(0, await worker.ProcessDueEscalationsAsync());
        Assert.Empty(host.Emails.Sent);

        // 4. T+10m00s: Worker runs -> escalates ONCE to support@neorizsolutions.com
        host.Clock.SetUtcNow(t0.AddMinutes(10));
        Assert.Equal(1, await worker.ProcessDueEscalationsAsync());
        Assert.Single(host.Emails.Sent);
        var sentEmail = host.Emails.Sent.Single();
        Assert.Equal("support@neorizsolutions.com", sentEmail.To);
        Assert.Contains("Hotel City", sentEmail.Body);
        Assert.Contains(convId.ToString(), sentEmail.Body);
        Assert.DoesNotContain("Counter 2 also impacted.", sentEmail.Body); // message body kept out

        // Escalation does NOT count as support reply; Status stays AwaitingSupport, duplicate worker runs send nothing
        var afterEsc = await support.GetConversationDetailAsync(convId, host.RestaurantA);
        Assert.True(afterEsc!.Conversation.IsEscalated);
        Assert.Equal("AwaitingSupport", afterEsc.Conversation.Status);
        Assert.Equal(0, await worker.ProcessDueEscalationsAsync());
        Assert.Single(host.Emails.Sent);

        // 5. T+12m: Super Admin replies -> ends waiting period & transitions to AwaitingRestaurant
        host.Clock.SetUtcNow(t0.AddMinutes(12));
        var saReply = await support.AddSuperAdminReplyAsync(convId, "Tablo Support", "superadmin@tablo.local", new CreateSupportMessageDto
        {
            Message = "Restarting your gateway now.",
            ClientOperationId = Guid.NewGuid()
        });
        Assert.Equal("AwaitingRestaurant", saReply.Conversation.Status);
        Assert.Null(saReply.Conversation.CurrentWaitingPeriodId);
        Assert.Null(saReply.Conversation.EscalationDeadlineUtc);
        Assert.False(saReply.Conversation.IsEscalated);
        Assert.Equal(0, saReply.Conversation.SuperAdminUnreadCount);
        Assert.Equal(1, saReply.Conversation.RestaurantUnreadCount);

        // 6. T+15m: Later restaurant message starts a NEW waiting period (deadline = T+25m)
        host.Clock.SetUtcNow(t0.AddMinutes(15));
        var msgPeriod2 = await support.AddRestaurantReplyAsync(convId, host.RestaurantA, host.AdminA, new CreateSupportMessageDto
        {
            Message = "Still showing offline error.",
            ClientOperationId = Guid.NewGuid()
        });
        Assert.NotEqual(period1, msgPeriod2.Conversation.CurrentWaitingPeriodId);
        Assert.Equal(t0.AddMinutes(25), msgPeriod2.Conversation.EscalationDeadlineUtc);

        // 7. T+20m: Super Admin replies BEFORE 10m -> cancels pending escalation
        host.Clock.SetUtcNow(t0.AddMinutes(20));
        await support.AddSuperAdminReplyAsync(convId, "Tablo Support", "superadmin@tablo.local", new CreateSupportMessageDto
        {
            Message = "Fixed upstream.",
            ClientOperationId = Guid.NewGuid()
        });
        host.Clock.SetUtcNow(t0.AddMinutes(30));
        Assert.Equal(0, await worker.ProcessDueEscalationsAsync());
        Assert.Single(host.Emails.Sent); // still only 1 email total!

        // 8. Resolution cancels pending escalation
        var msgPeriod3 = await support.AddRestaurantReplyAsync(convId, host.RestaurantA, host.AdminA, new CreateSupportMessageDto
        {
            Message = "One more question before closing.",
            ClientOperationId = Guid.NewGuid()
        });
        Assert.Equal(t0.AddMinutes(40), msgPeriod3.Conversation.EscalationDeadlineUtc);

        host.Clock.SetUtcNow(t0.AddMinutes(32));
        var resolved = await support.ResolveConversationAsync(convId, host.RestaurantA, "Restaurant", "City Admin");
        Assert.Equal("Resolved", resolved!.Status);
        Assert.Null(resolved.EscalationDeadlineUtc);

        host.Clock.SetUtcNow(t0.AddMinutes(45));
        Assert.Equal(0, await worker.ProcessDueEscalationsAsync());
        Assert.Single(host.Emails.Sent);

        // 9. Reopening resolved conversation WITH a message starts a NEW waiting period (deadline = T+55m)
        var reopened = await support.ReopenConversationAsync(convId, host.RestaurantA, host.AdminA, new ReopenSupportConversationDto
        {
            Message = "Reopening because issue recurred.",
            ClientOperationId = Guid.NewGuid()
        });
        Assert.Equal("AwaitingSupport", reopened.Conversation.Status);
        Assert.Equal(t0.AddMinutes(55), reopened.Conversation.EscalationDeadlineUtc);

        host.Clock.SetUtcNow(t0.AddMinutes(55));
        Assert.Equal(1, await worker.ProcessDueEscalationsAsync());
        Assert.Equal(2, host.Emails.Sent.Count);

        var logs = await db.SupportEscalationLogs.AsNoTracking().Where(l => l.ConversationId == convId).ToListAsync();
        Assert.Equal(2, logs.Count(l => l.Status == SupportEscalationStatus.Dispatched));
        Assert.Equal(2, logs.Count(l => l.Status == SupportEscalationStatus.Cancelled));
    }

    [Fact]
    public async Task EscalationWorker_HandlesConcurrency_Retries_BoundedFailures_And_RestartRecovery()
    {
        using var host = new SupportTestHost();
        await host.SeedAsync();

        var scopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();
        var workerA = new SupportEscalationWorker(scopeFactory, host.Clock, NullLogger<SupportEscalationWorker>.Instance);
        var workerB = new SupportEscalationWorker(scopeFactory, host.Clock, NullLogger<SupportEscalationWorker>.Instance);

        using var scope = host.Services.CreateScope();
        var support = scope.ServiceProvider.GetRequiredService<ISupportService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Case A: Concurrency - 2 simultaneous workers dispatch ONLY 1 email
        var t0 = host.Clock.GetUtcNow().UtcDateTime;
        var c1 = await support.CreateConversationAsync(host.RestaurantA, host.AdminA, new CreateSupportConversationDto
        {
            Subject = "Concurrency check",
            Message = "First check",
            ClientOperationId = Guid.NewGuid()
        });
        host.Clock.Advance(TimeSpan.FromMinutes(10));
        var concurrentResults = await Task.WhenAll(
            workerA.ProcessDueEscalationsAsync(),
            workerB.ProcessDueEscalationsAsync());
        Assert.Equal(1, concurrentResults.Sum());
        Assert.Single(host.Emails.Sent);

        // Case B: Email failure + exponential backoff + recovery on attempt 3
        var c2 = await support.CreateConversationAsync(host.RestaurantA, host.AdminA, new CreateSupportConversationDto
        {
            Subject = "Retry check",
            Message = "Simulate transient SMTP failures",
            ClientOperationId = Guid.NewGuid()
        });
        int failCalls = 0;
        host.Emails.FailureRule = _ => (++failCalls <= 2) ? new HttpRequestException("503 Brevo transient failure") : null;
        host.Clock.Advance(TimeSpan.FromMinutes(10));

        // Attempt 1 fails -> NextRetryAtUtc is +15s
        Assert.Equal(0, await workerA.ProcessDueEscalationsAsync());
        var log2 = await db.SupportEscalationLogs.AsNoTracking().SingleAsync(l => l.ConversationId == c2.Conversation.Id);
        Assert.Equal(SupportEscalationStatus.Failed, log2.Status);
        Assert.Equal(1, log2.AttemptCount);
        Assert.Contains("503 Brevo transient failure", log2.LastError);

        // Before backoff expires -> skips
        host.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(0, await workerA.ProcessDueEscalationsAsync());

        // Attempt 2 fails after backoff
        host.Clock.Advance(TimeSpan.FromSeconds(15));
        Assert.Equal(0, await workerA.ProcessDueEscalationsAsync());
        log2 = await db.SupportEscalationLogs.AsNoTracking().SingleAsync(l => l.ConversationId == c2.Conversation.Id);
        Assert.Equal(2, log2.AttemptCount);

        // Attempt 3 succeeds after second backoff (30s)
        host.Clock.Advance(TimeSpan.FromSeconds(35));
        Assert.Equal(1, await workerA.ProcessDueEscalationsAsync());
        Assert.Equal(2, host.Emails.Sent.Count);

        // Case C: Stale worker crash recovery (Processing lease older than 2 minutes)
        var c3 = await support.CreateConversationAsync(host.RestaurantB, host.AdminB, new CreateSupportConversationDto
        {
            Subject = "Crash recovery",
            Message = "Simulated killed worker mid-send",
            ClientOperationId = Guid.NewGuid()
        });
        host.Clock.Advance(TimeSpan.FromMinutes(10));
        var staleLog = await db.SupportEscalationLogs.SingleAsync(l => l.ConversationId == c3.Conversation.Id);
        staleLog.Status = SupportEscalationStatus.Processing;
        staleLog.ClaimedBy = "dead-worker-pod";
        staleLog.ClaimedAtUtc = host.Clock.GetUtcNow().UtcDateTime.AddMinutes(-3);
        staleLog.AttemptCount = 1;
        await db.SaveChangesAsync();

        Assert.Equal(1, await workerA.ProcessDueEscalationsAsync());
        Assert.Equal(3, host.Emails.Sent.Count);

        // Case D: Bounded retries stop at MaxAttempts (5)
        var c4 = await support.CreateConversationAsync(host.RestaurantB, host.AdminB, new CreateSupportConversationDto
        {
            Subject = "Terminal email failure",
            Message = "Permanent error",
            ClientOperationId = Guid.NewGuid()
        });
        host.Emails.FailureRule = _ => new InvalidOperationException("Permanent SMTP failure");
        host.Clock.Advance(TimeSpan.FromMinutes(10));

        for (int i = 0; i < SupportEscalationWorker.MaxAttempts + 2; i++)
        {
            await workerA.ProcessDueEscalationsAsync();
            host.Clock.Advance(TimeSpan.FromMinutes(6));
        }

        var log4 = await db.SupportEscalationLogs.AsNoTracking().SingleAsync(l => l.ConversationId == c4.Conversation.Id);
        Assert.Equal(SupportEscalationWorker.MaxAttempts, log4.AttemptCount);
        Assert.Equal(SupportEscalationStatus.Failed, log4.Status);
        Assert.Null(log4.NextRetryAtUtc);
    }

    [Fact]
    public async Task SignalRHub_EnforcesOwnership_DeniesForeignTenant_And_RevokesConnectedClients()
    {
        using var host = new SupportTestHost();
        await host.SeedAsync();

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var support = scope.ServiceProvider.GetRequiredService<ISupportService>();
        var registry = host.Services.GetRequiredService<SupportConnectionRegistry>();

        var convA = (await support.CreateConversationAsync(host.RestaurantA, host.AdminA, new CreateSupportConversationDto
        {
            Subject = "Tenant A private thread",
            Message = "Private details",
            ClientOperationId = Guid.NewGuid()
        })).Conversation;

        var aborted = new TaskCompletionSource<bool>();
        var removedGroups = new List<string>();
        var sentEvents = new List<string>();

        var mockClients = new Mock<IHubCallerClients>();
        var callerProxy = new Mock<ISingleClientProxy>();
        callerProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns<string, object?[], CancellationToken>((method, _, _) =>
            {
                sentEvents.Add(method);
                return Task.CompletedTask;
            });
        mockClients.SetupGet(c => c.Caller).Returns(callerProxy.Object);

        var groups = new Mock<IGroupManager>();
        groups.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        groups.Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((_, gName, _) =>
            {
                removedGroups.Add(gName);
                return Task.CompletedTask;
            });

        var context = new Mock<HubCallerContext>();
        var principalB = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, host.AdminB.ToString()),
            new Claim(ClaimTypes.NameIdentifier, host.AdminB.ToString()),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("RestaurantId", host.RestaurantB.ToString()),
            new Claim("SecurityStamp", "stamp-b1")
        }, "TestAuth"));
        context.SetupGet(c => c.ConnectionId).Returns("conn-admin-b");
        context.SetupGet(c => c.User).Returns(principalB);
        context.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);
        context.Setup(c => c.Abort()).Callback(() => aborted.TrySetResult(true));

        var hub = new SupportHub(db, registry)
        {
            Clients = mockClients.Object,
            Groups = groups.Object,
            Context = context.Object
        };

        await hub.OnConnectedAsync();

        // Restaurant B Admin CANNOT subscribe to SuperAdmin inbox, Restaurant A, or Restaurant A's conversation
        await Assert.ThrowsAsync<HubException>(() => hub.JoinSuperAdminInbox());
        await Assert.ThrowsAsync<HubException>(() => hub.JoinRestaurantSupport(host.RestaurantA));
        await Assert.ThrowsAsync<HubException>(() => hub.JoinConversation(convA.Id));

        // Now revoke Admin B directly in DB (e.g. account deactivated) & trigger pre-dispatch pruning -> connection is aborted!
        var empB = await db.Employees.SingleAsync(e => e.Id == host.AdminB);
        empB.IsActive = false;
        await db.SaveChangesAsync();

        await registry.PruneInvalidConnectionsForRestaurantAsync(host.RestaurantB, db);
        Assert.True(aborted.Task.IsCompletedSuccessfully);
        await Assert.ThrowsAsync<HubException>(() => hub.Ping());
    }

    [Fact]
    public async Task EmailService_LocalMailSink_CapturesAndDeduplicatesByIdempotencyKey()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tablo-mail-sink-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cfg = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:LocalSinkDirectory"] = tempDir,
                    ["Email:FromEmail"] = "no-reply@notify.neorizsolutions.com",
                    ["Email:FromName"] = "Tablo Alerts"
                })
                .Build();

            var emailSvc = new EmailService(cfg, new HttpClient());
            var r1 = await emailSvc.SendEmailWithResultAsync(
                "support@neorizsolutions.com",
                "Urgent Test",
                "Body",
                "period-123");
            var r2 = await emailSvc.SendEmailWithResultAsync(
                "support@neorizsolutions.com",
                "Urgent Test",
                "Body",
                "period-123");

            Assert.Single(Directory.GetFiles(tempDir, "*.json"));
            Assert.NotNull(r1.ProviderMessageId);
            Assert.StartsWith("local-idempotent:", r2.ProviderMessageId);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [SupportPostgresFact]
    public async Task PostgresMigration_HasNoPendingModelChanges_And_ConcurrentWorkerEscalatesOnce()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TABLO_SUPPORT_TEST_CONNECTION"));
        builder.Pooling = false;
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        var dbName = "tablo_support_test_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", admin))
            await create.ExecuteNonQueryAsync();
        builder.Database = dbName;

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(builder.ConnectionString)
                .Options;

            var clock = new ControllableTimeProvider(new DateTimeOffset(2026, 11, 25, 14, 0, 0, TimeSpan.Zero));
            var emails = new RecordingEmailService();

            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
                Assert.False(db.Database.HasPendingModelChanges());

                DbSeeder.Seed(db, false);
                var adminRole = await db.Roles.SingleAsync(r => r.Name == "Admin");
                var restaurant = new Restaurant { Id = Guid.NewGuid(), Name = "PG Hotel", ContactEmail = "pg@admin.com" };
                var emp = new Employee { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, RoleId = adminRole.Id, FirstName = "PG", LastName = "Admin", Email = "pg@admin.com", IsActive = true };
                db.AddRange(restaurant, emp);
                await db.SaveChangesAsync();

                var notifier = new Mock<ISupportRealtimeNotifier>();
                var svc = new SupportService(db, clock, notifier.Object, NullLogger<SupportService>.Instance);
                var res = await svc.CreateConversationAsync(restaurant.Id, emp.Id, new CreateSupportConversationDto
                {
                    Subject = "PG Real DB Test",
                    Message = "Check PG atomic lock + FK + filtered unique index",
                    ClientOperationId = Guid.NewGuid()
                });
                Assert.False(res.Deduplicated);
            }

            clock.Advance(TimeSpan.FromMinutes(10));

            var services = new ServiceCollection();
            services.AddScoped(_ => new ApplicationDbContext(options));
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IEmailService>(emails);
            await using var provider = services.BuildServiceProvider();

            var worker1 = new SupportEscalationWorker(provider.GetRequiredService<IServiceScopeFactory>(), clock, NullLogger<SupportEscalationWorker>.Instance);
            var worker2 = new SupportEscalationWorker(provider.GetRequiredService<IServiceScopeFactory>(), clock, NullLogger<SupportEscalationWorker>.Instance);

            var dispatched = await Task.WhenAll(
                worker1.ProcessDueEscalationsAsync(),
                worker2.ProcessDueEscalationsAsync());

            Assert.Equal(1, dispatched.Sum());
            Assert.Single(emails.Sent);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{dbName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
