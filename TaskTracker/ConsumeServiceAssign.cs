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
    public class ConsumeServiceAssign : BackgroundService
    {
        private readonly AppDbContext _context;

        private SchemaRegistry<List<TaskDTO>> _schemaRegistryTaskEnity;
        private SchemaRegistry<List<DoubleEntryBookkeepingDTO>> _schemaRegistryDoubleEntryBookkeepingDTO;

        private ConnectionFactory _factory;
        private IConnection _connection;

        private IModel _channelAssign;
        private IModel _channelDoubleEntry;
        private readonly string _queueAssign = "task_to_assign";
        private readonly string _queueDoubleEntry = "double_entry_to_billing";

        public ConsumeServiceAssign(AppDbContext context)
        {
            _context = context;

            _schemaRegistryTaskEnity = new SchemaRegistry<List<TaskDTO>>();
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

            _channelDoubleEntry = _connection.CreateModel();
            _channelDoubleEntry.QueueDeclare(queue: _queueDoubleEntry, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channelAssign);
            consumer.Received += (ch, ea) =>
            {
                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonStringEnumConverter());
                options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));

                List<TaskDTO>? task2push;
                List<DoubleEntryBookkeepingDTO> lstBookkeeping = new List<DoubleEntryBookkeepingDTO>();

                string message = Encoding.UTF8.GetString(ea.Body.ToArray());

                if (_schemaRegistryTaskEnity.ValidateSchema(message) && message != null)
                    task2push = JsonSerializer.Deserialize<List<TaskDTO>>(message, options);
                else throw new InvalidOperationException("JSON Schema is not valid");

                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        var source = _context.Tasks.ToList();
                        foreach (var task in source)
                        {
                            var t = task2push?.FirstOrDefault(s => s.PublicId == task.PublicId);
                            task.Cost = t!.Cost;
                            task.Reward = t.Reward;
                            task.UserId = t.UserId;

                            lstBookkeeping.Add(new DoubleEntryBookkeepingDTO()
                            {
                                TransactionType = TransactionTypeEnum.Outcome,
                                UserId = t.UserId,
                                TaskId = t.PublicId,
                                Value = t.Cost
                            });
                        }
                        _context.SaveChanges();

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }

                message = JsonSerializer.Serialize(lstBookkeeping, options);
                if (_schemaRegistryDoubleEntryBookkeepingDTO.ValidateSchema(message))
                {
                    var body = Encoding.UTF8.GetBytes(message);
                    _channelDoubleEntry.BasicPublish(exchange: "", routingKey: _queueDoubleEntry, basicProperties: null, body: body);
                }
                else throw new InvalidOperationException("JSON Schema is not valid");

                _channelAssign.BasicAck(ea.DeliveryTag, false);
            };

            _channelAssign.BasicConsume(queue: _queueAssign, autoAck: false, consumer: consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channelAssign.Close();
            _channelDoubleEntry.Close();
            _connection.Close();
            base.Dispose();
        }
    }
}
