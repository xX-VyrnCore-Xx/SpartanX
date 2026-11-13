using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SpartanX.Core.Weather;

public sealed record WeatherSnapshot(string City, double Temperature, bool Fahrenheit, int WeatherCode, bool IsDay)
{
    public string TemperatureText => $"{Math.Round(Temperature)}°";

    /// <summary>Emoji for the WMO weather code.</summary>
    public string Glyph => WeatherCodes.Glyph(WeatherCode, IsDay);

    public string DescriptionKey => WeatherCodes.DescriptionKey(WeatherCode);
}

public interface IWeatherService
{
    Task<WeatherSnapshot?> GetCurrentAsync(string city, bool fahrenheit, CancellationToken cancellationToken = default);
}

/// <summary>
/// Weather from Open-Meteo (free, no API key, no account). Only the city name the user typed
/// and its coordinates are sent.
/// </summary>
public sealed class OpenMeteoWeatherService(HttpClient http) : IWeatherService
{
    private readonly Dictionary<string, (double Lat, double Lon, string Name)> _geocodeCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<WeatherSnapshot?> GetCurrentAsync(string city, bool fahrenheit, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_geocodeCache.TryGetValue(city, out var place))
            {
                var geo = await http.GetFromJsonAsync<GeocodeResponse>(
                    $"https://geocoding-api.open-meteo.com/v1/search?count=1&name={Uri.EscapeDataString(city)}",
                    cancellationToken).ConfigureAwait(false);
                var hit = geo?.Results?.FirstOrDefault();
                if (hit is null)
                {
                    return null;
                }

                place = (hit.Latitude, hit.Longitude, hit.Name);
                _geocodeCache[city] = place;
            }

            var url = string.Create(CultureInfo.InvariantCulture,
                $"https://api.open-meteo.com/v1/forecast?latitude={place.Lat}&longitude={place.Lon}&current=temperature_2m,weather_code,is_day{(fahrenheit ? "&temperature_unit=fahrenheit" : "")}");
            var forecast = await http.GetFromJsonAsync<ForecastResponse>(url, cancellationToken).ConfigureAwait(false);
            if (forecast?.Current is not { } c)
            {
                return null;
            }

            return new WeatherSnapshot(place.Name, c.Temperature, fahrenheit, c.WeatherCode, c.IsDay == 1);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null; // Offline: the island simply hides the weather activity.
        }
    }

    private sealed record GeocodeResponse([property: JsonPropertyName("results")] List<GeocodeResult>? Results);

    private sealed record GeocodeResult(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);

    private sealed record ForecastResponse([property: JsonPropertyName("current")] CurrentWeather? Current);

    private sealed record CurrentWeather(
        [property: JsonPropertyName("temperature_2m")] double Temperature,
        [property: JsonPropertyName("weather_code")] int WeatherCode,
        [property: JsonPropertyName("is_day")] int IsDay);
}

/// <summary>WMO weather interpretation codes used by Open-Meteo.</summary>
public static class WeatherCodes
{
    public static string DescriptionKey(int code) => code switch
    {
        0 => "Weather.Clear",
        1 or 2 => "Weather.PartlyCloudy",
        3 => "Weather.Overcast",
        45 or 48 => "Weather.Fog",
        >= 51 and <= 57 => "Weather.Drizzle",
        >= 61 and <= 67 or >= 80 and <= 82 => "Weather.Rain",
        >= 71 and <= 77 or 85 or 86 => "Weather.Snow",
        >= 95 => "Weather.Thunderstorm",
        _ => "Weather.Unknown",
    };

    /// <summary>Colour emoji for the code; rendered with Segoe UI Emoji.</summary>
    public static string Glyph(int code, bool isDay) => code switch
    {
        0 => isDay ? "\u2600\uFE0F" : "\U0001F319",
        1 or 2 => isDay ? "\u26C5" : "\U0001F319",
        3 => "\u2601\uFE0F",
        45 or 48 => "\U0001F32B\uFE0F",
        >= 51 and <= 57 => "\U0001F326\uFE0F",
        >= 61 and <= 67 or >= 80 and <= 82 => "\U0001F327\uFE0F",
        >= 71 and <= 77 or 85 or 86 => "\u2744\uFE0F",
        >= 95 => "\u26C8\uFE0F",
        _ => "\u2601\uFE0F",
    };
}
