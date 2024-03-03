using JwtAuth.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddControllers();
var app = builder.Build();
SeedData.Initialize(app.Configuration);
app.UseAuthorization();
app.MapControllers();
app.Run();
