using Contracts;
using EventSchemaRegistry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace Analytics
{
    public class ConsumeServiceAnalytics : BackgroundService
    {
        private SchemaRegistry<List<DoubleEntryBookkeepingDTO>> _schemaRegistryListDoubleEntryBookkeepingDTO;
        private SchemaRegistry<DoubleEntryBookkeepingRedis> _schemaRegistryDoubleEntryBookkeepingRedis;

        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _channelAnalytics;
        private readonly string _queueAnalytics = "to_analytics";

        public ConsumeServiceAnalytics()
        {
            _schemaRegistryListDoubleEntryBookkeepingDTO = new SchemaRegistry<List<DoubleEntryBookkeepingDTO>>();
            _schemaRegistryDoubleEntryBookkeepingRedis = new SchemaRegistry<DoubleEntryBookkeepingRedis>();

            _factory = new ConnectionFactory()
            {
                UserName = "rabbitmq",
                Password = "rabbitmq",
                HostName = "10.5.0.3"
            };
            _connection = _factory.CreateConnection();

            _channelAnalytics = _connection.CreateModel();
            _channelAnalytics.QueueDeclare(queue: _queueAnalytics, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channelAnalytics);
            consumer.Received += (ch, ea) =>
            {
                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonStringEnumConverter());
                options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));

                string message = Encoding.UTF8.GetString(ea.Body.ToArray());
                if (_schemaRegistryListDoubleEntryBookkeepingDTO.ValidateSchema(message) && message != null)
                {
                    // Аналитика: самая дорогая задача за день
                    var data = JsonSerializer.Deserialize<List<DoubleEntryBookkeepingDTO>>(message, options);
                    var maxExpensiveTask = data!.Where(x => x.TransactionType == TransactionTypeEnum.Income).FirstOrDefault();

                    using (StreamWriter sw = new StreamWriter("task.txt"))
                    {
                        sw.WriteLine(JsonSerializer.Serialize(maxExpensiveTask, options));
                    }
                }
                else if(_schemaRegistryDoubleEntryBookkeepingRedis.ValidateSchema(message) && message != null)
                {
                    // Аналитика: сколько заработал топ-менеджмент за сегодня
                    var data = JsonSerializer.Deserialize<DoubleEntryBookkeepingRedis>(message, options)!.Records;
                    int total = 0, income = 0, outcome = 0;
                    foreach (var item in data)
                    {
                        if (item.TransactionType == TransactionTypeEnum.Income)
                            income += item.Value;

                        if (item.TransactionType == TransactionTypeEnum.Outcome)
                            outcome += item.Value;
                    }
                    total = income - outcome;

                    using (StreamWriter sw = new StreamWriter("total.txt"))
                    {
                        sw.WriteLine(total);
                    }
                }
                else throw new InvalidOperationException("JSON Schema is not valid");

                _channelAnalytics.BasicAck(ea.DeliveryTag, false);
            };

            _channelAnalytics.BasicConsume(queue: _queueAnalytics, autoAck: false, consumer: consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channelAnalytics.Close();
            _connection.Close();
            base.Dispose();
        }
    }
}
