import Foundation

/// Every user preference. Stored as JSON in UserDefaults by the app's SettingsStore.
public struct AppSettings: Codable, Equatable, Sendable {
    public enum Language: String, Codable, CaseIterable, Sendable {
        case system, en, it
    }

    public enum Interaction: String, Codable, CaseIterable, Sendable {
        case hoverToPeek, clickOnly
    }

    public enum Motion: String, Codable, CaseIterable, Sendable {
        case smooth, snappy, bouncy

        /// SwiftUI spring parameters (response, damping fraction).
        public var spring: (response: Double, damping: Double) {
            switch self {
            case .smooth: return (0.42, 0.80)
            case .snappy: return (0.30, 0.92)
            case .bouncy: return (0.50, 0.62)
            }
        }
    }

    public var firstRunCompleted = false
    public var language: Language = .system
    public var launchAtLogin = true
    public var checkForUpdates = true
    public var includePrereleases = false

    public var interaction: Interaction = .hoverToPeek
    public var autoExpandOnEvents = true
    public var autoCollapseSeconds: Double = 4
    public var showOnAllScreens = false

    public var mediaModule = true
    public var timerModule = true
    public var batteryModule = true
    public var weatherModule = true
    public var weatherCity = ""
    public var fahrenheit = false

    public var motion: Motion = .smooth
    public var reduceMotion = false

    public init() {}

    /// Tolerant decoding: keys added in newer versions fall back to their defaults.
    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        let d = AppSettings()
        func v<T: Decodable>(_ key: CodingKeys, _ fallback: T) -> T {
            (try? c.decodeIfPresent(T.self, forKey: key)) ?? fallback
        }

        firstRunCompleted = v(.firstRunCompleted, d.firstRunCompleted)
        language = v(.language, d.language)
        launchAtLogin = v(.launchAtLogin, d.launchAtLogin)
        checkForUpdates = v(.checkForUpdates, d.checkForUpdates)
        includePrereleases = v(.includePrereleases, d.includePrereleases)
        interaction = v(.interaction, d.interaction)
        autoExpandOnEvents = v(.autoExpandOnEvents, d.autoExpandOnEvents)
        autoCollapseSeconds = v(.autoCollapseSeconds, d.autoCollapseSeconds)
        showOnAllScreens = v(.showOnAllScreens, d.showOnAllScreens)
        mediaModule = v(.mediaModule, d.mediaModule)
        timerModule = v(.timerModule, d.timerModule)
        batteryModule = v(.batteryModule, d.batteryModule)
        weatherModule = v(.weatherModule, d.weatherModule)
        weatherCity = v(.weatherCity, d.weatherCity)
        fahrenheit = v(.fahrenheit, d.fahrenheit)
        motion = v(.motion, d.motion)
        reduceMotion = v(.reduceMotion, d.reduceMotion)
    }
}
