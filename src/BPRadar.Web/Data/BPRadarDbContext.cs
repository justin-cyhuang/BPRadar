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
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<SurveySubmission> SurveySubmissions => Set<SurveySubmission>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<ViolationMatch> ViolationMatches => Set<ViolationMatch>();

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

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.Property(organization => organization.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<Issue>(entity =>
        {
            entity.Property(issue => issue.Title).HasMaxLength(200);
            entity.Property(issue => issue.MatchingStatus)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.HasOne(issue => issue.Organization)
                .WithMany(organization => organization.Issues)
                .HasForeignKey(issue => issue.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ViolationMatch>(entity =>
        {
            entity.Property(match => match.ReviewStatus)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(match => match.MatchScore).HasPrecision(5, 4);
            entity.HasIndex(match => new { match.IssueId, match.ControlId })
                .IsUnique();
            entity.HasOne(match => match.Issue)
                .WithMany(issue => issue.ViolationMatches)
                .HasForeignKey(match => match.IssueId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(match => match.Control)
                .WithMany()
                .HasForeignKey(match => match.ControlId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SurveySubmission>(entity =>
        {
            entity.Property(submission => submission.Label).HasMaxLength(200);
            entity.HasOne(submission => submission.Organization)
                .WithMany(organization => organization.SurveySubmissions)
                .HasForeignKey(submission => submission.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(submission => submission.SurveyTemplate)
                .WithMany()
                .HasForeignKey(submission => submission.SurveyTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(submission => new
            {
                submission.OrganizationId,
                submission.SurveyTemplateId,
                submission.SnapshotDate
            }).IsUnique();
        });

        modelBuilder.Entity<SurveyResponse>(entity =>
        {
            entity.Property(response => response.ResponseLevel)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.HasIndex(response => new
            {
                response.SurveySubmissionId,
                response.SurveyQuestionId
            }).IsUnique();
            entity.HasOne(response => response.SurveySubmission)
                .WithMany(submission => submission.Responses)
                .HasForeignKey(response => response.SurveySubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(response => response.SurveyQuestion)
                .WithMany()
                .HasForeignKey(response => response.SurveyQuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
