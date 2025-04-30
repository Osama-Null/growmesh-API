using growmesh_API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace growmesh_API.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        // Constructor
        public ApplicationDbContext (DbContextOptions<ApplicationDbContext> options) : base(options) {}

        // Entity
        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<SavingsGoal> SavingsGoals { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Transaction -> SavingsGoal relationship to disable cascading delete
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.SavingsGoal)
                .WithMany()
                .HasForeignKey(t => t.SavingsGoalId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
