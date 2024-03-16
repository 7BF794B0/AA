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
using System.Text.Json.Serialization;
using EventSchemaRegistry;
using Asp.Versioning;

namespace TaskTracker.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TasksController> _logger;

        private SchemaRegistry<List<TaskDTO>> _schemaRegistryListTaskDTO;
        private SchemaRegistry<TaskDTO> _schemaRegistryTaskDTO;
        private SchemaRegistry<List<DoubleEntryBookkeepingDTO>> _schemaRegistryDoubleEntryBookkeepingDTO;

        private ConnectionFactory _factory;
        private IConnection _connection;

        private IModel _channelAssign;
        private IModel _channelMonetary;
        private IModel _createChannel;
        private IModel _channelDoubleEntry;

        private readonly string _queueAssign = "task_to_assign";
        private readonly string _queueMonetary = "task_to_monetary";
        private readonly string _createQueue = "task_to_create";
        private readonly string _queueDoubleEntry = "double_entry_to_billing";

        public TasksController(AppDbContext context, ILogger<TasksController> logger)
        {
            _context = context;
            _logger = logger;

            _schemaRegistryListTaskDTO = new SchemaRegistry<List<TaskDTO>>();
            _schemaRegistryTaskDTO = new SchemaRegistry<TaskDTO>();
            _schemaRegistryDoubleEntryBookkeepingDTO = new SchemaRegistry<List<DoubleEntryBookkeepingDTO>>();

            _factory = new ConnectionFactory()
            {
                UserName = "rabbitmq",
                Password = "rabbitmq",
                HostName = "10.5.0.3"
            };
            _connection = _factory.CreateConnection();

            _channelAssign = _connection.CreateModel();
            _channelAssign.QueueDeclare(queue: _queueAssign, durable: true, exclusive: false, autoDelete: false, arguments: null);

            _channelMonetary = _connection.CreateModel();
            _channelMonetary.QueueDeclare(queue: _queueMonetary, durable: true, exclusive: false, autoDelete: false, arguments: null);

            _createChannel = _connection.CreateModel();
            _createChannel.QueueDeclare(queue: _createQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);

            _channelDoubleEntry = _connection.CreateModel();
            _channelDoubleEntry.QueueDeclare(queue: _queueDoubleEntry, durable: true, exclusive: false, autoDelete: false, arguments: null);
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
                CreatedAt = task.CreatedAt,
                Cost = task.Cost,
                Reward = task.Reward
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
            var task = await _context.Tasks.FirstOrDefaultAsync(f => f.PublicId == id);

            if (task == null)
            {
                return NotFound();
            }

            return task;
        }

        // POST: api/Tasks
        [Authorize(Policy = IdentityData.PopugUserPolicyName)]
        [HttpPost]
        public async Task<ActionResult<TaskDTO>> AddTask(TaskRequest taskRequest, ApiVersion apiVersion)
        {
            string GetStringBetweenCharacters(string input, char charFrom, char charTo)
            {
                int posFrom = input.IndexOf(charFrom);
                if (posFrom != -1)
                {
                    int posTo = input.IndexOf(charTo, posFrom + 1);
                    if (posTo != -1)
                    {
                        return input.Substring(posFrom, posTo - posFrom + 1);
                    }
                }

                return string.Empty;
            }

            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));

            string message = string.Empty;

            var taskDto = new TaskDTO
            {
                UserId = taskRequest.UserId,
                CreatedBy = taskRequest.CreatedBy,
                Title = taskRequest.Title,
                Description = taskRequest.Description,
                Status = taskRequest.Status,
                Estimation = taskRequest.Estimation,
                CreatedAt = taskRequest.CreatedAt
            };

            if (apiVersion.MajorVersion == 2)
            {
                string substring = GetStringBetweenCharacters(taskDto.Title, '[', ']');
                if (substring != string.Empty)
                {
                    taskDto.Title = taskRequest.Title.Replace(substring, "");
                    taskDto.JiraId = substring;
                }
                else
                {
                    taskDto.Title = taskRequest.Title;
                    taskDto.JiraId = taskRequest.JiraId;
                }
            }

            message = JsonSerializer.Serialize(taskDto, options);
            if (_schemaRegistryTaskDTO.ValidateSchema(message))
            {
                var body = Encoding.UTF8.GetBytes(message);
                _channelMonetary.BasicPublish(exchange: "", routingKey: _queueMonetary, basicProperties: null, body: body);
            }
            else return BadRequest("JSON Schema is not valid");

            return Ok();
        }

        // POST: api/closetask/5
        [Authorize(Policy = IdentityData.PopugUserPolicyName)]
        [HttpPost("closetask/{id}")]
        public async Task<IActionResult> CloseTask(int id)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));

            TaskEnity? task = await _context.Tasks.FirstOrDefaultAsync(f => f.PublicId == id);
            if (task != null)
            {
                List<DoubleEntryBookkeepingDTO> lstBookkeeping =
                [
                    new DoubleEntryBookkeepingDTO()
                    {
                        TransactionType = TransactionTypeEnum.Income,
                        UserId = task.UserId,
                        TaskId = task.PublicId,
                        Value = task.Reward
                    },
                ];

                task.Status = StatusEnum.Cancelled;
                _context.SaveChanges();

                var message = JsonSerializer.Serialize(lstBookkeeping, options);
                if (_schemaRegistryDoubleEntryBookkeepingDTO.ValidateSchema(message))
                {
                    var body = Encoding.UTF8.GetBytes(message);
                    _channelDoubleEntry.BasicPublish(exchange: "", routingKey: _queueDoubleEntry, basicProperties: null, body: body);
                }
                else
                {
                    return BadRequest("JSON Schema is not valid");
                }
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
        public async Task<IActionResult> AssignTasks()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));

            Random rnd = new Random();
            List<UserDTO>? users = new List<UserDTO>();

            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            {
                HttpResponseMessage response = await client.GetAsync("http://10.5.0.4:5001/getallusers");
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                users = JsonSerializer.Deserialize<List<UserDTO>>(jsonResponse, options);
            }

            users = users!.Where(x => x.Role == RoleEnum.Popug).ToList();

            int len = 0;
            if (users != null)
                len = users.Count;
            else throw new InvalidOperationException("User list was not found");

            List<TaskDTO>? task2push = new List<TaskDTO>();
            var tasks = await _context.Tasks.ToListAsync();
            foreach (var task in tasks)
            {
                if (task.Status != StatusEnum.Cancelled)
                {
                    task2push.Add(new TaskDTO
                    {
                        PublicId = task.PublicId,
                        UserId = users[rnd.Next(len)].Id,
                        CreatedBy = task.CreatedBy,
                        Title = task.Title,
                        Description = task.Description,
                        Status = task.Status,
                        Estimation = task.Estimation,
                        CreatedAt = task.CreatedAt,
                        Cost = task.Cost,
                        Reward = task.Reward
                    });
                }
            }

            var message = JsonSerializer.Serialize(task2push, options);
            if (_schemaRegistryListTaskDTO.ValidateSchema(message))
            {
                var body = Encoding.UTF8.GetBytes(message);
                _channelAssign.BasicPublish(exchange: "", routingKey: _queueAssign, basicProperties: null, body: body);
            }
            else
            {
                return BadRequest("JSON Schema is not valid");
            }

            return Ok();
        }
    }
}
