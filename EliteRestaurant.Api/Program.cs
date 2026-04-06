using EliteRestaurant.Api.Security;
using EliteRestaurantPro.Data;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
AppDbContext.Initialize();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<TabletAuthService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LanOnly", policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("LanOnly");
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();
