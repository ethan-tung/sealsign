using Microsoft.EntityFrameworkCore;
using SealSign.Core.Entities;

namespace SealSign.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Seal> Seals => Set<Seal>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<SignRecord> SignRecords => Set<SignRecord>();
    public DbSet<VerifyLog> VerifyLogs => Set<VerifyLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasCharSet("utf8mb4");

        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Phone).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
        });

        b.Entity<Seal>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.Property(x => x.ImageObjectKey).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany(u => u.Seals).HasForeignKey(x => x.UserId);
        });

        b.Entity<Document>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ObjectKey).HasMaxLength(256).IsRequired();
            e.Property(x => x.SignedObjectKey).HasMaxLength(256);
            e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Sha256);
        });

        b.Entity<SignRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RecordNo).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.RecordNo).IsUnique();
            e.Property(x => x.SignedDocHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.SignedDocHash);
            e.Property(x => x.PrevHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.ChainHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.PlatformSignature).HasMaxLength(1024).IsRequired();
            e.Property(x => x.SignedObjectKey).HasMaxLength(256).IsRequired();
            e.Property(x => x.Ip).HasMaxLength(45);
        });

        b.Entity<VerifyLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DocHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.CheckedAt);
        });
    }
}
