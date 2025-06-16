namespace AspireApp1.Web;

public class WeatherApiClient(HttpClient httpClient)
{
    public async Task<UserActionData[]> GetWeatherAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<UserActionData>? forecasts = null;

        await foreach (var forecast in httpClient.GetFromJsonAsAsyncEnumerable<UserActionData>("/weatherforecast", cancellationToken))
        {
           
            if (forecast is not null)
            {
                forecasts ??= [];
                forecasts.Add(forecast);
            }
        }

        return forecasts?.ToArray() ?? [];
    }
}
public record UserActionData(string Action, int Count);
public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
