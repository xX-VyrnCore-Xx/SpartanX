import Foundation

/// How open the island is.
public enum IslandMode: String, Codable, Sendable {
    /// Hugs the notch (or a small pill on Macs without one).
    case compact
    /// Hover or a new event: one-line live activity.
    case peek
    /// Clicked open: full panel with controls.
    case expanded
}

/// What the island shows. Raw value is the priority: higher wins.
public enum ActivityKind: Int, Codable, CaseIterable, Comparable, Sendable {
    case idle = 0
    case weather = 10
    case media = 20
    case timer = 30
    case battery = 50
    case timerFinished = 80

    public static func < (lhs: ActivityKind, rhs: ActivityKind) -> Bool { lhs.rawValue < rhs.rawValue }
}

public struct IslandSize: Equatable, Sendable {
    public var width: Double
    public var height: Double

    public init(width: Double, height: Double) {
        self.width = width
        self.height = height
    }

    /// Bottom corner radius: the island hangs from the top edge like the notch.
    public var cornerRadius: Double { min(height / 2, 26) }
}

/// Target sizes for every (mode, activity) pair. Pure so it can be unit-tested.
public enum IslandLayout {
    /// Largest size the island can take; the host panel is sized to fit it.
    public static let maxSize = IslandSize(width: 560, height: 230)

    /// - Parameter notch: the physical notch size, or nil on Macs without one.
    public static func size(mode: IslandMode, activity: ActivityKind, notch: IslandSize?) -> IslandSize {
        let base = notch ?? IslandSize(width: 190, height: 32)
        switch mode {
        case .expanded:
            return IslandSize(width: 500, height: 210)
        case .peek:
            let extra: Double = activity == .idle ? 160 : 220
            return IslandSize(width: base.width + extra, height: base.height + 48)
        case .compact:
            // Live activities grow "ears" on both sides of the notch, like the iPhone.
            let ears: Double = activity == .idle ? 0 : 90
            return IslandSize(width: base.width + ears, height: base.height)
        }
    }
}
