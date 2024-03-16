using Billing.Identity;
using Billing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Billing.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AccountsController> _logger;

        public AccountsController(AppDbContext context, ILogger<AccountsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [Authorize(Policy = IdentityData.AccountantUserPolicyName)]
        [HttpGet()]
        public async Task<ActionResult<List<AccountEntity>>> GetStatistics()
        {
            var account = await _context.Accounts.ToListAsync();

            if (account == null)
            {
                return NotFound();
            }

            return account;
        }

        [Authorize(Policy = IdentityData.PopugUserPolicyName)]
        [HttpGet("{id}")]
        public async Task<ActionResult<AccountResponse>> GetBalance(int id)
        {
            var account = await _context.Accounts.FirstOrDefaultAsync(f => f.UserId == id);

            if (account == null)
            {
                return NotFound();
            }

            return new AccountResponse() { UserId = account.UserId, Balance = account.Balance };
        }
    }
}
