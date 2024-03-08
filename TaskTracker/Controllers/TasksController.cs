using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using TaskTracker.Identity;
using TaskTracker.Models;
using Contracts;
using RabbitMQ.Client;
using System.Text;

namespace TaskTracker.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TasksController> _logger;

        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _channel;

        public TasksController(AppDbContext context, ILogger<TasksController> logger)
        {
            _context = context;
            _logger = logger;

            _factory = new ConnectionFactory()
            {
                UserName = "rabbitmq",
                Password = "rabbitmq",
                HostName = "10.5.0.3"
            };

            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "task_to_assign", durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        private static TaskDTO TaskToDTO(TaskEnity task) =>
            new TaskDTO
            {
                UserId = task.UserId,
                CreatedBy = task.CreatedBy,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Estimation = task.Estimation,
                CreatedAt = task.CreatedAt
            };

        // GET: api/Tasks
        [Authorize(Policy = IdentityData.PopugUserPolicyName)]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskEnity>>> GetAllTasks()
        {
            return await _context.Tasks.ToListAsync();
        }

        // GET: api/Tasks/5
        [Authorize(Policy = IdentityData.PopugUserPolicyName)]
        [HttpGet("{id}")]
        public async Task<ActionResult<TaskEnity>> GetTask(int id)
        {
            var task = await _context.Tasks.FindAsync(id);

            if (task == null)
            {
                return NotFound();
            }

            return task;
        }

        // POST: api/Tasks
        [Authorize(Policy = IdentityData.PopugUserPolicyName)]
        [HttpPost]
        public async Task<ActionResult<TaskDTO>> AddTask(TaskDTO taskDTO)
        {
            var task = new TaskEnity
            {
                UserId = taskDTO.UserId,
                CreatedBy = taskDTO.CreatedBy,
                Title = taskDTO.Title,
                Description = taskDTO.Description,
                Status = taskDTO.Status,
                Estimation = taskDTO.Estimation,
                CreatedAt = taskDTO.CreatedAt
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
        }

        // POST: api/closetask/5
        [Authorize(Policy = IdentityData.PopugUserPolicyName)]
        [HttpPost("closetask/{id}")]
        public async Task<IActionResult> CloseTask(int id)
        {
            TaskEnity? task = await _context.Tasks.FindAsync(id);
            if (task != null)
            {
                task.Status = StatusEnum.Cancelled;
                _context.SaveChanges();
            }
            else
            {
                return BadRequest("Task not found");
            }
            return Ok();
        }

        // POST: api/assigntasks
        [Authorize(Policy = IdentityData.PopugUserPolicyName)]
        [HttpPost("assigntasks")]
        public async Task<ActionResult<TaskDTO>> AssignTasks()
        {
            Random rnd = new Random();
            List<UserDTO> users = new List<UserDTO>();

            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            {
                HttpResponseMessage response = await client.GetAsync("http://10.5.0.4:5001/getallusers");
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                users = JsonSerializer.Deserialize<List<UserDTO>>(jsonResponse);
            }

            var len = users.Count();
            List<TaskEnity> task2push = new List<TaskEnity>();
            var tasks = await _context.Tasks.ToListAsync();
            foreach (var task in tasks)
            {
                if (task.Status != StatusEnum.Cancelled)
                {
                    task2push.Add(new TaskEnity
                    {
                        Id = task.Id,
                        UserId = users[rnd.Next(len)].Id,
                        CreatedBy = task.CreatedBy,
                        Title = task.Title,
                        Description = task.Description,
                        Status = task.Status,
                        Estimation = task.Estimation,
                        CreatedAt = task.CreatedAt
                    });
                }
            }

            var options = new JsonSerializerOptions();
            options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));
            var message = JsonSerializer.Serialize(task2push, options);

            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(exchange: "", routingKey: "task_to_assign", basicProperties: null, body: body);

            return Ok();
        }
    }
}
