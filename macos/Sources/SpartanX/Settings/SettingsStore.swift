import Foundation
import Observation
import SpartanXCore

/// Observable wrapper around `AppSettings`, persisted as JSON in UserDefaults on every change.
@Observable
final class SettingsStore {
    private static let key = "SpartanX.settings"

    var settings: AppSettings {
        didSet {
            guard settings != oldValue else { return }
            save()
            if settings.language != oldValue.language {
                L10n.language = L10n.resolve(settings.language.rawValue)
            }
        }
    }

    init() {
        if let data = UserDefaults.standard.data(forKey: Self.key),
           let decoded = try? JSONDecoder().decode(AppSettings.self, from: data) {
            settings = decoded
        } else {
            settings = AppSettings()
        }
    }

    private func save() {
        if let data = try? JSONEncoder().encode(settings) {
            UserDefaults.standard.set(data, forKey: Self.key)
        }
    }

    func reset() {
        let firstRun = settings.firstRunCompleted
        settings = AppSettings()
        settings.firstRunCompleted = firstRun
    }
}
