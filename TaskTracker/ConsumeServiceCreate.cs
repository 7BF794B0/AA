using Contracts;
using EventSchemaRegistry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskTracker.Controllers;
using TaskTracker.Models;

namespace TaskTracker
{
    public class ConsumeServiceCreate : BackgroundService
    {
        private readonly AppDbContext _context;

        private SchemaRegistry<TaskDTO> _schemaRegistryTaskDTO;

        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _createChannel;
        private readonly string _createQueue = "task_to_create";

        public ConsumeServiceCreate(AppDbContext context)
        {
            _context = context;

            _schemaRegistryTaskDTO = new SchemaRegistry<TaskDTO>();

            _factory = new ConnectionFactory()
            {
                UserName = "rabbitmq",
                Password = "rabbitmq",
                HostName = "10.5.0.3"
            };

            _connection = _factory.CreateConnection();
            _createChannel = _connection.CreateModel();
            _createChannel.QueueDeclare(queue: _createQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_createChannel);
            consumer.Received += async (ch, ea) =>
            {
                string message = Encoding.UTF8.GetString(ea.Body.ToArray());
                if (_schemaRegistryTaskDTO.ValidateSchema(message) && message != null)
                {
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonStringEnumConverter());
                    options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));

                    var t = JsonSerializer.Deserialize<TaskDTO>(message, options);
                    var entity = new TaskEnity()
                    {
                        UserId = t!.UserId,
                        CreatedBy = t.CreatedBy,
                        Title = t.Title,
                        Description = t.Description,
                        Status = t.Status,
                        Estimation = t.Estimation,
                        CreatedAt = t.CreatedAt,
                        Cost = t.Cost,
                        Reward = t.Reward
                    };
                    if (t.JiraId != null)
                        entity.JiraId = t.JiraId;

                    _context.Tasks.Add(entity);
                    await _context.SaveChangesAsync();
                }
                else throw new InvalidOperationException("JSON Schema is not valid");

                _createChannel.BasicAck(ea.DeliveryTag, false);
            };

            _createChannel.BasicConsume(queue: _createQueue, autoAck: false, consumer: consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _createChannel.Close();
            _connection.Close();
            base.Dispose();
        }
    }
}
