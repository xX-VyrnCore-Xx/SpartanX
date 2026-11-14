import Foundation
import IOKit.ps

struct BatteryState: Equatable {
    var hasBattery: Bool
    var percent: Int
    var isCharging: Bool
    var isPluggedIn: Bool

    static let none = BatteryState(hasBattery: false, percent: 100, isCharging: false, isPluggedIn: true)

    var isLow: Bool { hasBattery && !isPluggedIn && percent <= 20 }

    var symbol: String {
        if isCharging { return "battery.100percent.bolt" }
        switch percent {
        case ..<13: return "battery.0percent"
        case ..<38: return "battery.25percent"
        case ..<63: return "battery.50percent"
        case ..<88: return "battery.75percent"
        default: return "battery.100percent"
        }
    }
}

/// Reads the internal battery through IOKit power-source APIs.
enum BatteryService {
    static func read() -> BatteryState {
        guard let info = IOPSCopyPowerSourcesInfo()?.takeRetainedValue(),
              let list = IOPSCopyPowerSourcesList(info)?.takeRetainedValue() as? [CFTypeRef] else {
            return .none
        }

        for source in list {
            guard let description = IOPSGetPowerSourceDescription(info, source)?.takeUnretainedValue() as? [String: Any],
                  (description["Type"] as? String) == "InternalBattery" else { continue }
            let current = description["Current Capacity"] as? Int ?? 100
            let max = description["Max Capacity"] as? Int ?? 100
            let percent = max > 0 ? Int((Double(current) / Double(max) * 100).rounded()) : current
            return BatteryState(
                hasBattery: true,
                percent: percent,
                isCharging: description["Is Charging"] as? Bool ?? false,
                isPluggedIn: (description["Power Source State"] as? String) == "AC Power")
        }

        return .none
    }
}
