using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text;



public class KafkaDummyProducer : BackgroundService
{
    private readonly ILogger<KafkaDummyProducer> _logger;
    private readonly IProducer<Null, string> _producer;

    public KafkaDummyProducer(ILogger<KafkaDummyProducer> logger)
    {
        _logger = logger;
        var config = new ProducerConfig { BootstrapServers = "localhost:19092" };
        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int userId = 1;
        while (!stoppingToken.IsCancellationRequested)
        {
            var payload = $"{{\"user_id\": {userId}, \"action\": \"click\", \"timestamp\": \"{DateTime.UtcNow:O}\"}}";
            await _producer.ProduceAsync("user_events", new Message<Null, string> { Value = payload });
            _logger.LogInformation("Produced event: {Payload}", payload);
            userId = new Random().Next(1,40);
            await Task.Delay(100, stoppingToken);
        }
    }
}

public class KafkaDummyConsumer : BackgroundService
{
    private readonly ILogger<KafkaDummyConsumer> _logger;

    public KafkaDummyConsumer(ILogger<KafkaDummyConsumer> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = "localhost:19092",
            GroupId = "streamapp-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe("user_events");

        return Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    _logger.LogInformation("Consumed: {Message}", result.Message.Value);
                }
                catch (OperationCanceledException) { break; }
            }
        }, stoppingToken);
    }
}


public static class MaterializeBootstrapper
{
    public static async Task EnsureStreamSetupAsync(string? connectionString)
    {
        connectionString ??= "Host=localhost;Port=6875;Username=materialize;Database=materialize";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var existing = new HashSet<string>();
        var cmd = new NpgsqlCommand("SHOW CONNECTIONS", conn);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                existing.Add(reader.GetString(0));
        }

        if (!existing.Contains("redpanda_conn"))
        {
            var createConn = new NpgsqlCommand(@"
                    CREATE CONNECTION redpanda_conn TO KAFKA (
                        BROKER 'redpanda:9092',
                        SECURITY PROTOCOL PLAINTEXT
                    );;", conn);
            await createConn.ExecuteNonQueryAsync();
        }

        existing.Clear();
        cmd = new NpgsqlCommand("SHOW SOURCES", conn);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                existing.Add(reader.GetString(0));
        }

        if (!existing.Contains("user_events"))
        {
            var createSource = new NpgsqlCommand(@"
                CREATE SOURCE user_events
                FROM KAFKA CONNECTION redpanda_conn (TOPIC 'user_events')
                FORMAT JSON;", conn);
            await createSource.ExecuteNonQueryAsync();
        }

        existing.Clear();
        cmd = new NpgsqlCommand("SHOW MATERIALIZED VIEWS;", conn);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                existing.Add(reader.GetString(0));
        }

        if (!existing.Contains("click_count_by_user"))
        {
            var createView = new NpgsqlCommand(@"CREATE MATERIALIZED VIEW click_count_by_user AS
                    SELECT
                      (data->>'user_id')::int AS user_id,
                      COUNT(*) AS total_clicks
                    FROM user_events
                    GROUP BY (data->>'user_id')::int;", 
                    conn);
            await createView.ExecuteNonQueryAsync();
        }
    }
}