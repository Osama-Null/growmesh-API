using System.Security.Claims;
using growmesh_API.Data;
using growmesh_API.DTOs.RequestDTOs;
using growmesh_API.DTOs.ResponseDTOs;
using growmesh_API.Services;
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
        private readonly LlamaService _llamaService;

        public TransactionController(ApplicationDbContext context, LlamaService llamaService)
        {
            _context = context;
            _llamaService = llamaService;
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

        // Llama ============================================
        [HttpPost("transactions-agent")]
        public async Task<IActionResult> TransactionsAgent([FromBody] AgentRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var transactions = await _context.Transactions
                .Where(t => t.BankAccount.UserId == userId)
                .Select(t => new { t.TransactionId, t.Amount, t.TransactionDate, t.Type, t.SavingsGoalId })
                .ToListAsync();

            try
            {
                var response = await _llamaService.SendTransactionsAgentMessageAsync(
                    request.Message,
                    transactions.Cast<object>().ToList()
                );
                return Ok(new { Response = response });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}