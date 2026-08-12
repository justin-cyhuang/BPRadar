using Microsoft.EntityFrameworkCore;

namespace BPRadar.Web.Data;

public sealed class BPRadarDbContext(DbContextOptions<BPRadarDbContext> options)
    : DbContext(options)
{
    public DbSet<Framework> Frameworks => Set<Framework>();
    public DbSet<Domain> Domains => Set<Domain>();
    public DbSet<Control> Controls => Set<Control>();
    public DbSet<SurveyTemplate> SurveyTemplates => Set<SurveyTemplate>();
    public DbSet<SurveyQuestion> SurveyQuestions => Set<SurveyQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Framework>(entity =>
        {
            entity.Property(framework => framework.Name).HasMaxLength(200);
            entity.Property(framework => framework.Version).HasMaxLength(50);
            entity.HasIndex(framework => new { framework.Name, framework.Version }).IsUnique();
        });

        modelBuilder.Entity<Domain>(entity =>
        {
            entity.Property(domain => domain.Code).HasMaxLength(50);
            entity.HasIndex(domain => new { domain.FrameworkId, domain.Code }).IsUnique();
        });

        modelBuilder.Entity<Control>(entity =>
        {
            entity.Property(control => control.Code).HasMaxLength(50);
            entity.HasIndex(control => new { control.DomainId, control.Code }).IsUnique();
        });

        modelBuilder.Entity<SurveyTemplate>(entity =>
        {
            entity.Property(template => template.Name).HasMaxLength(200);
            entity.Property(template => template.Cadence).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(template => template.Name).IsUnique();
            entity.HasOne(template => template.Framework)
                .WithMany()
                .HasForeignKey(template => template.FrameworkId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SurveyQuestion>(entity =>
        {
            entity.Property(question => question.Code).HasMaxLength(50);
            entity.HasIndex(question => new { question.SurveyTemplateId, question.Code }).IsUnique();
            entity.HasOne(question => question.SurveyTemplate)
                .WithMany(template => template.Questions)
                .HasForeignKey(question => question.SurveyTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(question => question.Framework)
                .WithMany()
                .HasForeignKey(question => question.FrameworkId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(question => question.Domain)
                .WithMany()
                .HasForeignKey(question => question.DomainId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(question => question.Control)
                .WithMany()
                .HasForeignKey(question => question.ControlId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
