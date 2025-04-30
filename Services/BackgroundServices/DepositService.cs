using growmesh_API.Data;
using growmesh_API.Models;
using Microsoft.EntityFrameworkCore;

namespace growmesh_API.Services.BackgroundServices
{
    public class DepositService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DepositService> _logger;

        public DepositService(IServiceProvider serviceProvider, ILogger<DepositService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDepositsAndTransfers();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing deposits and transfers");
                }

                // Run every day at midnight
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1);
                var delay = nextRun - now;
                await Task.Delay(delay, stoppingToken);
            }
        }

        private async Task ProcessDepositsAndTransfers()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Fetch savings goals eligible for deposits
                var savingsGoalsForDeposits = await context.SavingsGoals
                    .Include(sg => sg.BankAccount)
                    .Where(sg => sg.DepositAmount.HasValue &&
                                 sg.DepositFrequency.HasValue &&
                                 sg.Status == SavingsGoalStatus.InProgress &&
                                 sg.DeletedAt == null)
                    .ToListAsync();

                foreach (var savingsGoal in savingsGoalsForDeposits)
                {
                    if (savingsGoal.LastDepositDate == null)
                        savingsGoal.LastDepositDate = DateTime.UtcNow;

                    // Check if the goal should be marked as done
                    if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount)
                    {
                        if (savingsGoal.Status != SavingsGoalStatus.MarkDone)
                        {
                            savingsGoal.Status = SavingsGoalStatus.MarkDone;
                        }
                        continue;
                    }

                    if (savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate <= DateTime.UtcNow)
                    {
                        if (savingsGoal.Status != SavingsGoalStatus.Unlocked && savingsGoal.Status != SavingsGoalStatus.MarkDone)
                            savingsGoal.Status = SavingsGoalStatus.Unlocked;
                        continue;
                    }

                    // Calculate deposit interval
                    int daysSinceLastDeposit = (DateTime.UtcNow - savingsGoal.LastDepositDate.Value).Days;
                    int intervalDays = savingsGoal.DepositFrequency switch
                    {
                        DepositFrequency.Monthly => 30,
                        DepositFrequency.Weekly => 7,
                        DepositFrequency.Custom => savingsGoal.CustomDepositIntervalDays ?? 1,
                        _ => throw new InvalidOperationException("Invalid deposit frequency")
                    };

                    // Process deposit if interval is met
                    if (daysSinceLastDeposit >= intervalDays)
                    {
                        var bankAccount = savingsGoal.BankAccount;
                        var amount = savingsGoal.DepositAmount.Value;

                        // Cap the deposit amount for AmountBased goals
                        if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.TargetAmount.HasValue)
                        {
                            decimal remainingAmount = savingsGoal.TargetAmount.Value - savingsGoal.CurrentAmount;
                            if (amount > remainingAmount)
                                amount = remainingAmount;
                        }

                        if (bankAccount.Balance >= amount)
                        {
                            bankAccount.Balance -= amount;
                            savingsGoal.CurrentAmount += amount;
                            savingsGoal.LastDepositDate = DateTime.UtcNow;

                            var transaction = new Transaction
                            {
                                Amount = amount,
                                TransactionDate = DateTime.UtcNow,
                                Type = TransactionType.TransferToGoal,
                                BankAccountId = bankAccount.BankAccountId,
                                SavingsGoalId = savingsGoal.SavingsGoalId
                            };

                            context.Transactions.Add(transaction);

                            // Update status if goal is met
                            if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount)
                            {
                                savingsGoal.Status = SavingsGoalStatus.MarkDone;
                            }
                            else if (savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate <= DateTime.UtcNow)
                            {
                                savingsGoal.Status = SavingsGoalStatus.Unlocked;
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Insufficient funds in bank account {bankAccount.BankAccountId} for savings goal {savingsGoal.SavingsGoalId}");
                        }
                    }
                }

                // Handle transfers for goals marked as done and confirmed by the user
                // (This will be handled by a new endpoint, so we can remove the automatic transfer logic here)

                await context.SaveChangesAsync();
            }
        }
    }
}