using Contracts;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MyClient
{
    class Program
    {
        private static Random rnd = new Random();

        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[rnd.Next(s.Length)]).ToArray());
        }

        public static void Main(string[] args)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzUxMiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiIxIiwic3ViIjoiQnJvbnplYmVhcmRAcG9wdWdpbmMuY29tIiwiZW1haWwiOiJCcm9uemViZWFyZEBwb3B1Z2luYy5jb20iLCJ1c2VyaWQiOiIxIiwiUG9wdWciOnRydWUsIm5iZiI6MTcwOTQ3MzM5MCwiZXhwIjoxNzA5NTAyMTg5LCJpYXQiOjE3MDk0NzMzOTAsImlzcyI6IklkIiwiYXVkIjoiVGFza3MifQ.JdpQ-0124_LPRjeXNtz21HoqHgn0T09z_9XK9U3lCT3Yul_5al_6TaLeHSv7JGnhEqEsZjRY1aaG8BiXtXTvxg");

                HttpResponseMessage response = client.GetAsync("http://localhost:5001/getallusers").Result;
                response.EnsureSuccessStatusCode();
                var jsonResponse = response.Content.ReadAsStringAsync().Result;
                var users = JsonSerializer.Deserialize<List<UserDTO>>(jsonResponse);
                Console.WriteLine(jsonResponse);
                Console.WriteLine();

                for (int i = 0; i < 20; i++)
                {
                    var task = new TaskDTO()
                    {
                        UserId = rnd.Next(1, users.Count + 1),
                        CreatedBy = rnd.Next(1, users.Count + 1),
                        Title = RandomString(32),
                        Description = RandomString(256),
                        Status = StatusEnum.Open,
                        Estimation = 7,
                        CreatedAt = DateTime.Now,
                    };

                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new CustomDateTimeConverter("yyyy-MM-ddTHH:mm:ss.fffZ"));

                    StringContent content = new StringContent(JsonSerializer.Serialize(task, options), Encoding.UTF8, "application/json");
                    response = client.PostAsync("http://localhost:5000/api/Tasks", content).Result;
                    string responseText = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine(responseText);
                }

                Thread.Sleep(3000);
                Console.WriteLine();
                for (int i = 0; i < 100; i++)
                {
                    StringContent content = new StringContent("");
                    response = client.PostAsync("http://localhost:5000/api/Tasks/assigntasks", content).Result;
                    string responseText = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine(responseText);
                }
            }
        }
    }
}
    
