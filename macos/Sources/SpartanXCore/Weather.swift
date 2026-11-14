import Foundation

public struct WeatherSnapshot: Equatable, Sendable {
    public let city: String
    public let temperature: Double
    public let code: Int
    public let isDay: Bool

    public init(city: String, temperature: Double, code: Int, isDay: Bool) {
        self.city = city
        self.temperature = temperature
        self.code = code
        self.isDay = isDay
    }

    public var temperatureText: String { "\(Int(temperature.rounded()))°" }

    /// SF Symbol for the WMO weather code.
    public var symbol: String {
        switch code {
        case 0: return isDay ? "sun.max.fill" : "moon.stars.fill"
        case 1, 2: return isDay ? "cloud.sun.fill" : "cloud.moon.fill"
        case 3: return "cloud.fill"
        case 45, 48: return "cloud.fog.fill"
        case 51...57: return "cloud.drizzle.fill"
        case 61...67, 80...82: return "cloud.rain.fill"
        case 71...77, 85, 86: return "cloud.snow.fill"
        case 95...: return "cloud.bolt.rain.fill"
        default: return "cloud.fill"
        }
    }

    public var descriptionKey: String {
        switch code {
        case 0: return "weather.clear"
        case 1, 2: return "weather.partlyCloudy"
        case 3: return "weather.overcast"
        case 45, 48: return "weather.fog"
        case 51...57: return "weather.drizzle"
        case 61...67, 80...82: return "weather.rain"
        case 71...77, 85, 86: return "weather.snow"
        case 95...: return "weather.thunderstorm"
        default: return "weather.unknown"
        }
    }
}

/// Weather from Open-Meteo (free, no account). Only the city name and its coordinates are sent.
public actor WeatherClient {
    private var geocodeCache: [String: (lat: Double, lon: Double, name: String)] = [:]
    private let session: URLSession

    public init(session: URLSession = .shared) {
        self.session = session
    }

    private struct Geo: Decodable {
        struct Hit: Decodable { let name: String; let latitude: Double; let longitude: Double }
        let results: [Hit]?
    }

    private struct Forecast: Decodable {
        struct Current: Decodable {
            let temperature_2m: Double
            let weather_code: Int
            let is_day: Int
        }
        let current: Current?
    }

    public func current(city: String, fahrenheit: Bool) async -> WeatherSnapshot? {
        let key = city.lowercased()
        do {
            if geocodeCache[key] == nil {
                var c = URLComponents(string: "https://geocoding-api.open-meteo.com/v1/search")!
                c.queryItems = [URLQueryItem(name: "count", value: "1"), URLQueryItem(name: "name", value: city)]
                let (data, _) = try await session.data(from: c.url!)
                guard let hit = try JSONDecoder().decode(Geo.self, from: data).results?.first else { return nil }
                geocodeCache[key] = (hit.latitude, hit.longitude, hit.name)
            }

            guard let place = geocodeCache[key] else { return nil }
            var c = URLComponents(string: "https://api.open-meteo.com/v1/forecast")!
            var items: [URLQueryItem] = [
                URLQueryItem(name: "latitude", value: String(place.lat)),
                URLQueryItem(name: "longitude", value: String(place.lon)),
                URLQueryItem(name: "current", value: "temperature_2m,weather_code,is_day"),
            ]
            if fahrenheit {
                items.append(URLQueryItem(name: "temperature_unit", value: "fahrenheit"))
            }
            c.queryItems = items
            let (data, _) = try await session.data(from: c.url!)
            guard let cur = try JSONDecoder().decode(Forecast.self, from: data).current else { return nil }
            return WeatherSnapshot(city: place.name, temperature: cur.temperature_2m, code: cur.weather_code, isDay: cur.is_day == 1)
        } catch {
            return nil // Offline: the island simply hides the weather.
        }
    }
}
