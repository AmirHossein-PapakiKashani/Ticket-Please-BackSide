using Microsoft.EntityFrameworkCore;
using Ticket.Application.Data;
using Ticket.Domain;
using TicketEntity = Ticket.Domain.Ticket;

namespace Ticket.Infrastructure.Persistence;

public sealed class TicketDbContext(DbContextOptions<TicketDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<ProviderSubscription> ProviderSubscriptions => Set<ProviderSubscription>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TicketEntity> Tickets => Set<TicketEntity>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<TicketNote> TicketNotes => Set<TicketNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.PassHash).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.RoleName);
        });

        modelBuilder.Entity<Provider>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<Client>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<Plan>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);
        });

        modelBuilder.Entity<ProviderSubscription>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.MoneyPaid).HasPrecision(18, 2);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash);
        });

        modelBuilder.Entity<PasswordResetToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Token).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Token);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasMaxLength(500);
            e.Property(x => x.Type).HasConversion<int>();
        });

        modelBuilder.Entity<TicketEntity>(e =>
        {
            e.ToTable("Tickets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Topic).HasMaxLength(300).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Priority).HasConversion<int>();
            e.HasIndex(x => new { x.ProviderId, x.ClientId });
        });

        modelBuilder.Entity<TicketMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).IsRequired();
            e.HasMany(x => x.Attachments).WithOne().HasForeignKey(a => a.MessageId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Attachment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileUrl).HasMaxLength(500).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(300).IsRequired();
            e.Property(x => x.FileType).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<TicketNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).IsRequired();
        });
    }
}
