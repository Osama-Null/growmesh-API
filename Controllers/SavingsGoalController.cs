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
            {
                ModelState.AddModelError("TargetDate", "Target date is required for TimeBased goals");
                return BadRequest(ModelState);
            }

            if (model.LockType == LockType.AmountBased && model.TargetDate.HasValue)
            {
                ModelState.AddModelError("TargetDate", "Target date should not be provided for AmountBased goals");
                return BadRequest(ModelState);
            }

            if (model.DepositAmount.HasValue || model.DepositFrequency.HasValue)
            {
                if (!model.DepositAmount.HasValue || model.DepositAmount <= 0)
                    return BadRequest("Deposit amount must be greater than zero if automatic deposits are enabled");

                if (!model.DepositFrequency.HasValue)
                    return BadRequest("Deposit frequency is required if automatic deposits are enabled");

                if (model.DepositFrequency == DepositFrequency.Custom && (!model.CustomDepositIntervalDays.HasValue || model.CustomDepositIntervalDays <= 0))
                    return BadRequest("Custom deposit interval must be specified and greater than zero for Custom frequency");
            }

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
                LastDepositDate = (model.DepositAmount.HasValue && model.DepositFrequency.HasValue) ? DateTime.Now : null // Set initial deposit date if automatic deposits are enabled
            };

            _context.SavingsGoals.Add(savingsGoal);
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
                CustomDepositIntervalDays = savingsGoal.CustomDepositIntervalDays
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

            var savingsGoals = bankAccount.SavingsGoals.Select(sg => new SavingsGoalDTO
            {
                SavingsGoalId = sg.SavingsGoalId,
                SavingsGoalName = sg.SavingsGoalName,
                TargetAmount = sg.TargetAmount,
                CurrentAmount = sg.CurrentAmount,
                TargetDate = sg.TargetDate,
                Description = sg.Description,
                LockType = sg.LockType,
                Status = sg.Status,
                BankAccountId = sg.BankAccountId
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
                //TargetDate = savingsGoal.TargetDate,
                Description = savingsGoal.Description,
                LockType = savingsGoal.LockType,
                Status = savingsGoal.Status,
                BankAccountId = savingsGoal.BankAccountId
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

            if (model.SavingsGoalName != null) savingsGoal.SavingsGoalName = model.SavingsGoalName;
            if (model.TargetDate.HasValue) savingsGoal.TargetDate = model.TargetDate.Value;
            if (model.Description != null) savingsGoal.Description = model.Description;

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
                savingsGoal.LastDepositDate = DateTime.Now; // Reset LastDepositDate if automatic deposits are enabled
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

            // Update related transactions to set SavingsGoalId to null
            var transactions = await _context.Transactions
                .Where(t => t.SavingsGoalId == savingsGoal.SavingsGoalId)
                .ToListAsync();
            foreach (var transaction in transactions)
            {
                transaction.SavingsGoalId = null;
            }

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
                        TransactionDate = DateTime.Now,
                        Type = TransactionType.TransferFromGoal,
                        BankAccountId = bankAccount.BankAccountId,
                        SavingsGoalId = savingsGoal.SavingsGoalId
                    };
                    _context.Transactions.Add(transferBackTransaction);
                }
            }

            _context.SavingsGoals.Remove(savingsGoal);
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

            var bankAccount = savingsGoal.BankAccount;
            if (bankAccount.Balance < amount) return BadRequest("Insufficient funds in bank account");

            bankAccount.Balance -= amount;
            savingsGoal.CurrentAmount += amount;

            if (savingsGoal.LockType == LockType.TimeBased)
                savingsGoal.LastDepositDate = DateTime.Now;

            var transaction = new Transaction
            {
                Amount = amount,
                TransactionDate = DateTime.Now,
                Type = TransactionType.TransferToGoal,
                BankAccountId = bankAccount.BankAccountId,
                SavingsGoalId = savingsGoal.SavingsGoalId
            };

            _context.Transactions.Add(transaction);

            // If completed
            bool isCompleted = (savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate <= DateTime.Now) ||
                               (savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount);

            if (isCompleted && savingsGoal.Status == SavingsGoalStatus.Unlocked && savingsGoal.CurrentAmount > 0)
            {
                var amountToTransfer = savingsGoal.CurrentAmount;
                bankAccount.Balance += amountToTransfer;
                savingsGoal.CurrentAmount = 0;
                savingsGoal.Status = SavingsGoalStatus.Completed;

                var transferBackTransaction = new Transaction
                {
                    Amount = amountToTransfer,
                    TransactionDate = DateTime.Now,
                    Type = TransactionType.TransferFromGoal,
                    BankAccountId = bankAccount.BankAccountId,
                    SavingsGoalId = savingsGoal.SavingsGoalId
                };
                _context.Transactions.Add(transferBackTransaction);
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

            if (savingsGoal.Status == SavingsGoalStatus.Completed)
                return BadRequest("Cannot withdraw from a completed savings goal");

            if (savingsGoal.CurrentAmount < amount) return BadRequest("Insufficient funds in savings goal");

            var bankAccount = savingsGoal.BankAccount;
            bankAccount.Balance += amount;
            savingsGoal.CurrentAmount -= amount;

            var transaction = new Transaction
            {
                Amount = amount,
                TransactionDate = DateTime.Now,
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

            // Check if the savings goal is completed after unlocking
            bool isCompleted = (savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate <= DateTime.Now) ||
                               (savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount);

            if (isCompleted && savingsGoal.CurrentAmount > 0)
            {
                var bankAccount = savingsGoal.BankAccount;
                var amountToTransfer = savingsGoal.CurrentAmount;

                bankAccount.Balance += amountToTransfer;
                savingsGoal.CurrentAmount = 0;
                savingsGoal.Status = SavingsGoalStatus.Completed;

                var transaction = new Transaction
                {
                    Amount = amountToTransfer,
                    TransactionDate = DateTime.Now,
                    Type = TransactionType.TransferFromGoal,
                    BankAccountId = bankAccount.BankAccountId,
                    SavingsGoalId = savingsGoal.SavingsGoalId
                };

                _context.Transactions.Add(transaction);
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Savings goal unlocked successfully" });
        }

        private bool SavingsGoalExists(int id)
        {
            return _context.SavingsGoals.Any(e => e.SavingsGoalId == id);
        }
    }
}