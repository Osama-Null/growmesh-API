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
    public class BankAccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        public BankAccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/BankAccount/get
        [HttpGet("get")]
        public async Task<ActionResult<BankAccountDTO>> GetBankAccount()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            return new BankAccountDTO
            {
                AccountId = bankAccount.BankAccountId,
                Balance = bankAccount.Balance,
                SavingsGoals = bankAccount.SavingsGoals.Select(sg => new SavingsGoalDTO
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
                }).ToList(),
                UserId = bankAccount.UserId
            };
        }

        // GET: api/BankAccount/get-requests
        [HttpGet("get-requests")]
        public async Task<ActionResult<IEnumerable<RequestResponseDTO>>> GetRequests()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .ThenInclude(sg => sg.Requests) // Now works with the new navigation property
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            var requests = bankAccount.SavingsGoals
                .SelectMany(sg => sg.Requests)
                .Select(r => new RequestResponseDTO
                {
                    RequestId = r.RequestId,
                    Type = r.Type,
                    WithdrawalAmount = r.WithdrawalAmount,
                    RequestDate = r.RequestDate,
                    Reason = r.Reason,
                    SavingsGoalId = r.SavingsGoalId
                })
                .ToList();

            return requests;
        }

        // PUT: api/BankAccount/update
        [HttpPut("update")]
        public async Task<IActionResult> UpdateBankAccount([FromBody] UpdateBankAccountDTO bankAccountDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            bankAccount.Balance = bankAccountDto.Balance;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BankAccountExists(bankAccount.BankAccountId))
                {
                    return NotFound();
                }
                throw;
            }

            return Ok(new { success = true, message = "Bank account updated successfully" });
        }

        // POST: api/BankAccount/transfer-to-goal
        [HttpPost("transfer-to-goal")]
        public async Task<IActionResult> TransferToGoal([FromQuery] int savingsGoalId, [FromBody] decimal amount)
        {
            if (amount <= 0) return BadRequest("Amount must be greater than zero");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            var savingsGoal = bankAccount.SavingsGoals.FirstOrDefault(sg => sg.SavingsGoalId == savingsGoalId);
            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (bankAccount.Balance < amount) return BadRequest("Insufficient funds in bank account");

            bankAccount.Balance -= amount;
            savingsGoal.CurrentAmount += amount;

            var transaction = new Transaction
            {
                Amount = amount,
                TransactionDate = DateTime.Now,
                Type = TransactionType.TransferToGoal, // Updated to TransactionType
                BankAccountId = bankAccount.BankAccountId,
                SavingsGoalId = savingsGoal.SavingsGoalId
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Transfer to savings goal successful" });
        }

        // POST: api/BankAccount/pull-from-goal
        [HttpPost("pull-from-goal")]
        public async Task<IActionResult> PullFromGoal([FromQuery] int savingsGoalId, [FromBody] decimal amount)
        {
            if (amount <= 0) return BadRequest("Amount must be greater than zero");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            var savingsGoal = bankAccount.SavingsGoals.FirstOrDefault(sg => sg.SavingsGoalId == savingsGoalId);
            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (savingsGoal.CurrentAmount < amount) return BadRequest("Insufficient funds in savings goal");

            if (savingsGoal.Status != SavingsGoalStatus.Unlocked && savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate > DateTime.Now)
            {
                return BadRequest("Savings goal is locked until the target date");
            }

            if (savingsGoal.Status != SavingsGoalStatus.Unlocked && savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount < savingsGoal.TargetAmount)
            {
                return BadRequest("Savings goal is locked until the target amount is reached");
            }

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

            return Ok(new { success = true, message = "Funds pulled from savings goal successfully" });
        }

        private bool BankAccountExists(int id)
        {
            return _context.BankAccounts.Any(e => e.BankAccountId == id);
        }


    }
}