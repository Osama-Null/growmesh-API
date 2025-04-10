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

            var savingsGoal = new SavingsGoal
            {
                SavingsGoalName = model.SavingsGoalName,
                TargetAmount = model.TargetAmount,
                CurrentAmount = 0,
                TargetDate = model.TargetDate,
                Description = model.Description,
                LockType = model.LockType,
                Status = SavingsGoalStatus.InProgress,
                BankAccountId = bankAccount.BankAccountId
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
                BankAccountId = savingsGoal.BankAccountId
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
                TargetDate = savingsGoal.TargetDate,
                Description = savingsGoal.Description,
                LockType = savingsGoal.LockType,
                Status = savingsGoal.Status,
                BankAccountId = savingsGoal.BankAccountId
            };
        }

        // PUT: api/SavingsGoal/update/{id}
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateSavingsGoal(int id, [FromBody] UpdateSavingsGoalDTO model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (model.SavingsGoalName != null) savingsGoal.SavingsGoalName = model.SavingsGoalName;
            if (model.TargetAmount.HasValue) savingsGoal.TargetAmount = model.TargetAmount.Value;
            if (model.TargetDate.HasValue) savingsGoal.TargetDate = model.TargetDate.Value;
            if (model.Description != null) savingsGoal.Description = model.Description;
            if (model.LockType.HasValue) savingsGoal.LockType = model.LockType.Value;

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

            return Ok(new { success = true, message = "Savings goal updated successfully" });
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
        public async Task<IActionResult> Deposit(int id, [FromBody] decimal amount)
        {
            if (amount <= 0) return BadRequest("Amount must be greater than zero");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            var bankAccount = savingsGoal.BankAccount;
            if (bankAccount.Balance < amount) return BadRequest("Insufficient funds in bank account");

            bankAccount.Balance -= amount;
            savingsGoal.CurrentAmount += amount;

            var transaction = new Transaction
            {
                Amount = amount,
                TransactionDate = DateTime.Now,
                Type = TransactionType.TransferToGoal,
                BankAccountId = bankAccount.BankAccountId,
                SavingsGoalId = savingsGoal.SavingsGoalId
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Deposit successful" });
        }

        // POST: api/SavingsGoal/withdraw/{id}
        [HttpPost("withdraw/{id}")]
        public async Task<IActionResult> Withdraw(int id, [FromBody] decimal amount)
        {
            if (amount <= 0) return BadRequest("Amount must be greater than zero");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (savingsGoal.CurrentAmount < amount) return BadRequest("Insufficient funds in savings goal");

            if (savingsGoal.Status != SavingsGoalStatus.Unlocked && savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate > DateTime.Now)
                return BadRequest("Savings goal is locked until the target date");

            if (savingsGoal.Status != SavingsGoalStatus.Unlocked && savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount < savingsGoal.TargetAmount)
                return BadRequest("Savings goal is locked until the target amount is reached");

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

            savingsGoal.Status = SavingsGoalStatus.Unlocked;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Savings goal unlocked successfully" });
        }

        private bool SavingsGoalExists(int id)
        {
            return _context.SavingsGoals.Any(e => e.SavingsGoalId == id);
        }
    }
}