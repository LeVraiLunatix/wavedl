using Microsoft.EntityFrameworkCore;

namespace WaveDL.Data;

public sealed class WaveDlDbContext(DbContextOptions<WaveDlDbContext> options) : DbContext(options)
{
    public DbSet<HistoryRecord> History => Set<HistoryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HistoryRecord>();
        entity.ToTable("History");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Title).IsRequired();
        entity.Property(x => x.Artist).HasDefaultValue(string.Empty);
        entity.Property(x => x.Format).HasMaxLength(32);
        entity.Property(x => x.Quality).HasMaxLength(64);
        entity.HasIndex(x => x.DownloadedAt);
        entity.HasIndex(x => x.FilePath);
    }
}
