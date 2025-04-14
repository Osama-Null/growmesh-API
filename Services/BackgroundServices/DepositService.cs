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
                var now = DateTime.Now;
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

                // Process automatic deposits for both TimeBased and AmountBased savings goals
                var savingsGoalsForDeposits = await context.SavingsGoals
                    .Include(sg => sg.BankAccount)
                    .Where(sg => sg.DepositAmount.HasValue &&
                                 sg.DepositFrequency.HasValue &&
                                 sg.Status == SavingsGoalStatus.InProgress)
                    .ToListAsync();

                foreach (var savingsGoal in savingsGoalsForDeposits)
                {
                    // Skip if LastDepositDate is null (should be set when automatic deposits are enabled)
                    if (savingsGoal.LastDepositDate == null)
                    {
                        savingsGoal.LastDepositDate = DateTime.Now; // Initialize for the first deposit
                    }

                    // Check if the goal is AmountBased and has reached the TargetAmount
                    if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount)
                    {
                        savingsGoal.Status = SavingsGoalStatus.Completed;
                        continue; // Skip further deposits
                    }

                    // Check if the goal is TimeBased and has reached the TargetDate
                    if (savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate <= DateTime.Now)
                    {
                        savingsGoal.Status = SavingsGoalStatus.Unlocked; // Mark as Unlocked, not Completed
                        continue; // Skip further deposits
                    }

                    // Check if it's time for the next deposit
                    int daysSinceLastDeposit = (DateTime.Now - savingsGoal.LastDepositDate.Value).Days;
                    int intervalDays = savingsGoal.DepositFrequency switch
                    {
                        DepositFrequency.Monthly => 30,
                        DepositFrequency.Weekly => 7,
                        DepositFrequency.Custom => savingsGoal.CustomDepositIntervalDays ?? 1,
                        _ => throw new InvalidOperationException("Invalid deposit frequency")
                    };

                    if (daysSinceLastDeposit >= intervalDays)
                    {
                        var bankAccount = savingsGoal.BankAccount;
                        var amount = savingsGoal.DepositAmount.Value;

                        // Cap the deposit amount for AmountBased goals to avoid over-saving
                        if (savingsGoal.LockType == LockType.AmountBased)
                        {
                            decimal remainingAmount = savingsGoal.TargetAmount - savingsGoal.CurrentAmount;
                            if (amount > remainingAmount)
                            {
                                amount = remainingAmount; // Cap the deposit
                            }
                        }

                        if (bankAccount.Balance >= amount)
                        {
                            bankAccount.Balance -= amount;
                            savingsGoal.CurrentAmount += amount;
                            savingsGoal.LastDepositDate = DateTime.Now;

                            var transaction = new Transaction
                            {
                                Amount = amount,
                                TransactionDate = DateTime.Now,
                                Type = TransactionType.TransferToGoal,
                                BankAccountId = bankAccount.BankAccountId,
                                SavingsGoalId = savingsGoal.SavingsGoalId
                            };

                            context.Transactions.Add(transaction);

                            // Check if the goal is completed after the deposit
                            if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount)
                            {
                                savingsGoal.Status = SavingsGoalStatus.Completed;
                            }
                            else if (savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate <= DateTime.Now)
                            {
                                savingsGoal.Status = SavingsGoalStatus.Unlocked; // Mark as Unlocked, not Completed
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Insufficient funds in bank account {bankAccount.BankAccountId} for savings goal {savingsGoal.SavingsGoalId}");
                        }
                    }
                }

                // Process completed and unlocked savings goals
                var savingsGoalsForTransfer = await context.SavingsGoals
                    .Include(sg => sg.BankAccount)
                    .Where(sg => sg.Status == SavingsGoalStatus.Unlocked &&
                                 sg.CurrentAmount > 0 &&
                                 ((sg.LockType == LockType.TimeBased && sg.TargetDate <= DateTime.Now) ||
                                  (sg.LockType == LockType.AmountBased && sg.CurrentAmount >= sg.TargetAmount)))
                    .ToListAsync();

                foreach (var savingsGoal in savingsGoalsForTransfer)
                {
                    var bankAccount = savingsGoal.BankAccount;
                    var amountToTransfer = savingsGoal.CurrentAmount;

                    bankAccount.Balance += amountToTransfer;
                    savingsGoal.CurrentAmount = 0; // Reset CurrentAmount to 0 after transfer
                    savingsGoal.Status = SavingsGoalStatus.Completed; // Mark as completed

                    var transaction = new Transaction
                    {
                        Amount = amountToTransfer,
                        TransactionDate = DateTime.Now,
                        Type = TransactionType.TransferFromGoal,
                        BankAccountId = bankAccount.BankAccountId,
                        SavingsGoalId = savingsGoal.SavingsGoalId
                    };

                    context.Transactions.Add(transaction);
                    _logger.LogInformation($"Transferred {amountToTransfer} from savings goal {savingsGoal.SavingsGoalId} to bank account {bankAccount.BankAccountId}");
                }

                await context.SaveChangesAsync();
            }
        }
    }
}