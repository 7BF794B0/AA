using JwtAuth.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    var enumConverter = new JsonStringEnumConverter();
    opts.JsonSerializerOptions.Converters.Add(enumConverter);
});
var app = builder.Build();
SeedData.Initialize(app.Configuration);
app.UseAuthorization();
app.MapControllers();
app.Run();
