using System.Globalization;
using System.Security.Claims;
using growmesh_API.Data;
using growmesh_API.DTOs.RequestDTOs;
using growmesh_API.DTOs.ResponseDTOs;
using growmesh_API.Models;
using growmesh_API.Services;
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
        private readonly LlamaService _llamaService;

        public SavingsGoalController(ApplicationDbContext context, LlamaService llamaService)
        {
            _context = context;
            _llamaService = llamaService;
        }

        // POST: api/SavingsGoal/create
        [HttpPost("create")]
        public async Task<ActionResult<SavingsGoalDTO>> CreateSavingsGoal([FromBody] CreateSavingsGoalDTO model)
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
                .FirstOrDefaultAsync(ba => ba.UserId == userId);
            if (bankAccount == null) return NotFound("Bank account not found");

            if (model.LockType == LockType.TimeBased && !model.TargetDate.HasValue)
                return BadRequest("Target date is required for TimeBased goals");

            if (model.LockType == LockType.TimeBased && model.TargetAmount.HasValue)
                return BadRequest("Target amount should not be provided for TimeBased goals");

            if (model.LockType == LockType.AmountBased && !model.TargetAmount.HasValue)
                return BadRequest("Target amount is required for AmountBased goals");

            if (model.LockType == LockType.AmountBased && model.TargetDate.HasValue)
                return BadRequest("Target date should not be provided for AmountBased goals");

            if (model.LockType == LockType.AmountBased && (!model.TargetAmount.HasValue || model.TargetAmount.Value <= 0))
                return BadRequest("Target amount must be greater than zero for AmountBased goals");

            if (model.DepositFrequency.HasValue)
            {
                if (!model.DepositAmount.HasValue || model.DepositAmount <= 0)
                    return BadRequest("Deposit amount must be greater than zero when a deposit frequency is specified");
            }

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

                if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.TargetAmount.HasValue && amount > savingsGoal.TargetAmount.Value)
                    amount = savingsGoal.TargetAmount.Value;

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
                    amount = savingsGoal.TargetAmount.Value;

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

        // POST: api/SavingsGoal/create-amount-based
        [HttpPost("create-amount-based")]
        public async Task<ActionResult<SavingsGoalDTO>> CreateAmountBasedSavingsGoal([FromBody] CreateAmountBasedSavingsGoalDTO model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Errors = errors });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var (bankAccount, errorResult) = await GetUserBankAccountAsync(userId);
            if (errorResult != null) return errorResult;

            // Deposit-related validations
            if (model.DepositFrequency.HasValue)
            {
                if (!model.DepositAmount.HasValue || model.DepositAmount <= 0)
                    return BadRequest("Deposit amount must be greater than zero when a deposit frequency is specified");

                if (model.DepositFrequency == DepositFrequency.Custom &&
                    (!model.CustomDepositIntervalDays.HasValue || model.CustomDepositIntervalDays <= 0))
                    return BadRequest("Custom deposit interval must be specified and greater than zero for Custom frequency");
            }
            else if (model.DepositAmount.HasValue)
            {
                return BadRequest("Deposit frequency is required when a deposit amount is specified");
            }

            var savingsGoal = new SavingsGoal
            {
                SavingsGoalName = model.SavingsGoalName,
                TargetAmount = model.TargetAmount,
                TargetDate = null, // Enforce null for AmountBased
                CurrentAmount = 0,
                Description = model.Description,
                LockType = LockType.AmountBased,
                Status = SavingsGoalStatus.InProgress,
                BankAccountId = bankAccount.BankAccountId,
                DepositAmount = model.DepositAmount,
                DepositFrequency = model.DepositFrequency,
                CustomDepositIntervalDays = model.DepositFrequency == DepositFrequency.Custom ? model.CustomDepositIntervalDays : null,
                LastDepositDate = (model.DepositAmount.HasValue && model.DepositFrequency.HasValue) ? DateTime.UtcNow : null,
                Emoji = model.Emoji,
                Color = model.Color,
                CreatedAt = DateTime.UtcNow,
                InitialManualPayment = model.InitialManualPayment,
                InitialAutomaticPayment = model.InitialAutomaticPayment
            };

            _context.SavingsGoals.Add(savingsGoal);
            await _context.SaveChangesAsync();

            // Handle initial payments
            var paymentResult = await HandleInitialPaymentsAsync(savingsGoal, bankAccount,
                model.InitialManualPayment, model.InitialAutomaticPayment, model.InitialManualPaymentAmount, model.DepositAmount);
            if (paymentResult != null) return paymentResult;

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
                Color = savingsGoal.Color,
                CreatedAt = savingsGoal.CreatedAt,
                CompletedAt = savingsGoal.CompletedAt,
                InitialManualPayment = savingsGoal.InitialManualPayment,
                InitialAutomaticPayment = savingsGoal.InitialAutomaticPayment
            });
        }

        // POST: api/SavingsGoal/create-time-based
        [HttpPost("create-time-based")]
        public async Task<ActionResult<SavingsGoalDTO>> CreateTimeBasedSavingsGoal([FromBody] CreateTimeBasedSavingsGoalDTO model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Errors = errors });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var (bankAccount, errorResult) = await GetUserBankAccountAsync(userId);
            if (errorResult != null) return errorResult;

            // Additional validation for TargetDate
            if (model.TargetDate <= DateTime.UtcNow)
                return BadRequest("Target date must be in the future");

            // Deposit-related validations
            if (model.DepositFrequency.HasValue)
            {
                if (!model.DepositAmount.HasValue || model.DepositAmount <= 0)
                    return BadRequest("Deposit amount must be greater than zero when a deposit frequency is specified");

                if (model.DepositFrequency == DepositFrequency.Custom &&
                    (!model.CustomDepositIntervalDays.HasValue || model.CustomDepositIntervalDays <= 0))
                    return BadRequest("Custom deposit interval must be specified and greater than zero for Custom frequency");
            }
            else if (model.DepositAmount.HasValue)
            {
                return BadRequest("Deposit frequency is required when a deposit amount is specified");
            }

            var savingsGoal = new SavingsGoal
            {
                SavingsGoalName = model.SavingsGoalName,
                TargetAmount = null, // Enforce null for TimeBased
                TargetDate = model.TargetDate,
                CurrentAmount = 0,
                Description = model.Description,
                LockType = LockType.TimeBased,
                Status = SavingsGoalStatus.InProgress,
                BankAccountId = bankAccount.BankAccountId,
                DepositAmount = model.DepositAmount,
                DepositFrequency = model.DepositFrequency,
                CustomDepositIntervalDays = model.DepositFrequency == DepositFrequency.Custom ? model.CustomDepositIntervalDays : null,
                LastDepositDate = (model.DepositAmount.HasValue && model.DepositFrequency.HasValue) ? DateTime.UtcNow : null,
                Emoji = model.Emoji,
                Color = model.Color,
                CreatedAt = DateTime.UtcNow,
                InitialManualPayment = model.InitialManualPayment,
                InitialAutomaticPayment = model.InitialAutomaticPayment
            };

            _context.SavingsGoals.Add(savingsGoal);
            await _context.SaveChangesAsync();

            // Handle initial payments
            var paymentResult = await HandleInitialPaymentsAsync(savingsGoal, bankAccount,
                model.InitialManualPayment, model.InitialAutomaticPayment, model.InitialManualPaymentAmount, model.DepositAmount);
            if (paymentResult != null) return paymentResult;

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
                Color = savingsGoal.Color,
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
                    Color = sg.Color,
                    CreatedAt = sg.CreatedAt,
                    CompletedAt = sg.CompletedAt,
                    InitialManualPayment = sg.InitialManualPayment,
                    InitialAutomaticPayment = sg.InitialAutomaticPayment,
                    DeletedAt = sg.DeletedAt,
                    DepositAmount = sg.DepositAmount,
                    DepositFrequency = sg.DepositFrequency,
                    CustomDepositIntervalDays = sg.CustomDepositIntervalDays,
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
                DepositAmount = savingsGoal.DepositAmount,
                DepositFrequency = savingsGoal.DepositFrequency,
                CustomDepositIntervalDays = savingsGoal.CustomDepositIntervalDays,
                Emoji = savingsGoal.Emoji,
                Color = savingsGoal.Color,
                CreatedAt = savingsGoal.CreatedAt,
                CompletedAt = savingsGoal.CompletedAt,
                InitialManualPayment = savingsGoal.InitialManualPayment,
                InitialAutomaticPayment = savingsGoal.InitialAutomaticPayment,
                DeletedAt = savingsGoal.DeletedAt
            };
        }

        // PUT: api/SavingsGoal/update/{id}
        [HttpPut("update/{id}")]
        public async Task<ActionResult<SavingsGoalDTO>> UpdateSavingsGoal(int id, [FromBody] UpdateSavingsGoalDTO model)
        {
            // Validate model state and return detailed error messages
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

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            if (savingsGoal.Status != SavingsGoalStatus.InProgress)
                return BadRequest("Cannot update a goal that is not in progress");

            if (model.LockType.HasValue)
                return BadRequest("Changing the lock type is not allowed.");

            // Emoji
            if (model.Emoji != null)
            {
                if (string.IsNullOrWhiteSpace(model.Emoji))
                    return BadRequest("Emoji cannot be empty or whitespace.");

                // Count grapheme clusters (visual characters)
                var stringInfo = new StringInfo(model.Emoji);
                int graphemeCount = stringInfo.LengthInTextElements;

                if (graphemeCount != 1)
                    return BadRequest("Emoji must be exactly one emoji character.");

                savingsGoal.Emoji = model.Emoji;
            }

            // Changing Goal Name only
            if (model.SavingsGoalName != null)
            {
                if (string.IsNullOrEmpty(model.SavingsGoalName))
                    return BadRequest("Savings goal name cannot be empty");

                if(model.SavingsGoalName.Length > 100)
                    return BadRequest("Savings goal name cannot exceed 100 characters");

                savingsGoal.SavingsGoalName = model.SavingsGoalName;
            }

            // Changing Target Amount only
            if (model.TargetAmount.HasValue)
            {
                if (savingsGoal.LockType != LockType.AmountBased)
                    return BadRequest("Target amount can only be set for AmountBased goals");

                if (model.TargetAmount.Value <= 0)
                    return BadRequest("Target amount must be greater than zero");

                if (model.TargetAmount.Value < savingsGoal.CurrentAmount)
                    return BadRequest("Target amount cannot be less than the current amount");

                savingsGoal.TargetAmount = model.TargetAmount.Value;
            }

            // Changing Target Date only
            if (model.TargetDate.HasValue)
            {
                if (savingsGoal.LockType != LockType.TimeBased)
                    return BadRequest("Target date can only be set for TimeBased goals");

                if (model.TargetDate.Value < DateTime.UtcNow)
                    return BadRequest("Target date must be in the future");

                savingsGoal.TargetDate = model.TargetDate.Value;
            }

            // Wants to change only the Frequency
            if (model.DepositFrequency.HasValue)
            {
                if (model.DepositFrequency == DepositFrequencyDTO.Disabled)
                {
                    savingsGoal.DepositAmount = null;
                    savingsGoal.DepositFrequency = null;
                    savingsGoal.CustomDepositIntervalDays = null;
                    savingsGoal.LastDepositDate = null;
                }
                else
                {
                    // Consolidated validations
                    if (!model.DepositAmount.HasValue && !savingsGoal.DepositAmount.HasValue)
                        return BadRequest("Deposit amount is required when a frequency is specified");
                    if (model.DepositAmount.HasValue && model.DepositAmount <= 0)
                        return BadRequest("Deposit amount must be greater than zero");

                    if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.TargetAmount.HasValue &&
                        model.DepositAmount.HasValue && model.DepositAmount > savingsGoal.TargetAmount)
                        return BadRequest("Deposit amount cannot exceed target amount");

                    if (model.DepositFrequency == DepositFrequencyDTO.Custom && (!model.CustomDepositIntervalDays.HasValue || model.CustomDepositIntervalDays <= 0) && (!savingsGoal.CustomDepositIntervalDays.HasValue || savingsGoal.CustomDepositIntervalDays <= 0))
                        return BadRequest("Custom deposit interval must be greater than zero for Custom frequency");

                    // Apply frequency-specific updates
                    switch (model.DepositFrequency)
                    {
                        case DepositFrequencyDTO.Monthly:
                            savingsGoal.DepositFrequency = DepositFrequency.Monthly;
                            savingsGoal.CustomDepositIntervalDays = null;
                            break;
                        case DepositFrequencyDTO.Weekly:
                            savingsGoal.DepositFrequency = DepositFrequency.Weekly;
                            savingsGoal.CustomDepositIntervalDays = null;
                            break;
                        case DepositFrequencyDTO.Custom:
                            savingsGoal.DepositFrequency = DepositFrequency.Custom;
                            savingsGoal.CustomDepositIntervalDays = model.CustomDepositIntervalDays.HasValue ? model.CustomDepositIntervalDays.Value : savingsGoal.CustomDepositIntervalDays;
                            break;
                    }

                    if (model.DepositAmount.HasValue)
                    {
                        savingsGoal.DepositAmount = model.DepositAmount.Value;
                        savingsGoal.LastDepositDate = DateTime.UtcNow;
                    }
                }
            }

            // Changing Custom Deposit Interval Days only
            if (model.CustomDepositIntervalDays.HasValue)
            {
                if (model.CustomDepositIntervalDays.Value <= 0)
                    return BadRequest("Custom deposit interval must be greater than zero for Custom frequency");

                // Allow update if either the existing frequency is Custom OR the request sets it to Custom
                bool isExistingCustom = savingsGoal.DepositFrequency.HasValue && savingsGoal.DepositFrequency == DepositFrequency.Custom;
                bool isRequestCustom = model.DepositFrequency.HasValue && model.DepositFrequency == DepositFrequencyDTO.Custom;

                if (!isExistingCustom && !isRequestCustom)
                    return BadRequest("Set frequency to 'Custom' to specify custom days");

                savingsGoal.CustomDepositIntervalDays = model.CustomDepositIntervalDays.Value;
            }

            // Changing Amount only
            if(model.DepositAmount.HasValue)
            {
                if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.TargetAmount.HasValue && model.DepositAmount.Value > savingsGoal.TargetAmount.Value)
                    return BadRequest("Deposit amount cannot exceed target amount");

                if (model.DepositAmount <= 0)
                    return BadRequest("Deposit amount must be greater than zero");

                if(savingsGoal.LockType == LockType.AmountBased)
                {
                    if (!savingsGoal.TargetAmount.HasValue || !model.TargetAmount.HasValue)
                        return BadRequest("Target amount must be specified for AmountBased goals.");

                    if (model.DepositAmount.HasValue && model.DepositAmount.Value > savingsGoal.TargetAmount.Value)
                        return BadRequest("Deposit amount cannot exceed target amount");
                }

                if (!model.DepositFrequency.HasValue && !savingsGoal.DepositFrequency.HasValue)
                    return BadRequest("Deposit frequency must be specified");

                // Ensure CustomDepositIntervalDays is provided if frequency is Custom
                if ((model.DepositFrequency.HasValue && model.DepositFrequency == DepositFrequencyDTO.Custom) ||
                    (savingsGoal.DepositFrequency.HasValue && savingsGoal.DepositFrequency == DepositFrequency.Custom))
                {
                    if (!model.CustomDepositIntervalDays.HasValue && !savingsGoal.CustomDepositIntervalDays.HasValue)
                        return BadRequest("Custom deposit interval days must be specified for Custom frequency");
                }

                savingsGoal.DepositAmount = model.DepositAmount.Value;
                savingsGoal.LastDepositDate = DateTime.UtcNow;
            }

            // Changing Description only
            if (model.Description != null)
            {
                if (model.Description.Length > 500)
                    return BadRequest("Description cannot exceed 500 characters.");
                savingsGoal.Description = model.Description;
            }

            // Changing Color only
            if (model.Color != null)
            {
                if (string.IsNullOrWhiteSpace(model.Color))
                    return BadRequest("Color cannot be empty or whitespace.");
                savingsGoal.Color = model.Color;
            }

            // Save changes and reload entity
            try
            {
                Console.WriteLine("Saving changes...");
                await _context.SaveChangesAsync();
                Console.WriteLine("Reloading entity...");
                await _context.Entry(savingsGoal).ReloadAsync();
                Console.WriteLine($"Reloaded DepositAmount: {savingsGoal.DepositAmount}");
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
                CustomDepositIntervalDays = savingsGoal.CustomDepositIntervalDays,
                Emoji = savingsGoal.Emoji,
                Color = savingsGoal.Color,
                CreatedAt = savingsGoal.CreatedAt,
                CompletedAt = savingsGoal.CompletedAt,
                InitialManualPayment = savingsGoal.InitialManualPayment,
                InitialAutomaticPayment = savingsGoal.InitialAutomaticPayment,
                DeletedAt = savingsGoal.DeletedAt
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
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { Errors = errors });
            }

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
                                  (savingsGoal.LockType == LockType.AmountBased && savingsGoal.TargetAmount.HasValue && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount.Value);

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
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { Errors = errors });
            }

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
                                  (savingsGoal.LockType == LockType.AmountBased && savingsGoal.TargetAmount.HasValue && savingsGoal.CurrentAmount >= savingsGoal.TargetAmount.Value);

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
                        // For the latest period (i=0), use the current time; for historical periods, use midnight
                        periodEnd = i == 0 ? now : now.Date.AddDays(-i).AddHours(23).AddMinutes(59).AddSeconds(59).AddMilliseconds(999);
                        break;
                    case "month":
                        periodEnd = i == 0 ? now : now.Date.AddMonths(-i).AddDays(-now.Day + 1).AddMonths(1).AddDays(-1);
                        break;
                    case "year":
                        periodEnd = i == 0 ? now : now.Date.AddYears(-i).AddDays(-now.DayOfYear + 1).AddYears(1).AddDays(-1);
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
                    decimal amountAtPeriodEnd;

                    if (i == 0)
                    {
                        // For the latest period, use CurrentAmount directly
                        amountAtPeriodEnd = goal.CurrentAmount;
                    }
                    else
                    {
                        // For historical periods, calculate based on transactions
                        var transactionsBeforePeriod = goal.Transactions
                            .Where(t => t.TransactionDate <= periodEnd &&
                                        (t.Type == TransactionType.TransferToGoal || t.Type == TransactionType.TransferFromGoal))
                            .ToList();

                        amountAtPeriodEnd = transactionsBeforePeriod
                            .Sum(t => t.Type == TransactionType.TransferToGoal ? t.Amount : -t.Amount);
                    }

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

        // GET: api/SavingsGoal/get-trend/{id}
        [HttpGet("get-trend/{id}")]
        public async Task<ActionResult<SavingsGoalTrendResponseDTO>> GetSavingsGoalTrend(int id, [FromQuery] int periods = 7)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.Transactions)
                .Include(sg => sg.BankAccount)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);

            if (savingsGoal == null) return NotFound("Savings goal not found");

            // Determine period type based on goal type
            string periodType = DeterminePeriodType(savingsGoal);

            var trendData = new List<SavingsGoalTrendDTO>();
            var now = DateTime.UtcNow;

            // Calculate the 7 periods
            for (int i = periods - 1; i >= 0; i--)
            {
                DateTime periodEnd = CalculatePeriodEnd(periodType, now, i, savingsGoal);

                decimal cumulativeSavings = 0;

                // Calculate cumulative savings up to period end
                var transactionsBeforePeriod = savingsGoal.Transactions
                    .Where(t => t.TransactionDate <= periodEnd &&
                                (t.Type == TransactionType.TransferToGoal || t.Type == TransactionType.TransferFromGoal))
                    .ToList();

                cumulativeSavings = transactionsBeforePeriod
                    .Sum(t => t.Type == TransactionType.TransferToGoal ? t.Amount : -t.Amount);

                // For the latest period, use CurrentAmount if it's higher
                if (i == 0 && savingsGoal.CurrentAmount > cumulativeSavings)
                {
                    cumulativeSavings = savingsGoal.CurrentAmount;
                }

                // Calculate target cumulative savings for TimeBased goals
                decimal? targetCumulativeSavings = null;
                if (savingsGoal.LockType == LockType.TimeBased && savingsGoal.TargetDate.HasValue)
                {
                    targetCumulativeSavings = CalculateTargetCumulativeSavings(savingsGoal, periodEnd);
                }

                trendData.Add(new SavingsGoalTrendDTO
                {
                    PeriodEnd = periodEnd,
                    CumulativeSavings = cumulativeSavings,
                    Difference = 0,  // Will calculate later
                    TargetCumulativeSavings = targetCumulativeSavings
                });
            }

            // Calculate differences between periods
            for (int i = 1; i < trendData.Count; i++)
            {
                trendData[i].Difference = trendData[i].CumulativeSavings - trendData[i - 1].CumulativeSavings;
            }
            if (trendData.Count > 0)
                trendData[0].Difference = trendData[0].CumulativeSavings;  // Initial difference

            return Ok(new SavingsGoalTrendResponseDTO
            {
                TrendData = trendData,
                PeriodType = periodType
            });
        }

        private string DeterminePeriodType(SavingsGoal goal)
        {
            if (goal.LockType == LockType.TimeBased && goal.TargetDate.HasValue)
            {
                var durationDays = (goal.TargetDate.Value - goal.CreatedAt).TotalDays;
                if (durationDays <= 30) return "day";
                if (durationDays <= 180) return "week";
                return "month";
            }
            else // AmountBased
            {
                if (goal.DepositFrequency.HasValue)
                {
                    switch (goal.DepositFrequency.Value)
                    {
                        case DepositFrequency.Weekly: return "week";
                        case DepositFrequency.Monthly: return "month";
                        case DepositFrequency.Custom:
                            if (goal.CustomDepositIntervalDays <= 7) return "day";
                            if (goal.CustomDepositIntervalDays <= 30) return "week";
                            return "month";
                    }
                }
                return "month"; // Default for AmountBased without frequency
            }
        }

        private DateTime CalculatePeriodEnd(string periodType, DateTime now, int i, SavingsGoal goal)
        {
            DateTime periodEnd;
            switch (periodType)
            {
                case "day":
                    periodEnd = now.Date.AddDays(-i);
                    break;
                case "week":
                    // End of the week (Sunday)
                    int daysUntilSunday = ((int)now.DayOfWeek - (int)DayOfWeek.Sunday + 7) % 7;
                    periodEnd = now.Date.AddDays(-daysUntilSunday - (i * 7));
                    break;
                case "month":
                    periodEnd = new DateTime(now.Year, now.Month, 1).AddMonths(-i).AddDays(-1);
                    break;
                default:
                    throw new InvalidOperationException("Invalid period type");
            }

            // Cap at CompletedAt for finished goals
            if (goal.CompletedAt.HasValue && periodEnd > goal.CompletedAt.Value)
            {
                periodEnd = goal.CompletedAt.Value;
            }

            return periodEnd;
        }

        private decimal? CalculateTargetCumulativeSavings(SavingsGoal goal, DateTime periodEnd)
        {
            if (goal.LockType != LockType.TimeBased || !goal.TargetDate.HasValue) return null;

            var totalDuration = (goal.TargetDate.Value - goal.CreatedAt).TotalDays;
            var elapsedDuration = (periodEnd - goal.CreatedAt).TotalDays;

            if (totalDuration <= 0) return 0;

            var progressRatio = (decimal)elapsedDuration / (decimal)totalDuration;
            return progressRatio * goal.TargetAmount;
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

        // Helper:
        private async Task<(BankAccount, ActionResult)> GetUserBankAccountAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return (null, Unauthorized());
            var bankAccount = await _context.BankAccounts.FirstOrDefaultAsync(ba => ba.UserId == userId);
            if (bankAccount == null) return (null, NotFound("Bank account not found"));
            return (bankAccount, null);
        }

        private async Task<ActionResult> HandleInitialPaymentsAsync(SavingsGoal savingsGoal, BankAccount bankAccount,
            bool initialManualPayment, bool initialAutomaticPayment, decimal? initialManualPaymentAmount, decimal? depositAmount)
        {
            if (initialManualPayment && initialAutomaticPayment)
                return BadRequest("Cannot select both initial manual and automatic payment");

            if (initialAutomaticPayment && !depositAmount.HasValue)
                return BadRequest("Deposit amount is required for initial automatic payment");

            if (initialManualPayment && !initialManualPaymentAmount.HasValue)
                return BadRequest("Initial manual payment amount is required if manual payment is selected");

            if (initialManualPayment && initialManualPaymentAmount.HasValue)
            {
                var amount = initialManualPaymentAmount.Value;
                if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.TargetAmount.HasValue && amount > savingsGoal.TargetAmount.Value)
                    amount = savingsGoal.TargetAmount.Value;

                if (bankAccount.Balance < amount)
                    return BadRequest("Insufficient funds for initial manual deposit");

                bankAccount.Balance -= amount;
                savingsGoal.CurrentAmount += amount;
                _context.Transactions.Add(new Transaction
                {
                    Amount = amount,
                    TransactionDate = DateTime.UtcNow,
                    Type = TransactionType.TransferToGoal,
                    BankAccountId = bankAccount.BankAccountId,
                    SavingsGoalId = savingsGoal.SavingsGoalId
                });
            }

            if (initialAutomaticPayment && depositAmount.HasValue)
            {
                var amount = depositAmount.Value;
                if (savingsGoal.LockType == LockType.AmountBased && savingsGoal.TargetAmount.HasValue && amount > savingsGoal.TargetAmount.Value)
                    amount = savingsGoal.TargetAmount.Value;

                if (bankAccount.Balance < amount)
                    return BadRequest("Insufficient funds for initial automatic deposit");

                bankAccount.Balance -= amount;
                savingsGoal.CurrentAmount += amount;
                _context.Transactions.Add(new Transaction
                {
                    Amount = amount,
                    TransactionDate = DateTime.UtcNow,
                    Type = TransactionType.TransferToGoal,
                    BankAccountId = bankAccount.BankAccountId,
                    SavingsGoalId = savingsGoal.SavingsGoalId
                });
            }

            await _context.SaveChangesAsync();
            return null;
        }

        // Llama ============================================
        [HttpPost("home-agent")]
        public async Task<IActionResult> HomeAgent([FromBody] AgentRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);
            if (bankAccount == null) return NotFound("Bank account not found");

            var allGoalsData = bankAccount.SavingsGoals
                .Where(sg => sg.DeletedAt == null)
                .Select(sg => new
                {
                    sg.SavingsGoalId,
                    sg.SavingsGoalName,
                    sg.TargetAmount,
                    sg.CurrentAmount,
                    sg.TargetDate,
                    sg.LockType,
                    sg.Status,
                    sg.DepositAmount,
                    sg.DepositFrequency
                })
                .ToList();

            var trendData = await GetSavingsTrendDataAsync(userId);

            try
            {
                var response = await _llamaService.SendHomeAgentMessageAsync(
                    request.Message,
                    allGoalsData.Cast<object>().ToList(),
                    trendData.Cast<object>().ToList()
                );
                return Ok(new { Response = response });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpPost("all-goals-agent")]
        public async Task<IActionResult> AllGoalsAgent([FromBody] AgentRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bankAccount = await _context.BankAccounts
                .Include(ba => ba.SavingsGoals)
                .FirstOrDefaultAsync(ba => ba.UserId == userId);
            if (bankAccount == null) return NotFound("Bank account not found");

            var allGoalsData = bankAccount.SavingsGoals
                .Where(sg => sg.DeletedAt == null)
                .Select(sg => new
                {
                    sg.SavingsGoalId,
                    sg.SavingsGoalName,
                    sg.TargetAmount,
                    sg.CurrentAmount,
                    sg.TargetDate,
                    sg.LockType,
                    sg.Status,
                    sg.DepositAmount,
                    sg.DepositFrequency
                })
                .ToList();

            try
            {
                var response = await _llamaService.SendAllGoalsAgentMessageAsync(
                    request.Message,
                    allGoalsData.Cast<object>().ToList()
                );
                return Ok(new { Response = response });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpPost("goal-details-agent/{id}")]
        public async Task<IActionResult> GoalDetailsAgent(int id, [FromBody] AgentRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var savingsGoal = await _context.SavingsGoals
                .Include(sg => sg.Transactions)
                .FirstOrDefaultAsync(sg => sg.SavingsGoalId == id && sg.BankAccount.UserId == userId);
            if (savingsGoal == null) return NotFound("Savings goal not found");

            var goalData = new
            {
                savingsGoal.SavingsGoalId,
                savingsGoal.SavingsGoalName,
                savingsGoal.TargetAmount,
                savingsGoal.CurrentAmount,
                savingsGoal.TargetDate,
                savingsGoal.LockType,
                savingsGoal.Status,
                savingsGoal.DepositAmount,
                savingsGoal.DepositFrequency
            };

            var trendData = await GetSavingsGoalTrendDataAsync(id);
            var transactions = savingsGoal.Transactions
                .Select(t => new { t.TransactionId, t.Amount, t.TransactionDate, t.Type })
                .ToList();

            try
            {
                var response = await _llamaService.SendGoalDetailsAgentMessageAsync(
                    request.Message,
                    goalData,
                    trendData.Cast<object>().ToList(),
                    transactions.Cast<object>().ToList()
                );
                return Ok(new { Response = response });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        private async Task<List<SavingsTrendDTO>> GetSavingsTrendDataAsync(string userId)
        {
            var trendData = await GetSavingsTrend("month", 7); // Default to monthly trend
            return trendData.Value as List<SavingsTrendDTO>;
        }

        private async Task<List<SavingsGoalTrendDTO>> GetSavingsGoalTrendDataAsync(int goalId)
        {
            var trendData = await GetSavingsGoalTrend(goalId);
            return trendData.Value.TrendData as List<SavingsGoalTrendDTO>;
        }
    }
}