﻿using Microsoft.AspNetCore.Authorization;
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
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);

            if (bankAccount == null) return NotFound("Bank account not found");

            var savingsGoal = bankAccount.SavingsGoals.FirstOrDefault(sg => sg.SavingsGoalId == requestDto.SavingsGoalId);
            if (savingsGoal == null) return NotFound("Savings goal not found");

            var request = new Request
            {
                Type = requestDto.Type,
                WithdrawalAmount = requestDto.WithdrawalAmount,
                Reason = requestDto.Reason,
                RequestDate = DateTime.Now,
                SavingsGoalId = requestDto.SavingsGoalId
            };

            // Process the request immediately based on its type
            switch (request.Type)
            {
                case RequestType.Unlock:
                    savingsGoal.Status = SavingsGoalStatus.Unlocked;
                    break;

                case RequestType.PartialWithdrawal:
                    if (request.WithdrawalAmount == null)
                        return BadRequest("Withdrawal amount is required for partial withdrawal requests");

                    if (savingsGoal.Status != SavingsGoalStatus.Unlocked && savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate > DateTime.Now)
                        return BadRequest("Savings goal is locked until the target date");

                    if (savingsGoal.Status != SavingsGoalStatus.Unlocked && savingsGoal.LockType == LockType.AmountBased && savingsGoal.CurrentAmount < savingsGoal.TargetAmount)
                        return BadRequest("Savings goal is locked until the target amount is reached");

                    if (savingsGoal.CurrentAmount < request.WithdrawalAmount)
                        return BadRequest("Insufficient funds in savings goal");

                    savingsGoal.CurrentAmount -= request.WithdrawalAmount.Value;
                    bankAccount.Balance += request.WithdrawalAmount.Value;

                    var withdrawalTransaction = new Transaction
                    {
                        Amount = request.WithdrawalAmount.Value,
                        TransactionDate = DateTime.Now,
                        Type = TransactionType.TransferFromGoal,
                        BankAccountId = bankAccount.BankAccountId,
                        SavingsGoalId = savingsGoal.SavingsGoalId
                    };
                    _context.Transactions.Add(withdrawalTransaction);
                    break;

                case RequestType.DeleteGoal:
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

                    _context.SavingsGoals.Remove(savingsGoal);
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