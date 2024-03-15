using Contracts;
using EventSchemaRegistry;
using RabbitMQ.Client;
using System.Text.Json.Serialization;
using System.Text.Json;
using RabbitMQ.Client.Events;
using System.Text;

namespace Billing
{
    public class CalculateTaskCostService : BackgroundService
    {
        private SchemaRegistry<TaskDTO> _schemaRegistryTaskDTO;

        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _channelMonetary;
        private IModel _createChannel;
        private readonly string _queueMonetary = "task_to_monetary";
        private readonly string _createQueue = "task_to_create";

        public CalculateTaskCostService()
        {
            _schemaRegistryTaskDTO = new SchemaRegistry<TaskDTO>();

            _factory = new ConnectionFactory()
            {
                UserName = "rabbitmq",
                Password = "rabbitmq",
                HostName = "10.5.0.3"
            };
            _connection = _factory.CreateConnection();

            _channelMonetary = _connection.CreateModel();
            _channelMonetary.QueueDeclare(queue: _queueMonetary, durable: true, exclusive: false, autoDelete: false, arguments: null);

            _createChannel = _connection.CreateModel();
            _createChannel.QueueDeclare(queue: _createQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channelMonetary);
            consumer.Received += (ch, ea) =>
            {
                Random rnd = new Random();
                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonStringEnumConverter());
                options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));

                string message = Encoding.UTF8.GetString(ea.Body.ToArray());
                if (_schemaRegistryTaskDTO.ValidateSchema(message) && message != null)
                {
                    try
                    {
                        var task = JsonSerializer.Deserialize<TaskDTO>(message, options);
                        task.Cost = rnd.Next(10, 21);
                        task.Reward = rnd.Next(20, 41);

                        message = JsonSerializer.Serialize(task, options);
                        if (_schemaRegistryTaskDTO.ValidateSchema(message))
                        {
                            var body = Encoding.UTF8.GetBytes(message);
                            _createChannel.BasicPublish(exchange: "", routingKey: _createQueue, basicProperties: null, body: body);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
                else throw new InvalidOperationException("JSON Schema is not valid");

                _channelMonetary.BasicAck(ea.DeliveryTag, false);
            };

            _channelMonetary.BasicConsume(queue: _queueMonetary, autoAck: false, consumer: consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channelMonetary.Close();
            _createChannel.Close();
            _connection.Close();
            base.Dispose();
        }
    }
}
