using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Analytics.Identity;
using Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Analytics.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly ILogger<AnalyticsController> _logger;

        JsonSerializerOptions options = new JsonSerializerOptions();

        public AnalyticsController(ILogger<AnalyticsController> logger)
        {
            _logger = logger;
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));
        }

        [Authorize(Policy = IdentityData.AdminUserPolicyName)]
        [HttpGet("getstatistics")]
        public async Task<ActionResult<DoubleEntryBookkeepingDTO>> GetStatistics()
        {
            using (StreamReader sr = new StreamReader("task.txt"))
            {
                return JsonSerializer.Deserialize<DoubleEntryBookkeepingDTO>(sr.ReadLine()!, options)!;
            }
        }

        [Authorize(Policy = IdentityData.AdminUserPolicyName)]
        [HttpGet("gettotal")]
        public async Task<ActionResult<int>> GetTotal()
        {
            using (StreamReader sr = new StreamReader("total.txt"))
            {
                return int.Parse(sr.ReadLine()!);
            }
        }
    }
}
