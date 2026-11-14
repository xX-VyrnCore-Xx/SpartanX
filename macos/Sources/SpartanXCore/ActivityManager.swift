import Foundation

/// A live activity. Transient ones (battery, timer finished) expire on their own.
public struct Activity: Equatable, Sendable {
    public var kind: ActivityKind
    public var expiresAt: Date?

    public init(kind: ActivityKind, expiresAt: Date? = nil) {
        self.kind = kind
        self.expiresAt = expiresAt
    }

    public var isTransient: Bool { expiresAt != nil }
}

/// Keeps one activity per kind and decides which one the island shows.
public final class ActivityManager {
    private var activities: [ActivityKind: Activity] = [:]
    private let now: () -> Date

    public private(set) var current = Activity(kind: .idle)

    /// Called whenever `current` changes.
    public var onChange: ((Activity) -> Void)?

    public init(now: @escaping () -> Date = Date.init) {
        self.now = now
    }

    public var all: [Activity] { Array(activities.values) }

    public func has(_ kind: ActivityKind) -> Bool { activities[kind] != nil }

    public func setPersistent(_ kind: ActivityKind) {
        activities[kind] = Activity(kind: kind)
        evaluate()
    }

    public func showTransient(_ kind: ActivityKind, for duration: TimeInterval) {
        activities[kind] = Activity(kind: kind, expiresAt: now().addingTimeInterval(duration))
        evaluate()
    }

    public func remove(_ kind: ActivityKind) {
        if activities.removeValue(forKey: kind) != nil {
            evaluate()
        }
    }

    /// Expires transient activities. Call periodically.
    public func tick() {
        let date = now()
        for (kind, activity) in activities where (activity.expiresAt.map { $0 <= date } ?? false) {
            activities.removeValue(forKey: kind)
        }

        evaluate()
    }

    private func evaluate() {
        let best = activities.values.max { $0.kind < $1.kind } ?? Activity(kind: .idle)
        if best != current {
            current = best
            onChange?(best)
        }
    }
}
