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
        [HttpGet("get-info")]
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
                BankAccountId = bankAccount.BankAccountId,
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
                    BankAccountId = sg.BankAccountId,
                    DepositAmount = sg.DepositAmount,
                    DepositFrequency = sg.DepositFrequency,
                    CustomDepositIntervalDays = sg.CustomDepositIntervalDays,
                    Emoji = sg.Emoji,
                    CreatedAt = sg.CreatedAt,
                    CompletedAt = sg.CompletedAt,
                    InitialManualPayment = sg.InitialManualPayment,
                    InitialAutomaticPayment = sg.InitialAutomaticPayment,
                    DeletedAt = sg.DeletedAt
                }).ToList(),
                UserId = bankAccount.UserId
            };
        }

        // POST: api/BankAccount/deposit
        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositToBankAccountDTO depositDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { Errors = errors });
            }

            var amount = depositDto.Amount;
            if (amount <= 0) return BadRequest("Amount must be greater than zero");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts.FirstOrDefaultAsync(ba => ba.UserId == userId);
            if (bankAccount == null) return NotFound("Bank account not found");

            bankAccount.Balance += amount;

            var transaction = new Transaction
            {
                Amount = amount,
                TransactionDate = DateTime.UtcNow,
                Type = TransactionType.Deposit,
                BankAccountId = bankAccount.BankAccountId,
                SavingsGoalId = null
            };
            _context.Transactions.Add(transaction);

            await _context.SaveChangesAsync();

            var bankAccountDto = new BankAccountDTO
            {
                BankAccountId = bankAccount.BankAccountId,
                Balance = bankAccount.Balance
            };

            return Ok(new { success = true, message = "Deposit successful", bankAccount = bankAccountDto });
        }

        private bool BankAccountExists(int id)
        {
            return _context.BankAccounts.Any(e => e.BankAccountId == id);
        }
    }
}