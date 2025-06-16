using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHostedService<KafkaDummyProducer>();
builder.Services.AddHostedService<KafkaDummyConsumer>();

var connectionString = builder.Configuration.GetConnectionString("Materialize")
                     ?? "Host=localhost;Port=6875;Username=materialize";
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}



app.MapGet("/weatherforecast", async (IConfiguration config) =>
{
    var connString = config.GetConnectionString("Materialize")
                     ?? "Host=localhost;Port=6875;Username=materialize";
    await MaterializeBootstrapper.EnsureStreamSetupAsync(connString);
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    var cmd = new NpgsqlCommand("select user_id as Action, total_clicks as Count from click_count_by_user  ", conn);
    var reader = await cmd.ExecuteReaderAsync();

    var results = new List<UserActionData>();
    while (await reader.ReadAsync())
    {
        results.Add(new UserActionData
        ( reader.GetInt32(0).ToString(),
            reader.GetInt32(1)
        ));
    }

    return results;
})
.WithName("GetWeatherForecast");

app.MapDefaultEndpoints();

app.Run();

record UserActionData(string Action, int Count);
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
