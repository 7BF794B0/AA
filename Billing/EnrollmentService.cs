using Contracts;
using EventSchemaRegistry;
using RabbitMQ.Client;
using System.Text.Json.Serialization;
using System.Text.Json;
using RabbitMQ.Client.Events;
using System.Text;
using Billing.Models;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;

namespace Billing
{
    public class EnrollmentService : BackgroundService
    {
        private ConnectionMultiplexer _redis;
        IDatabase _db;

        private SchemaRegistry<List<DoubleEntryBookkeepingDTO>> _schemaRegistryDoubleEntryBookkeepingDTO;

        private ConnectionFactory _factory;
        private IConnection _connection;

        private IModel _channelDoubleEntry;
        private IModel _channelAnalytics;

        private readonly string _queueDoubleEntry = "double_entry_to_billing";
        private readonly string _queueAnalytics = "to_analytics";

        public EnrollmentService()
        {
            _schemaRegistryDoubleEntryBookkeepingDTO = new SchemaRegistry<List<DoubleEntryBookkeepingDTO>>();

            _redis = ConnectionMultiplexer.Connect("10.5.0.7");
            _db = _redis.GetDatabase();

            _factory = new ConnectionFactory()
            {
                UserName = "rabbitmq",
                Password = "rabbitmq",
                HostName = "10.5.0.3"
            };
            _connection = _factory.CreateConnection();

            _channelDoubleEntry = _connection.CreateModel();
            _channelDoubleEntry.QueueDeclare(queue: _queueDoubleEntry, durable: true, exclusive: false, autoDelete: false, arguments: null);

            _channelAnalytics = _connection.CreateModel();
            _channelAnalytics.QueueDeclare(queue: _queueAnalytics, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channelDoubleEntry);
            consumer.Received += (ch, ea) =>
            {
                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonStringEnumConverter());
                options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));

                string message = Encoding.UTF8.GetString(ea.Body.ToArray());
                if (_schemaRegistryDoubleEntryBookkeepingDTO.ValidateSchema(message) && message != null)
                {
                    try
                    {
                        var lstBookkeeping = JsonSerializer.Deserialize<List<DoubleEntryBookkeepingDTO>>(message, options);
                        if (lstBookkeeping != null)
                        {
                            DateTime dt = DateTime.UtcNow;
                            string billingCycleId = $"{dt.Year}{dt.Month}{dt.Day}";

                            JsonCommands json = _db.JSON();
                            if (_db.KeyExists(billingCycleId))
                            {
                                foreach (var item in lstBookkeeping)
                                    json.ArrAppend(billingCycleId, "$.Records", item);
                            }
                            else
                            {
                                json.Set(billingCycleId, "$", new DoubleEntryBookkeepingRedis()
                                {
                                    BillingCycleId = billingCycleId,
                                    Records = lstBookkeeping
                                });
                            }

                            // Send to analytics (самая дорогая задача за день)
                            var body = Encoding.UTF8.GetBytes(message);
                            _channelAnalytics.BasicPublish(exchange: "", routingKey: _queueAnalytics, basicProperties: null, body: body);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
                else throw new InvalidOperationException("JSON Schema is not valid");

                _channelDoubleEntry.BasicAck(ea.DeliveryTag, false);
            };

            _channelDoubleEntry.BasicConsume(queue: _queueDoubleEntry, autoAck: false, consumer: consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channelDoubleEntry.Close();
            _channelAnalytics.Close();
            _connection.Close();
            base.Dispose();
        }
    }
}
