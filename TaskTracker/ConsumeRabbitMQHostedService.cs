using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using TaskTracker.Controllers;
using TaskTracker.Models;

namespace TaskTracker
{
    public class ConsumeRabbitMQHostedService : BackgroundService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TasksController> _logger;

        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _channel;

        public ConsumeRabbitMQHostedService(AppDbContext context, ILogger<TasksController> logger)
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

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (ch, ea) =>
            {
                var message = Encoding.UTF8.GetString(ea.Body.ToArray());

                var options = new JsonSerializerOptions();
                options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));
                List<TaskEnity> task2push = JsonSerializer.Deserialize<List<TaskEnity>>(message);

                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        var source = _context.Tasks.ToList();
                        source = task2push;
                        _context.SaveChanges();

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queue: "task_to_assign", autoAck: false, consumer: consumer);

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
