using Handball.Belgium.Rules.Quiz.Domain;
using Microsoft.EntityFrameworkCore;
using QuizManagement.Infrastructure.Configurations;

namespace QuizManagement.Infrastructure;

public class QuizManagementContext(DbContextOptions<QuizManagementContext> options) : DbContext(options)
{
    public DbSet<QuizSession> QuizSessions { get; set; } = null!;
    public DbSet<QuizTitle> QuizTitles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new QuizTitleConfiguration());
        modelBuilder.ApplyConfiguration(new QuizSessionConfiguration());
        
        base.OnModelCreating(modelBuilder);
    }
}

