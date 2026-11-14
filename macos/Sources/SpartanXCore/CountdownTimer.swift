import Foundation

/// A pausable countdown that derives its state from a clock instead of counting ticks.
public final class CountdownTimer {
    private let now: () -> Date
    private var endsAt = Date.distantPast
    private var remainingWhenPaused: TimeInterval = 0

    public private(set) var isRunning = false
    public private(set) var isPaused = false
    public private(set) var duration: TimeInterval = 0
    public var onCompleted: (() -> Void)?

    public init(now: @escaping () -> Date = Date.init) {
        self.now = now
    }

    public var isActive: Bool { isRunning || isPaused }

    public var remaining: TimeInterval {
        if isPaused { return remainingWhenPaused }
        if isRunning { return max(0, endsAt.timeIntervalSince(now())) }
        return 0
    }

    /// 0 at start, 1 when finished.
    public var progress: Double { duration > 0 ? 1 - remaining / duration : 0 }

    public func start(_ duration: TimeInterval) {
        self.duration = duration
        endsAt = now().addingTimeInterval(duration)
        isRunning = true
        isPaused = false
    }

    public func pause() {
        guard isRunning else { return }
        remainingWhenPaused = remaining
        isRunning = false
        isPaused = true
    }

    public func resume() {
        guard isPaused else { return }
        endsAt = now().addingTimeInterval(remainingWhenPaused)
        isPaused = false
        isRunning = true
    }

    public func cancel() {
        isRunning = false
        isPaused = false
        duration = 0
    }

    /// Checks for completion. Call periodically.
    public func tick() {
        if isRunning && remaining <= 0 {
            isRunning = false
            onCompleted?()
        }
    }

    public static func format(_ t: TimeInterval) -> String {
        let total = Int(t.rounded(.up))
        let h = total / 3600, m = (total % 3600) / 60, s = total % 60
        return h > 0 ? String(format: "%d:%02d:%02d", h, m, s) : String(format: "%d:%02d", m, s)
    }
}
