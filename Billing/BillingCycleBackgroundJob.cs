using NRedisStack.RedisStackCommands;
using NRedisStack;
using Quartz;
using StackExchange.Redis;
using System.Text.Json.Serialization;
using System.Text.Json;
using Contracts;
using Billing.Models;
using MimeKit;
using System.Net;
using MailKit.Net.Smtp;
using System.Text;
using RabbitMQ.Client;

namespace Billing
{
    public class BillingCycleBackgroundJob : IJob
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BillingCycleBackgroundJob> _logger;

        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _channelAnalytics;
        private readonly string _queueAnalytics = "to_analytics";

        private ConnectionMultiplexer _redis;
        IDatabase _db;

        public BillingCycleBackgroundJob(AppDbContext context, ILogger<BillingCycleBackgroundJob> logger)
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
            _channelAnalytics = _connection.CreateModel();
            _channelAnalytics.QueueDeclare(queue: _queueAnalytics, durable: true, exclusive: false, autoDelete: false, arguments: null);

            _redis = ConnectionMultiplexer.Connect("10.5.0.7");
            _db = _redis.GetDatabase();
        }

        private void SendEmail(string name, string email, int balance)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("System", "system@popuginc.com"));
            message.To.Add(new MailboxAddress(name, email));
            message.Subject = "Payment for today";

            message.Body = new TextPart("plain")
            {
                Text = $"{balance}"
            };

            using (var client = new SmtpClient())
            {
                client.Connect("smtp.popuginc.com", 587, false);

                client.Send(message);
                client.Disconnect(true);
            }
        }

        public Task Execute(IJobExecutionContext context)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));

            _logger.LogInformation($"{DateTime.UtcNow}");

            List<AccountEntity>? lstAccountEntityTemp = new List<AccountEntity>();
            List<UserDTO>? users = new List<UserDTO>();
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            {
                HttpResponseMessage response = client.GetAsync("http://10.5.0.4:5001/getallusers").Result;
                response.EnsureSuccessStatusCode();
                var jsonResponse = response.Content.ReadAsStringAsync().Result;
                users = JsonSerializer.Deserialize<List<UserDTO>>(jsonResponse, options);
            }
            users = users!.Where(x => x.Role == RoleEnum.Popug).ToList();

            DateTime dt = DateTime.UtcNow;
            string billingCycleId = $"{dt.Year}{dt.Month}{dt.Day}";

            JsonCommands json = _db.JSON();
            var result = json.Get(key: billingCycleId, path: "$");
            if (result.IsNull == false)
            {
                var message = result.ToString();
                if (message!.Length > 2)
                    message = message.Substring(1, message.Length - 2);

                var bookkeeping = JsonSerializer.Deserialize<DoubleEntryBookkeepingRedis>(message, options);
                if (bookkeeping != null)
                {
                    json.Del(billingCycleId);

                    var usersIds = bookkeeping.Records.Select(p => p.UserId)
                               .Distinct()
                               .ToList();

                    foreach (var id in usersIds)
                    {
                        int sum = 0, income = 0, outcome = 0;
                        var lst = bookkeeping.Records.Where(p => p.UserId == id).ToList();
                        foreach (var entry in lst)
                        {
                            if (entry.TransactionType == TransactionTypeEnum.Income)
                                income += entry.Value;

                            if (entry.TransactionType == TransactionTypeEnum.Outcome)
                                outcome += entry.Value;
                        }
                        sum = income - outcome;

                        using (var transaction = _context.Database.BeginTransaction())
                        {
                            try
                            {
                                var ae = new AccountEntity
                                {
                                    Balance = sum,
                                    BillingCycleId = billingCycleId,
                                    UserId = id,
                                };

                                lstAccountEntityTemp.Add(ae);

                                _context.Accounts.Add(ae);
                                _context.SaveChanges();

                                transaction.Commit();
                            }
                            catch (Exception ex)
                            {
                                throw new Exception(ex.Message);
                            }
                        }
                    }

                    // Send to analytics (сколько заработал топ-менеджмент за сегодня)
                    var body = Encoding.UTF8.GetBytes(message);
                    _channelAnalytics.BasicPublish(exchange: "", routingKey: _queueAnalytics, basicProperties: null, body: body);
                }

                if (lstAccountEntityTemp != null)
                {
                    foreach (var user in users)
                    {
                        int b = lstAccountEntityTemp!.FirstOrDefault(p => p.UserId == user.Id).Balance;
                        //SendEmail(user.Name, user.Email, b);
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
