using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ticket.Application.Abstractions;
using Ticket.Domain;
using Ticket.Domain.Enums;
using Ticket.Infrastructure.Persistence;
using TicketEntity = Ticket.Domain.Ticket;

namespace Ticket.Infrastructure;

/// <summary>
/// Idempotent startup seed: roles, demo org graph, and a 90-day analytics ticket dataset (~2000 tickets).
/// Ensures the PostgreSQL database from the connection string exists before migrate/seed.
/// </summary>
public static class DbSeed
{
    public const string AnalyticsProviderName = "Aria Support Co.";
    public const string AnalyticsClientName = "Pars Retail Group";
    public const string SeedTopicPrefix = "[SEED]";
    public const string DemoPassword = "ChangeMe123!";
    public const int AnalyticsTicketCount = 2000;
    public const int AnalyticsDayWindow = 90;

    private static readonly string[] DepartmentCodes =
    [
        "FIN", "SALES", "WH", "CS", "IT", "HR", "OPS", "MKT", "LEGAL", "PROD", "LOG", "MGMT"
    ];

    private static readonly string[] DepartmentNames =
    [
        "Finance", "Sales", "Warehouse", "Customer Service", "IT Internal", "HR",
        "Operations", "Marketing", "Legal", "Product", "Logistics", "Management"
    ];

    /// <summary>Relative ticket volume per department (sums to 100).</summary>
    private static readonly int[] DepartmentWeights = [18, 12, 8, 14, 15, 5, 7, 6, 3, 5, 4, 3];

    public static async Task EnsureSeededAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("DbSeed");

        await EnsurePostgresDatabaseExistsAsync(db.Database.GetConnectionString(), cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        await SeedRolesAsync(db, cancellationToken);
        await SeedSuperAdminAsync(db, hasher, cancellationToken);

        if (await db.Tickets.AnyAsync(t => t.Topic.StartsWith(SeedTopicPrefix), cancellationToken))
        {
            logger?.LogInformation("Analytics seed already present; skipping ticket dataset.");
            return;
        }

        var graph = await SeedOrgGraphAsync(db, hasher, cancellationToken);
        await SeedAnalyticsDatasetAsync(db, graph, logger, cancellationToken);
    }

    private static async Task EnsurePostgresDatabaseExistsAsync(string? connectionString, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(NormalizeNpgsqlConnectionString(connectionString));
        }
        catch (ArgumentException)
        {
            // Non-Npgsql key/value or unsupported URI — skip auto-create; MigrateAsync will fail clearly.
            return;
        }

        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
            return;

