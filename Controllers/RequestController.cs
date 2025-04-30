using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using growmesh_API.Data;
using growmesh_API.Models;
using growmesh_API.DTOs.RequestDTOs;
using growmesh_API.DTOs.ResponseDTOs;
using System.Security.Claims;


namespace growmesh_API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class RequestController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RequestController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/Request/create
        [HttpPost("create")]
        public async Task<IActionResult> CreateRequest([FromBody] RequestDTO requestDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { Errors = errors });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            var savingsGoal = bankAccount.SavingsGoals.FirstOrDefault(sg => sg.SavingsGoalId == requestDto.SavingsGoalId);
            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (savingsGoal.Status == SavingsGoalStatus.Completed && (requestDto.Type == RequestType.Unlock || requestDto.Type == RequestType.PartialWithdrawal))
            {
                return BadRequest("Cannot process Unlock or Partial Withdrawal requests for a completed savings goal");
            }

            var request = new Request
            {
                Type = requestDto.Type,
                WithdrawalAmount = requestDto.WithdrawalAmount,
                Reason = requestDto.Reason,
                RequestDate = DateTime.UtcNow,
                SavingsGoalId = requestDto.SavingsGoalId
            };

            // Process the request immediately based on its type
            switch (request.Type)
            {
                case RequestType.Unlock:
                    savingsGoal.Status = SavingsGoalStatus.Unlocked;

                    // Check if the savings goal should be marked as done
                    bool shouldMarkDone = (savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate <= DateTime.UtcNow) ||
                          (savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount);

                    if (shouldMarkDone)
                    {
                        savingsGoal.Status = SavingsGoalStatus.MarkDone;
                    }
                    break;

                case RequestType.PartialWithdrawal:
                    if (request.WithdrawalAmount == null)
                        return BadRequest("Withdrawal amount is required for partial withdrawal requests");

                    if (savingsGoal.CurrentAmount < request.WithdrawalAmount)
                        return BadRequest("Insufficient funds in savings goal");

                    // Removed lock checks to allow partial withdrawals regardless of lock status or completion
                    savingsGoal.CurrentAmount -= request.WithdrawalAmount.Value;
                    bankAccount.Balance += request.WithdrawalAmount.Value;

                    var withdrawalTransaction = new Transaction
                    {
                        Amount = request.WithdrawalAmount.Value,
                        TransactionDate = DateTime.UtcNow,
                        Type = TransactionType.TransferFromGoal,
                        BankAccountId = bankAccount.BankAccountId,
                        SavingsGoalId = savingsGoal.SavingsGoalId
                    };
                    _context.Transactions.Add(withdrawalTransaction);
                    break;

                case RequestType.DeleteGoal:
                    // Mark the goal as deleted instead of removing it
                    savingsGoal.DeletedAt = DateTime.UtcNow;
                    savingsGoal.Status = SavingsGoalStatus.Completed; // Mark as completed to stop further deposits

                    // Transfer remaining balance back to bank account
                    if (savingsGoal.CurrentAmount > 0)
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
                    break;

                default:
                    return BadRequest("Invalid request type");
            }

            _context.Requests.Add(request);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Request processed successfully" });
        }

        // GET: api/Request/get-all
        [HttpGet("get-all")]
        public async Task<ActionResult<IEnumerable<RequestResponseDTO>>> GetAllRequests()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .ThenInclude(sg => sg.Requests)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            var requests = bankAccount.SavingsGoals
                .Where(sg => sg.DeletedAt == null)
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

        // GET: api/Request/get/{id}
        [HttpGet("get/{id}")]
        public async Task<ActionResult<RequestResponseDTO>> GetRequest(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var request = await _context.Requests
                .Include(r => r.SavingsGoal)
                .ThenInclude(sg => sg.BankAccount)
                .FirstOrDefaultAsync(r => r.RequestId == id && r.SavingsGoal.BankAccount.UserId == userId);

            if (request == null) return NotFound("Request not found");

            return new RequestResponseDTO
            {
                RequestId = request.RequestId,
                Type = request.Type,
                WithdrawalAmount = request.WithdrawalAmount,
                RequestDate = request.RequestDate,
                Reason = request.Reason,
                SavingsGoalId = request.SavingsGoalId
            };
        }

        // DELETE: api/Request/delete/{id}
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteRequest(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var request = await _context.Requests
                .Include(r => r.SavingsGoal)
                .ThenInclude(sg => sg.BankAccount)
                .FirstOrDefaultAsync(r => r.RequestId == id && r.SavingsGoal.BankAccount.UserId == userId);

            if (request == null) return NotFound("Request not found");

            _context.Requests.Remove(request);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Request deleted successfully" });
        }
    }
}