using System.Security.Claims;
using growmesh_API.Data;
using growmesh_API.DTOs.ResponseDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace growmesh_API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransactionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Transaction/get-all
        [HttpGet("get-all")]
        public async Task<ActionResult<IEnumerable<TransactionDTO>>> GetAllTransactions()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            var transactions = await _context.Transactions
                .Where(t => t.BankAccountId == bankAccount.BankAccountId)
                .Select(t => new TransactionDTO
                {
                    TransactionId = t.TransactionId,
                    Amount = t.Amount,
                    TransactionDate = t.TransactionDate,
                    TransactionType = t.Type, // Updated to TransactionType
                    BankAccountId = t.BankAccountId,
                    SavingsGoalId = t.SavingsGoalId
                })
                .ToListAsync();

            return transactions;
        }

        // GET: api/Transaction/get-by-savings-goal/{savingsGoalId}
        [HttpGet("get-by-savings-goal/{savingsGoalId}")]
        public async Task<ActionResult<IEnumerable<TransactionDTO>>> GetTransactionsBySavingsGoal(int savingsGoalId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            var savingsGoal = bankAccount.SavingsGoals.FirstOrDefault(sg => sg.SavingsGoalId == savingsGoalId);
            if (savingsGoal == null) return NotFound("Savings goal not found");

            var transactions = await _context.Transactions
                .Where(t => t.BankAccountId == bankAccount.BankAccountId && t.SavingsGoalId == savingsGoalId)
                .Select(t => new TransactionDTO
                {
                    TransactionId = t.TransactionId,
                    Amount = t.Amount,
                    TransactionDate = t.TransactionDate,
                    TransactionType = t.Type,
                    BankAccountId = t.BankAccountId,
                    SavingsGoalId = t.SavingsGoalId
                })
                .ToListAsync();

            return transactions;
        }
    }
}