        // CREATE DATABASE cannot run inside a pooled app connection to the target DB.
        builder.Database = "postgres";
        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using (var check = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", conn))
        {
            check.Parameters.AddWithValue("name", databaseName);
            var exists = await check.ExecuteScalarAsync(cancellationToken);
            if (exists is not null)
                return;
        }

        if (!IsSafePgIdentifier(databaseName))
            throw new InvalidOperationException($"Refusing to CREATE DATABASE with unsafe name: {databaseName}");

        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", conn);
        await create.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Accepts classic Npgsql key/value strings and postgres:// / postgresql:// URIs.</summary>
    public static string NormalizeNpgsqlConnectionString(string connectionString)
    {
        if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(connectionString);
            var userInfo = uri.UserInfo.Split(':', 2);
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
                Username = Uri.UnescapeDataString(userInfo[0]),
            };
            if (userInfo.Length > 1)
                builder.Password = Uri.UnescapeDataString(userInfo[1]);

            var query = uri.Query.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length != 2)
                        continue;
                    var key = Uri.UnescapeDataString(kv[0]);
                    var value = Uri.UnescapeDataString(kv[1]);
                    if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                        builder.SslMode = Enum.Parse<SslMode>(value, ignoreCase: true);
                    else
                        builder[key] = value;
                }
            }

            return builder.ConnectionString;
        }

        return connectionString;
    }

    private static bool IsSafePgIdentifier(string name) =>
        name.Length is > 0 and <= 63 && name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-');

    private static async Task SeedRolesAsync(TicketDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Roles.AnyAsync(cancellationToken))
            return;

        db.Roles.AddRange(
            new Role { Id = 1, Name = RoleNames.SuperAdmin, Description = "Platform admin" },
            new Role { Id = 2, Name = RoleNames.ProviderManager, Description = "Provider manager" },
            new Role { Id = 3, Name = RoleNames.Agent, Description = "Support agent" },
            new Role { Id = 4, Name = RoleNames.ClientManager, Description = "Client manager" },
            new Role { Id = 5, Name = RoleNames.Requester, Description = "Ticket requester" });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSuperAdminAsync(TicketDbContext db, IPasswordHasher hasher, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Username == "superadmin", cancellationToken))
            return;

        db.Users.Add(new User
        {
            Username = "superadmin",
            FullName = "System SuperAdmin",
            PhoneNumber = "09000000000",
            PassHash = hasher.Hash(DemoPassword),
            RoleId = 1,
            IsActive = true
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record OrgGraph(
        Provider Provider,
        Client Client,
        User Pm,
        IReadOnlyList<User> Agents,
        User InactiveAgent,
        User Cm,
        IReadOnlyList<User> Requesters);

    private static async Task<OrgGraph> SeedOrgGraphAsync(
        TicketDbContext db,
        IPasswordHasher hasher,
        CancellationToken cancellationToken)
    {
        var existing = await db.Providers.FirstOrDefaultAsync(p => p.Name == AnalyticsProviderName, cancellationToken);
        if (existing is not null)
        {
            var existingClient = await db.Clients.FirstAsync(
                c => c.ProviderId == existing.Id && c.Name == AnalyticsClientName,
                cancellationToken);
            var existingAgents = await db.Users
                .Where(u => u.ProviderId == existing.Id && u.RoleId == 3 && u.IsActive)
                .OrderBy(u => u.Username)
                .ToListAsync(cancellationToken);
            var existingInactive = await db.Users.FirstAsync(
                u => u.ProviderId == existing.Id && u.RoleId == 3 && !u.IsActive,
                cancellationToken);
            var existingPm = await db.Users.FirstAsync(
                u => u.ProviderId == existing.Id && u.RoleId == 2,
                cancellationToken);
            var existingCm = await db.Users.FirstAsync(
                u => u.ClientId == existingClient.Id && u.RoleId == 4,
                cancellationToken);
            var existingRequesters = await db.Users
                .Where(u => u.ClientId == existingClient.Id && u.RoleId == 5
                    && u.Username.StartsWith("demo.req."))
                .OrderBy(u => u.Username)
                .ToListAsync(cancellationToken);
            return new OrgGraph(
                existing,
                existingClient,
                existingPm,
                existingAgents,
                existingInactive,
                existingCm,
                existingRequesters);
        }

        var now = DateTime.UtcNow;
        var passHash = hasher.Hash(DemoPassword);

        var plans = new[]
        {
            new Plan
            {
                Name = "Analytics Bronze",
                DurationDays = 90,
                Price = 250_000,
                MaxClientCount = 5,
                MaxAgentCount = 5,
                ModulesJson = "[]",
                IsActive = true
            },
            new Plan
            {
                Name = "Analytics Silver",
                DurationDays = 180,
                Price = 600_000,
                MaxClientCount = 20,
                MaxAgentCount = 10,
                ModulesJson = "[]",
                IsActive = true
            },
            new Plan
            {
                Name = "Analytics Gold",
                DurationDays = 365,
                Price = 1_200_000,
                MaxClientCount = 50,
                MaxAgentCount = 20,
                ModulesJson = "[]",
                IsActive = true
            }
        };
        db.Plans.AddRange(plans);
        await db.SaveChangesAsync(cancellationToken);

        var provider = new Provider
        {
            Name = AnalyticsProviderName,
            Email = "ops@aria-support.example.com",
            PhoneNumber = "02144000000",
            IsActive = true,
            CreatedAt = now.AddDays(-120)
        };
        var quietProvider = new Provider
        {
            Name = "Quiet Labs (Inactive)",
            Email = "admin@quiet-labs.example.com",
            PhoneNumber = "02155000000",
            IsActive = false,
            CreatedAt = now.AddDays(-200)
        };
        db.Providers.AddRange(provider, quietProvider);
        await db.SaveChangesAsync(cancellationToken);

        var gold = plans[2];
        var silver = plans[1];
        db.ProviderSubscriptions.AddRange(
            new ProviderSubscription
            {
                ProviderId = provider.Id,
                PlanId = silver.Id,
                PurchaseDate = now.AddDays(-100),
                ExpireDate = now.AddDays(-100 + silver.DurationDays),
                MoneyPaid = silver.Price,
                IsActive = false
            },
            new ProviderSubscription
            {
                ProviderId = provider.Id,
                PlanId = gold.Id,
                PurchaseDate = now.AddDays(-20),
                ExpireDate = now.AddDays(-20 + gold.DurationDays),
                MoneyPaid = gold.Price,
                IsActive = true
            },
            new ProviderSubscription
            {
                ProviderId = quietProvider.Id,
                PlanId = plans[0].Id,
                PurchaseDate = now.AddDays(-60),
                ExpireDate = now.AddDays(-60 + plans[0].DurationDays),
                MoneyPaid = plans[0].Price,
                IsActive = false
            });

        var pm = new User
        {
            Username = "demo.pm",
            FullName = "Neda ProviderManager",
            PhoneNumber = "09121000001",
            PassHash = passHash,
            RoleId = 2,
            ProviderId = provider.Id,
            CreatedAt = now.AddDays(-110)
        };

        var agentSpecs = new (string User, string Name, string Phone, int LastAssignedDaysAgo)[]
        {
            ("demo.agent1", "Reza Agent", "09121000011", 1),
            ("demo.agent2", "Sara Agent", "09121000012", 2),
            ("demo.agent3", "Ali Agent", "09121000013", 4),
            ("demo.agent4", "Mina Agent", "09121000014", 7),
            ("demo.agent5", "Omar Agent", "09121000015", 10)
        };

        var agents = agentSpecs.Select(a => new User
        {
            Username = a.User,
            FullName = a.Name,
            PhoneNumber = a.Phone,
            PassHash = passHash,
            RoleId = 3,
            ProviderId = provider.Id,
            IsActive = true,
            LastAssignedAt = now.AddDays(-a.LastAssignedDaysAgo),
            CreatedAt = now.AddDays(-100)
        }).ToList();

        var inactiveAgent = new User
        {
            Username = "demo.agent6",
            FullName = "Inactive Agent",
            PhoneNumber = "09121000016",
            PassHash = passHash,
            RoleId = 3,
            ProviderId = provider.Id,
            IsActive = false,
            LastAssignedAt = now.AddDays(-45),
            CreatedAt = now.AddDays(-100)
        };

        db.Users.Add(pm);
        db.Users.AddRange(agents);
        db.Users.Add(inactiveAgent);

        var client = new Client
        {
            ProviderId = provider.Id,
            Name = AnalyticsClientName,
            Email = "it@pars-retail.example.com",
            PhoneNumber = "02166000000",
            IsActive = true,
            CreatedAt = now.AddDays(-100)
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);

        var cm = new User
        {
            Username = "demo.cm",
            FullName = "Parsa ClientManager",
            PhoneNumber = "09121000020",
            PassHash = passHash,
            RoleId = 4,
            ClientId = client.Id,
            CreatedAt = now.AddDays(-95)
        };
        db.Users.Add(cm);

        var requesters = new List<User>(DepartmentCodes.Length);
        for (var i = 0; i < DepartmentCodes.Length; i++)
        {
            var code = DepartmentCodes[i].ToLowerInvariant();
            requesters.Add(new User
            {
                Username = $"demo.req.{code}",
                FullName = $"{DepartmentNames[i]} Requester",
                PhoneNumber = $"09121001{i:00}",
                PassHash = passHash,
                RoleId = 5,
                ClientId = client.Id,
                CreatedAt = now.AddDays(-90 + i)
            });
        }

        // Compatibility aliases used by earlier FE/Postman demos.
        requesters.Add(new User
        {
            Username = "demo.req1",
            FullName = "Demo Requester One",
            PhoneNumber = "09121001901",
            PassHash = passHash,
            RoleId = 5,
            ClientId = client.Id,
            CreatedAt = now.AddDays(-90)
        });
        requesters.Add(new User
        {
            Username = "demo.req2",
            FullName = "Demo Requester Two",
            PhoneNumber = "09121001902",
            PassHash = passHash,
            RoleId = 5,
            ClientId = client.Id,
            CreatedAt = now.AddDays(-89)
        });

        db.Users.AddRange(requesters);
        await db.SaveChangesAsync(cancellationToken);

        // Department requesters only (exclude aliases) for weighted analytics.
        var deptRequesters = requesters.Where(u => u.Username.StartsWith("demo.req.", StringComparison.Ordinal)).ToList();
        return new OrgGraph(provider, client, pm, agents, inactiveAgent, cm, deptRequesters);
    }

    private static async Task SeedAnalyticsDatasetAsync(
        TicketDbContext db,
        OrgGraph graph,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var rng = new Random(20260719);
        var today = DateTime.UtcNow.Date;
        var windowStart = today.AddDays(-(AnalyticsDayWindow - 1));

        var deptRequesterIds = BuildWeightedRequesterPool(graph.Requesters);
        var agentWeights = new[] { 35, 25, 18, 12, 7 }; // sums 97; remainder = unassigned / inactive
        var activeAgents = graph.Agents.ToList();

        var tickets = new List<TicketEntity>(AnalyticsTicketCount);
        var messages = new List<TicketMessage>(AnalyticsTicketCount * 3);
        var notes = new List<TicketNote>(400);
        var notifications = new List<Notification>(AnalyticsTicketCount * 2);
        var attachments = new List<Attachment>(80);

        var openStatuses = new[]
        {
            TicketStatus.Open,
            TicketStatus.InProgress,
            TicketStatus.PendingRequesterResponse
        };

        for (var i = 0; i < AnalyticsTicketCount; i++)
        {
            var dayOffset = PickDayOffset(rng, AnalyticsDayWindow);
            var openDate = windowStart
                .AddDays(dayOffset)
                .AddHours(rng.Next(7, 20))
                .AddMinutes(rng.Next(0, 60))
                .AddSeconds(rng.Next(0, 60));
            if (openDate > DateTime.UtcNow)
                openDate = DateTime.UtcNow.AddMinutes(-rng.Next(5, 180));

            var ageDays = Math.Max(0, (today - openDate.Date).TotalDays);
            var deptIndex = PickWeightedIndex(rng, DepartmentWeights);
            var requester = deptRequesterIds[deptIndex][rng.Next(deptRequesterIds[deptIndex].Count)];
            var priority = PickPriority(rng);
            var status = PickStatusForAge(rng, ageDays);

            var assignedAgentId = PickAgentId(rng, activeAgents, agentWeights, graph.InactiveAgent, status, ageDays);
            var reopenCount = 0;
            DateTime? closeDate = null;
            DateTime? seenByAgent = null;

            // Reopen scenario: ticket was resolved/closed, then reopened (same agent kept).
            var isReopenCase = false;
            if (assignedAgentId is not null && ageDays >= 10)
            {
                if ((status is TicketStatus.Open or TicketStatus.InProgress) && rng.NextDouble() < 0.08)
                    isReopenCase = true;
                else if ((status is TicketStatus.Resolved or TicketStatus.Closed) && rng.NextDouble() < 0.10)
                {
                    isReopenCase = true;
                    status = rng.NextDouble() < 0.55 ? TicketStatus.Open : TicketStatus.InProgress;
                }
            }
            if (status is TicketStatus.Resolved or TicketStatus.Closed)
            {
                var maxCloseDays = Math.Max(1, (int)Math.Min(14, Math.Max(1, ageDays)));
                var closeOffsetDays = rng.Next(1, maxCloseDays + 1);
                closeDate = openDate.AddDays(closeOffsetDays).AddHours(rng.Next(1, 8));
                if (closeDate > DateTime.UtcNow)
                    closeDate = DateTime.UtcNow.AddHours(-1);
                if (closeDate <= openDate)
                    closeDate = openDate.AddHours(2);
                seenByAgent = openDate.AddHours(rng.Next(1, 12));
            }
            else if (isReopenCase)
            {
                reopenCount = rng.Next(1, 3);
                closeDate = null;
                seenByAgent = status == TicketStatus.Open ? null : openDate.AddHours(rng.Next(1, 24));
            }
            else if (status != TicketStatus.Open || assignedAgentId is not null)
            {
                if (status != TicketStatus.Open || rng.NextDouble() < 0.55)
                    seenByAgent = openDate.AddHours(rng.Next(1, 36));
            }

            if (assignedAgentId is null)
                seenByAgent = null;

            var topic =
                $"{SeedTopicPrefix}[{DepartmentCodes[deptIndex]}] {DepartmentNames[deptIndex]} issue #{i + 1:0000} — {priority} / {status}";

            var ticket = new TicketEntity
            {
                ClientId = graph.Client.Id,
                ProviderId = graph.Provider.Id,
                RequesterId = requester.Id,
                AssignedAgentId = assignedAgentId,
                Topic = topic.Length <= 300 ? topic : topic[..300],
                Status = status,
                Priority = priority,
                OpenDate = DateTime.SpecifyKind(openDate, DateTimeKind.Utc),
                SeenByAgentDate = seenByAgent is null ? null : DateTime.SpecifyKind(seenByAgent.Value, DateTimeKind.Utc),
                CloseDate = closeDate is null ? null : DateTime.SpecifyKind(closeDate.Value, DateTimeKind.Utc),
                ReopenCount = reopenCount,
                IsDeleted = false
            };
            tickets.Add(ticket);
        }

        // Persist tickets first to get IDs for related rows.
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        const int ticketBatch = 250;
        for (var offset = 0; offset < tickets.Count; offset += ticketBatch)
        {
            var batch = tickets.Skip(offset).Take(ticketBatch).ToList();
            db.Tickets.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        // Reload tickets with IDs (already tracked after SaveChanges — use in-memory list; IDs populated).
        // After Clear(), entities are detached but keep generated Ids.
        for (var i = 0; i < tickets.Count; i++)
        {
            var ticket = tickets[i];
            var rngLocal = new Random(20260719 + i);
            var agentId = ticket.AssignedAgentId;

            // Assignment notification
            if (agentId is int assignedId)
            {
                notifications.Add(new Notification
                {
                    UserId = assignedId,
                    Type = NotificationType.NewTicketAssigned,
                    RefId = ticket.Id,
                    Title = "New ticket assigned",
                    Body = ticket.Topic,
                    IsRead = rngLocal.NextDouble() < 0.55,
                    CreatedAt = ticket.OpenDate
                });
            }

            if (ticket.ReopenCount > 0 && agentId is int reopenAgent)
            {
                notifications.Add(new Notification
                {
                    UserId = reopenAgent,
                    Type = NotificationType.TicketReopened,
                    RefId = ticket.Id,
                    Title = "Ticket reopened",
                    Body = ticket.Topic,
                    IsRead = rngLocal.NextDouble() < 0.4,
                    CreatedAt = ticket.OpenDate.AddDays(rngLocal.Next(3, 9))
                });
            }

            if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
            {
                notifications.Add(new Notification
                {
                    UserId = ticket.RequesterId,
                    Type = ticket.Status == TicketStatus.Resolved
                        ? NotificationType.TicketResolved
                        : NotificationType.TicketClosed,
                    RefId = ticket.Id,
                    Title = ticket.Status == TicketStatus.Resolved ? "Ticket resolved" : "Ticket closed",
                    Body = ticket.Topic,
                    IsRead = rngLocal.NextDouble() < 0.5,
                    CreatedAt = ticket.CloseDate ?? ticket.OpenDate.AddDays(2)
                });
            }

            // ~5% reassigned once (notification only — final AssignedAgentId already set).
            if (agentId is int currentAgent && rngLocal.NextDouble() < 0.05 && activeAgents.Count > 1)
            {
                var other = activeAgents.FirstOrDefault(a => a.Id != currentAgent) ?? activeAgents[0];
                notifications.Add(new Notification
                {
                    UserId = other.Id,
                    Type = NotificationType.TicketReassigned,
                    RefId = ticket.Id,
                    Title = "Ticket reassigned to you",
                    Body = ticket.Topic,
                    IsRead = false,
                    CreatedAt = ticket.OpenDate.AddHours(rngLocal.Next(6, 48))
                });
            }

            // Chat: ~65% of tickets get a conversation; denser on open/in-progress.
            var shouldChat = openStatuses.Contains(ticket.Status)
                ? rngLocal.NextDouble() < 0.85
                : rngLocal.NextDouble() < 0.55;

            if (shouldChat)
            {
                var msgCount = openStatuses.Contains(ticket.Status)
                    ? rngLocal.Next(3, 9)
                    : rngLocal.Next(2, 6);
                var cursor = ticket.OpenDate;
                var endBound = ticket.CloseDate ?? DateTime.UtcNow;

                for (var m = 0; m < msgCount; m++)
                {
                    var fromRequester = m % 2 == 0;
                    var senderId = fromRequester
                        ? ticket.RequesterId
                        : (agentId ?? activeAgents[rngLocal.Next(activeAgents.Count)].Id);

                    cursor = cursor.AddHours(rngLocal.Next(1, 18));
                    if (cursor > endBound)
                        cursor = endBound.AddMinutes(-Math.Max(1, msgCount - m));

                    var msg = new TicketMessage
                    {
                        TicketId = ticket.Id,
                        SenderId = senderId,
                        Text = fromRequester
                            ? $"Requester update #{m + 1} on ticket {ticket.Id}"
                            : $"Agent reply #{m + 1} on ticket {ticket.Id}",
                        CreatedAt = DateTime.SpecifyKind(cursor, DateTimeKind.Utc),
                        SeenAt = rngLocal.NextDouble() < 0.7
                            ? DateTime.SpecifyKind(cursor.AddMinutes(rngLocal.Next(5, 120)), DateTimeKind.Utc)
                            : null,
                        IsDeleted = false
                    };
                    messages.Add(msg);

                    var notifyUserId = fromRequester ? (agentId ?? ticket.RequesterId) : ticket.RequesterId;
                    if (agentId is not null || !fromRequester)
                    {
                        notifications.Add(new Notification
                        {
                            UserId = notifyUserId,
                            Type = NotificationType.NewMessage,
                            RefId = ticket.Id,
                            Title = "New message",
                            Body = msg.Text,
                            IsRead = rngLocal.NextDouble() < 0.45,
                            CreatedAt = msg.CreatedAt
                        });
                    }
                }
            }

            // Internal notes on ~20% of assigned non-open tickets.
            if (agentId is int noteAuthor
                && ticket.Status != TicketStatus.Open
                && rngLocal.NextDouble() < 0.20)
            {
                notes.Add(new TicketNote
                {
                    TicketId = ticket.Id,
                    AuthorId = noteAuthor,
                    Text = $"Internal note for ticket {ticket.Id}: escalate checklist reviewed.",
                    CreatedAt = ticket.OpenDate.AddHours(rngLocal.Next(2, 48)),
                    UpdatedAt = ticket.OpenDate.AddHours(rngLocal.Next(49, 72)),
                    IsDeleted = false
                });
            }
        }

        const int relatedBatch = 400;
        for (var offset = 0; offset < messages.Count; offset += relatedBatch)
        {
            db.TicketMessages.AddRange(messages.Skip(offset).Take(relatedBatch));
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        // Attach a few files to earliest messages that were just saved — reload a sample.
        var sampleMessageIds = await db.TicketMessages.AsNoTracking()
            .OrderBy(m => m.Id)
            .Select(m => m.Id)
            .Take(60)
            .ToListAsync(cancellationToken);
        foreach (var messageId in sampleMessageIds)
        {
            if (rng.NextDouble() > 0.5)
                continue;
            attachments.Add(new Attachment
            {
                MessageId = messageId,
                FileUrl = $"/uploads/seed/ticket-msg-{messageId}.png",
                FileName = $"screenshot-{messageId}.png",
                FileType = "image/png",
                FileSizeKB = rng.Next(40, 900),
                UploadedAt = DateTime.UtcNow.AddDays(-rng.Next(0, AnalyticsDayWindow)),
                IsDeleted = false
            });
        }

        if (attachments.Count > 0)
        {
            db.Attachments.AddRange(attachments);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        for (var offset = 0; offset < notes.Count; offset += relatedBatch)
        {
            db.TicketNotes.AddRange(notes.Skip(offset).Take(relatedBatch));
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        for (var offset = 0; offset < notifications.Count; offset += relatedBatch)
        {
            db.Notifications.AddRange(notifications.Skip(offset).Take(relatedBatch));
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        db.ChangeTracker.AutoDetectChangesEnabled = true;

        // Refresh agent LastAssignedAt from newest assigned open tickets.
        foreach (var agent in activeAgents)
        {
            var last = tickets
                .Where(t => t.AssignedAgentId == agent.Id)
                .Select(t => t.OpenDate)
                .DefaultIfEmpty(agent.LastAssignedAt ?? windowStart)
                .Max();
            var tracked = await db.Users.FirstAsync(u => u.Id == agent.Id, cancellationToken);
            tracked.LastAssignedAt = last;
        }

        await db.SaveChangesAsync(cancellationToken);

        logger?.LogInformation(
            "Analytics seed complete: {Tickets} tickets, {Messages} messages, {Notes} notes, {Notifications} notifications, {Attachments} attachments (window {Days}d).",
            tickets.Count,
            messages.Count,
            notes.Count,
            notifications.Count,
            attachments.Count,
            AnalyticsDayWindow);

        Console.WriteLine(
            $"DbSeed analytics: tickets={tickets.Count}, messages={messages.Count}, notes={notes.Count}, notifications={notifications.Count}, attachments={attachments.Count}");
    }

    private static List<List<User>> BuildWeightedRequesterPool(IReadOnlyList<User> departmentRequesters)
    {
        // One requester user per department code, ordered like DepartmentCodes.
        var byCode = departmentRequesters
            .GroupBy(u => u.Username.Replace("demo.req.", "", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(g => g.Key.ToUpperInvariant(), g => g.First());

        var pools = new List<List<User>>(DepartmentCodes.Length);
        foreach (var code in DepartmentCodes)
        {
            if (!byCode.TryGetValue(code, out var user))
                throw new InvalidOperationException($"Missing department requester for {code}");
            pools.Add([user]);
        }

        return pools;
    }

    private static int PickDayOffset(Random rng, int windowDays)
    {
        // Weekdays heavier than weekends for a realistic intake curve.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var day = rng.Next(0, windowDays);
            var dow = (int)DateTime.UtcNow.Date.AddDays(-(windowDays - 1) + day).DayOfWeek;
            var weekend = dow is (int)DayOfWeek.Saturday or (int)DayOfWeek.Sunday;
            if (!weekend || rng.NextDouble() < 0.35)
                return day;
        }

        return rng.Next(0, windowDays);
    }

    private static TicketPriority PickPriority(Random rng)
    {
        var roll = rng.Next(100);
        if (roll < 20) return TicketPriority.Low;
        if (roll < 70) return TicketPriority.Medium;
        return TicketPriority.High;
    }

    private static TicketStatus PickStatusForAge(Random rng, double ageDays)
    {
        var roll = rng.Next(100);
        if (ageDays <= 3)
        {
            if (roll < 50) return TicketStatus.Open;
            if (roll < 80) return TicketStatus.InProgress;
            if (roll < 95) return TicketStatus.PendingRequesterResponse;
            return TicketStatus.Resolved;
        }

        if (ageDays <= 14)
        {
            if (roll < 20) return TicketStatus.Open;
            if (roll < 45) return TicketStatus.InProgress;
            if (roll < 60) return TicketStatus.PendingRequesterResponse;
            if (roll < 85) return TicketStatus.Resolved;
            return TicketStatus.Closed;
        }

        if (ageDays <= 45)
        {
            if (roll < 8) return TicketStatus.Open;
            if (roll < 18) return TicketStatus.InProgress;
            if (roll < 25) return TicketStatus.PendingRequesterResponse;
            if (roll < 65) return TicketStatus.Resolved;
            return TicketStatus.Closed;
        }

        if (roll < 3) return TicketStatus.Open;
        if (roll < 8) return TicketStatus.InProgress;
        if (roll < 10) return TicketStatus.PendingRequesterResponse;
        if (roll < 50) return TicketStatus.Resolved;
        return TicketStatus.Closed;
    }

    private static int? PickAgentId(
        Random rng,
        IReadOnlyList<User> activeAgents,
        IReadOnlyList<int> weights,
        User inactiveAgent,
        TicketStatus status,
        double ageDays)
    {
        var roll = rng.Next(100);

        // Older closed tickets may still sit on the inactive agent (deactivate does not reassign).
        if ((status is TicketStatus.Resolved or TicketStatus.Closed)
            && ageDays > 40
            && roll < 4)
        {
            return inactiveAgent.Id;
        }

        // ~3% unassigned open work.
        if ((status is TicketStatus.Open or TicketStatus.InProgress or TicketStatus.PendingRequesterResponse)
            && roll >= 97)
        {
            return null;
        }

        var pick = rng.Next(weights.Sum());
        var cumulative = 0;
        for (var i = 0; i < activeAgents.Count && i < weights.Count; i++)
        {
            cumulative += weights[i];
            if (pick < cumulative)
                return activeAgents[i].Id;
        }

        return activeAgents[^1].Id;
    }

    private static int PickWeightedIndex(Random rng, IReadOnlyList<int> weights)
    {
        var pick = rng.Next(weights.Sum());
        var cumulative = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            cumulative += weights[i];
            if (pick < cumulative)
                return i;
        }

        return weights.Count - 1;
    }
}
