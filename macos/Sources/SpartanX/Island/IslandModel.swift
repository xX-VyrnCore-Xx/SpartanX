import AppKit
import Observation
import SpartanXCore
import SwiftUI

/// State of the Dynamic Island. Services feed activities in; the view springs toward `size`
/// and blur-swaps content whenever `contentKey` changes.
@MainActor
@Observable
final class IslandModel {
    let store: SettingsStore
    private let nowPlaying = NowPlayingService()
    private let weatherClient = WeatherClient()
    @ObservationIgnored private let activities = ActivityManager()
    @ObservationIgnored private let countdown = CountdownTimer()
    @ObservationIgnored private var ticker: Timer?
    @ObservationIgnored private var autoPeek = false
    @ObservationIgnored private var hovered = false
    @ObservationIgnored private var lastWeatherFetch = Date.distantPast
    @ObservationIgnored private var collapseTask: Task<Void, Never>?

    // Geometry (set by IslandController for its screen).
    var notch: IslandSize?

    // State
    private(set) var mode: IslandMode = .compact
    private(set) var activity: ActivityKind = .idle

    // Data
    private(set) var media: NowPlaying?
    private(set) var artwork: NSImage?
    private(set) var battery = BatteryState.none
    private(set) var weather: WeatherSnapshot?
    private(set) var timerText = "0:00"
    private(set) var timerProgress = 0.0
    private(set) var timerPaused = false
    private(set) var now = Date()

    init(store: SettingsStore) {
        self.store = store
        activities.onChange = { [weak self] activity in self?.activityChanged(activity) }
        countdown.onCompleted = { [weak self] in self?.timerCompleted() }
        nowPlaying.onChange = { [weak self] in self?.mediaChanged() }
    }

    var settings: AppSettings { store.settings }

    var size: IslandSize { IslandLayout.size(mode: mode, activity: activity, notch: notch) }

    /// Identity of the content currently shown; changing it triggers the blur transition.
    var contentKey: String {
        switch mode {
        case .expanded:
            switch activity {
            case .timer, .timerFinished: return "expanded.timer"
            default: return media != nil && settings.mediaModule ? "expanded.media" : "expanded.dashboard"
            }
        case .peek: return "peek.\(activity)"
        case .compact: return "compact.\(activity)"
        }
    }

    var spring: Animation {
        if settings.reduceMotion { return .linear(duration: 0.01) }
        let s = settings.motion.spring
        return .spring(response: s.response, dampingFraction: s.damping)
    }

    // MARK: Lifecycle

    func start() {
        battery = BatteryService.read()
        if settings.mediaModule { nowPlaying.start() }
        let t = Timer(timeInterval: 0.5, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
        RunLoop.main.add(t, forMode: .common)
        ticker = t
        tick()
    }

    /// Re-applies settings that services depend on (called after Settings changes).
    func settingsChanged() {
        if settings.mediaModule { nowPlaying.start() } else { nowPlaying.stop(); media = nil; activities.remove(.media) }
        if !settings.timerModule { cancelTimer() }
        if !settings.batteryModule { activities.remove(.battery) }
        lastWeatherFetch = .distantPast
        tick()
    }

    @ObservationIgnored private var tickCount = 0

    private func tick() {
        now = Date()
        countdown.tick()
        if countdown.isActive {
            timerText = CountdownTimer.format(countdown.remaining)
            timerProgress = countdown.progress
            timerPaused = countdown.isPaused
        }

        tickCount += 1
        if tickCount % 6 == 0 { // every ~3 s
            let previous = battery
            battery = BatteryService.read()
            batteryChanged(from: previous)
        }

        activities.tick()

        if now.timeIntervalSince(lastWeatherFetch) > 30 * 60 {
            lastWeatherFetch = now
            Task { await refreshWeather() }
        }
    }

    // MARK: Pointer

    func setHovered(_ inside: Bool) {
        guard inside != hovered else { return }
        hovered = inside
        if inside {
            collapseTask?.cancel()
            if settings.interaction == .hoverToPeek && mode == .compact { mode = .peek }
        } else if mode != .compact {
            autoPeek = false
            mode = .compact
        }
    }

    func toggle() {
        mode = mode == .expanded ? .compact : .expanded
    }

    // MARK: Media

    func playPause() { nowPlaying.playPause() }
    func next() { nowPlaying.next() }
    func previous() { nowPlaying.previous() }

    private func mediaChanged() {
        let trackChanged = media?.trackID != nowPlaying.current?.trackID
        media = nowPlaying.current
        artwork = nowPlaying.artwork
        guard settings.mediaModule, let media else {
            activities.remove(.media)
            return
        }

        activities.setPersistent(.media)
        if trackChanged && media.isPlaying { peekForEvent() }
    }

    // MARK: Timer

    func startTimer(minutes: Double) {
        guard settings.timerModule else { return }
        countdown.start(minutes * 60)
        timerText = CountdownTimer.format(countdown.remaining)
        timerProgress = 0
        timerPaused = false
        activities.remove(.timerFinished)
        activities.setPersistent(.timer)
    }

    func toggleTimerPause() {
        if countdown.isPaused { countdown.resume() } else { countdown.pause() }
        timerPaused = countdown.isPaused
    }

    func cancelTimer() {
        countdown.cancel()
        activities.remove(.timer)
        activities.remove(.timerFinished)
    }

    private func timerCompleted() {
        activities.remove(.timer)
        activities.showTransient(.timerFinished, for: 8)
        NSSound(named: NSSound.Name("Glass"))?.play()
        peekForEvent()
    }

    // MARK: Battery

    private func batteryChanged(from previous: BatteryState) {
        guard settings.batteryModule, battery.hasBattery else { return }
        let plugged = battery.isPluggedIn != previous.isPluggedIn
        let becameLow = battery.isLow && !previous.isLow
        if plugged || becameLow {
            activities.showTransient(.battery, for: 3)
            peekForEvent()
        }
    }

    // MARK: Weather

    func refreshWeather() async {
        let city = settings.weatherCity.trimmingCharacters(in: .whitespaces)
        guard settings.weatherModule, !city.isEmpty else {
            weather = nil
            activities.remove(.weather)
            return
        }

        let result = await weatherClient.current(city: city, fahrenheit: settings.fahrenheit)
        weather = result
        if result == nil { activities.remove(.weather) } else { activities.setPersistent(.weather) }
    }

    // MARK: Layout

    private func peekForEvent() {
        guard settings.autoExpandOnEvents, mode == .compact else { return }
        autoPeek = true
        mode = .peek
        collapseTask?.cancel()
        let delay = max(1, settings.autoCollapseSeconds)
        collapseTask = Task { [weak self] in
            try? await Task.sleep(nanoseconds: UInt64(delay * 1_000_000_000))
            guard let self, !Task.isCancelled, self.autoPeek, !self.hovered, self.mode == .peek else { return }
            self.autoPeek = false
            self.mode = .compact
        }
    }

    private func activityChanged(_ activity: Activity) {
        self.activity = activity.kind
    }
}
