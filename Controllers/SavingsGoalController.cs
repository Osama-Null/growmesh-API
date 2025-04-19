using System.Security.Claims;
using growmesh_API.Data;
using growmesh_API.DTOs.RequestDTOs;
using growmesh_API.DTOs.ResponseDTOs;
using growmesh_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace growmesh_API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SavingsGoalController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SavingsGoalController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/SavingsGoal/create
        [HttpPost("create")]
        public async Task<ActionResult<SavingsGoalDTO>> CreateSavingsGoal([FromBody] CreateSavingsGoalDTO model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.UserId == userId);
            if (bankAccount == null) return NotFound("Bank account not found");

            if (model.LockType == LockType.TimeBased && !model.TargetDate.HasValue)
                return BadRequest("Target date is required for TimeBased goals");

            if (model.LockType == LockType.AmountBased && model.TargetDate.HasValue)
                return BadRequest("Target date should not be provided for AmountBased goals");

            if (model.DepositAmount.HasValue || model.DepositFrequency.HasValue)
            {
                if (!model.DepositAmount.HasValue || model.DepositAmount <= 0)
                    return BadRequest("Deposit amount must be greater than zero if automatic deposits are enabled");

                if (!model.DepositFrequency.HasValue)
                    return BadRequest("Deposit frequency is required if automatic deposits are enabled");

                if (model.DepositFrequency == DepositFrequency.Custom &&
                    (!model.CustomDepositIntervalDays.HasValue || model.CustomDepositIntervalDays <= 0))
                    return BadRequest("Custom deposit interval must be specified and greater than zero for Custom frequency");
            }

            if (model.InitialManualPayment && model.InitialAutomaticPayment)
                return BadRequest("Cannot select both initial manual and automatic payment");

            if (model.InitialAutomaticPayment && !model.DepositAmount.HasValue)
                return BadRequest("Deposit amount is required for initial automatic payment");

            if (model.InitialManualPayment && !model.InitialManualPaymentAmount.HasValue)
                return BadRequest("Initial manual payment amount is required if manual payment is selected");

            var savingsGoal = new SavingsGoal
            {
                SavingsGoalName = model.SavingsGoalName,
                TargetAmount = model.TargetAmount,
                CurrentAmount = 0,
                TargetDate = model.TargetDate,
                Description = model.Description,
                LockType = model.LockType,
                Status = SavingsGoalStatus.InProgress,
                BankAccountId = bankAccount.BankAccountId,
                DepositAmount = model.DepositAmount,
                DepositFrequency = model.DepositFrequency,
                CustomDepositIntervalDays = model.DepositFrequency == DepositFrequency.Custom ? model.CustomDepositIntervalDays : null,
                LastDepositDate = (model.DepositAmount.HasValue && model.DepositFrequency.HasValue) ? DateTime.UtcNow : null,
                Emoji = model.Emoji,
                CreatedAt = DateTime.UtcNow,
                InitialManualPayment = model.InitialManualPayment,
                InitialAutomaticPayment = model.InitialAutomaticPayment
            };

            _context.SavingsGoals.Add(savingsGoal);
            await _context.SaveChangesAsync();

            // Handle initial manual payment
            if (model.InitialManualPayment && model.InitialManualPaymentAmount.HasValue)
            {
                var amount = model.InitialManualPaymentAmount.Value;

                if (savingsGoal.LockType == LockType.AmountBased && amount > savingsGoal.TargetAmount)
                    amount = savingsGoal.TargetAmount;

                if (bankAccount.Balance >= amount)
                {
                    bankAccount.Balance -= amount;
                    savingsGoal.CurrentAmount += amount;

                    var transaction = new Transaction
                    {
                        Amount = amount,
                        TransactionDate = DateTime.UtcNow,
                        Type = TransactionType.TransferToGoal,
                        BankAccountId = bankAccount.BankAccountId,
                        SavingsGoalId = savingsGoal.SavingsGoalId
                    };

                    _context.Transactions.Add(transaction);
                }
                else
                {
                    return BadRequest("Insufficient funds for initial manual deposit");
                }
            }

            // Handle initial automatic payment
            if (model.InitialAutomaticPayment && model.DepositAmount.HasValue)
            {
                var amount = model.DepositAmount.Value;

                if (savingsGoal.LockType == LockType.AmountBased && amount > savingsGoal.TargetAmount)
                    amount = savingsGoal.TargetAmount;

                if (bankAccount.Balance >= amount)
                {
                    bankAccount.Balance -= amount;
                    savingsGoal.CurrentAmount += amount;

                    var transaction = new Transaction
                    {
                        Amount = amount,
                        TransactionDate = DateTime.UtcNow,
                        Type = TransactionType.TransferToGoal,
                        BankAccountId = bankAccount.BankAccountId,
                        SavingsGoalId = savingsGoal.SavingsGoalId
                    };

                    _context.Transactions.Add(transaction);
                }
                else
                {
                    return BadRequest("Insufficient funds for initial automatic deposit");
                }
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSavingsGoal), new { id = savingsGoal.SavingsGoalId }, new SavingsGoalDTO
            {
                SavingsGoalId = savingsGoal.SavingsGoalId,
                SavingsGoalName = savingsGoal.SavingsGoalName,
                TargetAmount = savingsGoal.TargetAmount,
                CurrentAmount = savingsGoal.CurrentAmount,
                TargetDate = savingsGoal.TargetDate,
                Description = savingsGoal.Description,
                LockType = savingsGoal.LockType,
                Status = savingsGoal.Status,
                BankAccountId = savingsGoal.BankAccountId,
                DepositAmount = savingsGoal.DepositAmount,
                DepositFrequency = savingsGoal.DepositFrequency,
                CustomDepositIntervalDays = savingsGoal.CustomDepositIntervalDays,
                Emoji = savingsGoal.Emoji,
                CreatedAt = savingsGoal.CreatedAt,
                CompletedAt = savingsGoal.CompletedAt,
                InitialManualPayment = savingsGoal.InitialManualPayment,
                InitialAutomaticPayment = savingsGoal.InitialAutomaticPayment
            });
        }

        // GET: api/SavingsGoal/get-all
        [HttpGet("get-all")]
        public async Task<ActionResult<IEnumerable<SavingsGoalDTO>>> GetAllSavingsGoals()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            var savingsGoals = bankAccount.SavingsGoals
                .Where(sg => sg.DeletedAt == null)
                .Select(sg => new SavingsGoalDTO
            {
                    SavingsGoalId = sg.SavingsGoalId,
                    SavingsGoalName = sg.SavingsGoalName,
                    TargetAmount = sg.TargetAmount,
                    CurrentAmount = sg.CurrentAmount,
                    TargetDate = sg.TargetDate,
                    Description = sg.Description,
                    LockType = sg.LockType,
                    Status = sg.Status,
                    BankAccountId = sg.BankAccountId,
                    Emoji = sg.Emoji,
                    CreatedAt = sg.CreatedAt,
                    CompletedAt = sg.CompletedAt,
                    InitialManualPayment = sg.InitialManualPayment,
                    InitialAutomaticPayment = sg.InitialAutomaticPayment,
                    DeletedAt = sg.DeletedAt
                }).ToList();

            return savingsGoals;
        }

        // GET: api/SavingsGoal/get/{id}
        [HttpGet("get/{id}")]
        public async Task<ActionResult<SavingsGoalDTO>> GetSavingsGoal(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            return new SavingsGoalDTO
            {
                SavingsGoalId = savingsGoal.SavingsGoalId,
                SavingsGoalName = savingsGoal.SavingsGoalName,
                TargetAmount = savingsGoal.TargetAmount,
                CurrentAmount = savingsGoal.CurrentAmount,
                TargetDate = savingsGoal.TargetDate,
                Description = savingsGoal.Description,
                LockType = savingsGoal.LockType,
                Status = savingsGoal.Status,
                BankAccountId = savingsGoal.BankAccountId,
                Emoji = savingsGoal.Emoji
            };
        }

        // PUT: api/SavingsGoal/update/{id}
        [HttpPut("update/{id}")]
        public async Task<ActionResult<SavingsGoalDTO>> UpdateSavingsGoal(int id, [FromBody] UpdateSavingsGoalDTO model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (savingsGoal.Status == SavingsGoalStatus.Completed || savingsGoal.Status == SavingsGoalStatus.MarkDone)
                return BadRequest("Cannot update a completed or marked-done savings goal");

            if (savingsGoal.Status == SavingsGoalStatus.Completed)
                return BadRequest("Cannot update a completed savings goal");

            if (model.TargetAmount.HasValue)
            {
                if (model.TargetAmount.Value <= 0)
                    return BadRequest("Target amount must be greater than zero");
                if (model.TargetAmount.Value < savingsGoal.CurrentAmount)
                    return BadRequest("Target amount cannot be less than the current amount");
                savingsGoal.TargetAmount = model.TargetAmount.Value;
            }

            if (model.LockType.HasValue)
            {
                savingsGoal.LockType = model.LockType.Value;
                if (savingsGoal.LockType == LockType.TimeBased && !savingsGoal.TargetDate.HasValue)
                    return BadRequest("Target date is required for TimeBased goals");
                if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.TargetDate.HasValue)
                    return BadRequest("Target date should not be provided for AmountBased goals");
            }

            if (model.SavingsGoalName != null) savingsGoal.SavingsGoalName = model.SavingsGoalName;
            if (model.TargetDate.HasValue) savingsGoal.TargetDate = model.TargetDate.Value;
            if (model.Description != null) savingsGoal.Description = model.Description;
            if (model.Emoji != null) savingsGoal.Emoji = model.Emoji;

            if (model.LockType.HasValue)
            {
                savingsGoal.LockType = model.LockType.Value;
            }

            // Validate deposit fields if automatic deposits are being updated
            if (model.DepositAmount.HasValue || model.DepositFrequency.HasValue)
            {
                if (!model.DepositAmount.HasValue || model.DepositAmount <= 0)
                    return BadRequest("Deposit amount must be greater than zero if automatic deposits are enabled");

                if (!model.DepositFrequency.HasValue)
                    return BadRequest("Deposit frequency is required if automatic deposits are enabled");

                if (model.DepositFrequency == DepositFrequency.Custom && (!model.CustomDepositIntervalDays.HasValue || model.CustomDepositIntervalDays <= 0))
                    return BadRequest("Custom deposit interval must be specified and greater than zero for Custom frequency");

                savingsGoal.DepositAmount = model.DepositAmount.Value;
                savingsGoal.DepositFrequency = model.DepositFrequency.Value;
                savingsGoal.CustomDepositIntervalDays = model.DepositFrequency == DepositFrequency.Custom ? model.CustomDepositIntervalDays : null;
                savingsGoal.LastDepositDate = DateTime.UtcNow; // Reset LastDepositDate if automatic deposits are enabled
            }
            else if (model.DepositAmount == null && model.DepositFrequency == null)
            {
                // If automatic deposits are being disabled, clear the fields
                savingsGoal.DepositAmount = null;
                savingsGoal.DepositFrequency = null;
                savingsGoal.CustomDepositIntervalDays = null;
                savingsGoal.LastDepositDate = null;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SavingsGoalExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return Ok(new SavingsGoalDTO
            {
                SavingsGoalId = savingsGoal.SavingsGoalId,
                SavingsGoalName = savingsGoal.SavingsGoalName,
                TargetAmount = savingsGoal.TargetAmount,
                CurrentAmount = savingsGoal.CurrentAmount,
                TargetDate = savingsGoal.TargetDate,
                Description = savingsGoal.Description,
                LockType = savingsGoal.LockType,
                Status = savingsGoal.Status,
                BankAccountId = savingsGoal.BankAccountId,
                DepositAmount = savingsGoal.DepositAmount,
                DepositFrequency = savingsGoal.DepositFrequency,
                CustomDepositIntervalDays = savingsGoal.CustomDepositIntervalDays
            });
        }

        // DELETE: api/SavingsGoal/delete/{id}
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteSavingsGoal(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            // Mark the goal as deleted instead of removing it
            savingsGoal.DeletedAt = DateTime.UtcNow;
            savingsGoal.Status = SavingsGoalStatus.Completed;

            // Transfer remaining balance back to bank account
            if (savingsGoal.CurrentAmount > 0)
            {
                var bankAccount = await _context.BankAccounts
                    .FirstOrDefaultAsync(ba => ba.BankAccountId == savingsGoal.BankAccountId);

                if (bankAccount != null)
                {
                    bankAccount.Balance += savingsGoal.CurrentAmount;

                    var transferBackTransaction = new Transaction
                    {
                        Amount = savingsGoal.CurrentAmount,
                        TransactionDate = DateTime.UtcNow,
                        Type = TransactionType.TransferFromGoal,
                        BankAccountId = bankAccount.BankAccountId,
                        SavingsGoalId = savingsGoal.SavingsGoalId
                    };
                    _context.Transactions.Add(transferBackTransaction);

                    savingsGoal.CurrentAmount = 0;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Savings goal deleted successfully" });
        }

        // POST: api/SavingsGoal/deposit/{id}
        [HttpPost("deposit/{id}")]
        public async Task<IActionResult> Deposit(int id, [FromBody] TransferAmountDTO transferDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var amount = transferDto.Amount;
            if (amount <= 0) return BadRequest("Amount must be greater than zero");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (savingsGoal.Status == SavingsGoalStatus.Completed)
                return BadRequest("Cannot deposit into a completed savings goal");

            if (savingsGoal.Status == SavingsGoalStatus.MarkDone)
                return BadRequest("Cannot deposit into a savings goal that has reached its target (MarkDone). Please mark the goal as done to transfer funds to your balance.");

            var bankAccount = savingsGoal.BankAccount;
            if (bankAccount.Balance < amount) return BadRequest("Insufficient funds in bank account");

            bankAccount.Balance -= amount;
            savingsGoal.CurrentAmount += amount;

            if (savingsGoal.LockType == LockType.TimeBased)
                savingsGoal.LastDepositDate = DateTime.UtcNow;

            var transaction = new Transaction
            {
                Amount = amount,
                TransactionDate = DateTime.UtcNow,
                Type = TransactionType.TransferToGoal,
                BankAccountId = bankAccount.BankAccountId,
                SavingsGoalId = savingsGoal.SavingsGoalId
            };

            _context.Transactions.Add(transaction);

            // Check if the goal should be marked as done
            bool shouldMarkDone = (savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate <= DateTime.UtcNow) ||
                                  (savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount);

            if (shouldMarkDone)
            {
                savingsGoal.Status = SavingsGoalStatus.MarkDone;
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Deposit successful" });
        }

        // POST: api/SavingsGoal/withdraw/{id}
        [HttpPost("withdraw/{id}")]
        public async Task<IActionResult> Withdraw(int id, [FromBody] TransferAmountDTO transferDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var amount = transferDto.Amount;
            if (amount <= 0) return BadRequest("Amount must be greater than zero");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (savingsGoal.Status == SavingsGoalStatus.Completed || savingsGoal.Status == SavingsGoalStatus.MarkDone)
                return BadRequest("Cannot withdraw from a completed or marked-done savings goal");

            if (savingsGoal.Status == SavingsGoalStatus.Completed)
                return BadRequest("Cannot withdraw from a completed savings goal");

            if (savingsGoal.CurrentAmount < amount) return BadRequest("Insufficient funds in savings goal");

            var bankAccount = savingsGoal.BankAccount;
            bankAccount.Balance += amount;
            savingsGoal.CurrentAmount -= amount;

            var transaction = new Transaction
            {
                Amount = amount,
                TransactionDate = DateTime.UtcNow,
                Type = TransactionType.TransferFromGoal,
                BankAccountId = bankAccount.BankAccountId,
                SavingsGoalId = savingsGoal.SavingsGoalId
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Withdrawal successful" });
        }

        // POST: api/SavingsGoal/unlock/{id}
        [HttpPost("unlock/{id}")]
        public async Task<IActionResult> Unlock(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (savingsGoal.Status == SavingsGoalStatus.Completed)
                return BadRequest("Savings goal is already completed");

            if (savingsGoal.Status == SavingsGoalStatus.Unlocked)
                return BadRequest("Savings goal is already unlocked");

            savingsGoal.Status = SavingsGoalStatus.Unlocked;

            // Check if the savings goal should be marked as done
            bool shouldMarkDone = (savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate <= DateTime.UtcNow) ||
                                  (savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount);

            if (shouldMarkDone)
            {
                savingsGoal.Status = SavingsGoalStatus.MarkDone;
            }
            else
            {
                // Transfer funds to balance if not transitioning to MarkDone
                if (savingsGoal.CurrentAmount > 0)
                {
                    var bankAccount = savingsGoal.BankAccount;
                    var amountToTransfer = savingsGoal.CurrentAmount;

                    bankAccount.Balance += amountToTransfer;
                    savingsGoal.CurrentAmount = 0;

                    var transaction = new Transaction
                    {
                        Amount = amountToTransfer,
                        TransactionDate = DateTime.UtcNow,
                        Type = TransactionType.TransferFromGoal,
                        BankAccountId = bankAccount.BankAccountId,
                        SavingsGoalId = savingsGoal.SavingsGoalId
                    };

                    _context.Transactions.Add(transaction);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Savings goal unlocked successfully" });
        }

        // GET: api/SavingsGoal/get-savings-trend/
        [HttpGet("get-savings-trend")]
        public async Task<ActionResult<IEnumerable<SavingsTrendDTO>>> GetSavingsTrend([FromQuery] string periodType, [FromQuery] int periods = 7)
        {
            // Validate input parameters
            if (periods <= 0) return BadRequest("Periods must be greater than zero");

            var validPeriodTypes = new[] { "day", "month", "year" };
            if (!validPeriodTypes.Contains(periodType.ToLower()))
                return BadRequest("Invalid period type. Use 'day', 'month', or 'year'");

            // Verify user authentication
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Fetch bank account with savings goals and transactions
            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .ThenInclude(sg => sg.Transactions)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);
            if (bankAccount == null) return NotFound("Bank account not found");

            var trendData = new List<SavingsTrendDTO>();
            var now = DateTime.UtcNow;

            // Calculate savings for each period
            for (int i = periods - 1; i >= 0; i--)
            {
                DateTime periodEnd;
                switch (periodType.ToLower())
                {
                    case "day":
                        periodEnd = now.Date.AddDays(-i);
                        break;
                    case "month":
                        periodEnd = now.Date.AddMonths(-i).AddDays(-now.Day + 1).AddMonths(1).AddDays(-1);
                        break;
                    case "year":
                        periodEnd = now.Date.AddYears(-i).AddDays(-now.DayOfYear + 1).AddYears(1).AddDays(-1);
                        break;
                    default:
                        return BadRequest("Invalid period type");
                }

                decimal totalSavings = 0;

                foreach (var goal in bankAccount.SavingsGoals)
                {
                    // Skip goals created after the period, completed before it, or deleted before it
                    if (goal.CreatedAt > periodEnd ||
                        (goal.CompletedAt.HasValue && goal.CompletedAt < periodEnd) ||
                        (goal.DeletedAt.HasValue && goal.DeletedAt < periodEnd))
                        continue;

                    // Calculate net savings up to period end
                    var transactionsBeforePeriod = goal.Transactions
                        .Where(t => t.TransactionDate <= periodEnd &&
                                    (t.Type == TransactionType.TransferToGoal || t.Type == TransactionType.TransferFromGoal))
                        .ToList();

                    decimal amountAtPeriodEnd = transactionsBeforePeriod
                        .Sum(t => t.Type == TransactionType.TransferToGoal ? t.Amount : -t.Amount);

                    totalSavings += Math.Max(0, amountAtPeriodEnd);
                }

                trendData.Add(new SavingsTrendDTO
                {
                    PeriodEnd = periodEnd,
                    TotalSavings = totalSavings,
                    Difference = 0
                });
            }

            // Calculate differences between periods
            for (int i = 1; i < trendData.Count; i++)
            {
                trendData[i].Difference = trendData[i].TotalSavings - trendData[i - 1].TotalSavings;
            }
            if (trendData.Count > 0)
                trendData[0].Difference = 0;

            return Ok(trendData);
        }

        // POST: api/SavingsGoal/mark-as-done/{id}
        [HttpPost("mark-as-done/{id}")]
        public async Task<IActionResult> MarkAsDone(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (savingsGoal.Status != SavingsGoalStatus.MarkDone)
                return BadRequest("Savings goal must be in MarkDone status to be completed");

            if (savingsGoal.DeletedAt.HasValue)
                return BadRequest("Cannot mark a deleted savings goal as done");

            // Transfer remaining balance to bank account
            if (savingsGoal.CurrentAmount > 0)
            {
                var bankAccount = savingsGoal.BankAccount;
                var amountToTransfer = savingsGoal.CurrentAmount;

                bankAccount.Balance += amountToTransfer;
                savingsGoal.CurrentAmount = 0;

                var transaction = new Transaction
                {
                    Amount = amountToTransfer,
                    TransactionDate = DateTime.UtcNow,
                    Type = TransactionType.TransferFromGoal,
                    BankAccountId = bankAccount.BankAccountId,
                    SavingsGoalId = savingsGoal.SavingsGoalId
                };

                _context.Transactions.Add(transaction);
            }

            savingsGoal.Status = SavingsGoalStatus.Completed;
            savingsGoal.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Savings goal marked as done and funds transferred to balance" });
        }

        private bool SavingsGoalExists(int id)
        {
            return _context.SavingsGoals.Any(e => e.SavingsGoalId == id);
        }
    }
}