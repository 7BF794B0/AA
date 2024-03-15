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

        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _channel;
        private readonly string _queueName = "task_to_assign";

        public ConsumeServiceAssign(AppDbContext context)
        {
            _context = context;

            _schemaRegistryTaskEnity = new SchemaRegistry<List<TaskDTO>>();

            _factory = new ConnectionFactory()
            {
                UserName = "rabbitmq",
                Password = "rabbitmq",
                HostName = "10.5.0.3"
            };

            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (ch, ea) =>
            {
                List<TaskDTO>? task2push;

                string message = Encoding.UTF8.GetString(ea.Body.ToArray());
                if (_schemaRegistryTaskEnity.ValidateSchema(message) && message != null)
                {
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonStringEnumConverter());
                    options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));
                    task2push = JsonSerializer.Deserialize<List<TaskDTO>>(message, options);
                }
                else
                {
                    throw new InvalidOperationException("JSON Schema is not valid");
                }

                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        var source = _context.Tasks.ToList();
                        foreach (var task in source)
                        {
                            var t = task2push?.FirstOrDefault(s => s.PublicId == task.PublicId);
                            task.Cost = t.Cost;
                            task.Reward = t.Reward;
                            task.UserId = t.UserId;
                        }
                        _context.SaveChanges();

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }

                // TODO: ВОТ ТУТ НАДО СПИСЫВАТЬ ДЕНЬГИ С ПОПУГОВ

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel.Close();
            _connection.Close();
            base.Dispose();
        }
    }
}
