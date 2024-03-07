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
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            using (var connection = _factory.CreateConnection())
            {
                using (var chanel = connection.CreateModel())
                {
                    chanel.QueueDeclare(
                        queue: "my_queue",
                        exclusive: false,
                        durable: true,
                        autoDelete: false,
                        arguments: null
                        );

                    var consumer = new EventingBasicConsumer(chanel);

                    consumer.Received += (model, es) =>
                    {
                        var body = es.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        var source = _context.Tasks.ToList();

                        var options = new JsonSerializerOptions();
                        options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));
                        List<TaskEnity> task2push = JsonSerializer.Deserialize<List<TaskEnity>>(message);
                        source = task2push;
                        _context.SaveChanges();
                    };

                    chanel.BasicConsume(
                        queue: "my_queue",
                        autoAck: true,
                        consumer: consumer
                    );
                }
            }

            return Task.CompletedTask;
        }
    }
}
