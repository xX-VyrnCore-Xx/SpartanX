import AppKit
import Foundation

/// Snapshot of what Music or Spotify is playing.
struct NowPlaying: Equatable {
    enum Source: String { case music, spotify }

    var source: Source
    var title: String
    var artist: String
    var album: String
    var isPlaying: Bool
    var position: Double
    var duration: Double

    var progress: Double { duration > 0 ? min(max(position / duration, 0), 1) : 0 }
    var trackID: String { "\(source.rawValue)|\(title)|\(artist)|\(album)" }
}

/// Reads "now playing" from Music and Spotify through AppleScript.
///
/// Since macOS 15.4 the private MediaRemote framework no longer serves third-party apps, so
/// Apple Events are the supported route. macOS asks the user once per player (Automation).
/// Players are only queried while they are running, so SpartanX never launches them.
@MainActor
final class NowPlayingService {
    private static let bundleIDs: [(NowPlaying.Source, String)] = [
        (.spotify, "com.spotify.client"),
        (.music, "com.apple.Music"),
    ]

    private var timer: Timer?
    private var lastTrackID: String?

    private(set) var current: NowPlaying?
    private(set) var artwork: NSImage?

    /// Called on the main thread whenever `current` or `artwork` changes.
    var onChange: (() -> Void)?

    func start() {
        guard timer == nil else { return }
        poll()
        let t = Timer(timeInterval: 1, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.poll() }
        }
        RunLoop.main.add(t, forMode: .common)
        timer = t
    }

    func stop() {
        timer?.invalidate()
        timer = nil
    }

    // MARK: Controls

    func playPause() { send("playpause") }
    func next() { send("next track") }
    func previous() { send("previous track") }

    private func send(_ command: String) {
        guard let source = current?.source ?? runningPlayers().first else { return }
        run("tell application \"\(appName(source))\" to \(command)")
        Task { @MainActor [weak self] in
            try? await Task.sleep(nanoseconds: 250_000_000)
            self?.poll()
        }
    }

    // MARK: Polling

    private func runningPlayers() -> [NowPlaying.Source] {
        Self.bundleIDs.compactMap { source, id in
            NSRunningApplication.runningApplications(withBundleIdentifier: id).isEmpty ? nil : source
        }
    }

    private func appName(_ source: NowPlaying.Source) -> String {
        source == .spotify ? "Spotify" : "Music"
    }

    private func poll() {
        // Prefer whichever player is actually playing; otherwise the first paused one.
        var best: NowPlaying?
        for source in runningPlayers() {
            guard let snapshot = query(source) else { continue }
            if snapshot.isPlaying { best = snapshot; break }
            if best == nil { best = snapshot }
        }

        let trackChanged = best?.trackID != lastTrackID
        let changed = best != current
        current = best
        if trackChanged {
            lastTrackID = best?.trackID
            artwork = nil
            if let best { loadArtwork(for: best) }
        }

        if changed || trackChanged {
            onChange?()
        }
    }

    private func query(_ source: NowPlaying.Source) -> NowPlaying? {
        let name = appName(source)
        let artworkField = source == .spotify ? " & \"||\" & (artwork url of t)" : ""
        let script = """
        tell application "\(name)"
            if player state is stopped then return "STOPPED"
            set t to current track
            return (player state as string) & "||" & (name of t) & "||" & (artist of t) & "||" & (album of t) & "||" & (duration of t as string) & "||" & (player position as string)\(artworkField)
        end tell
        """
        guard let output = run(script)?.stringValue, output != "STOPPED" else { return nil }
        let parts = output.components(separatedBy: "||")
        guard parts.count >= 6 else { return nil }

        // AppleScript formats reals with the user's decimal separator.
        func number(_ s: String) -> Double { Double(s.replacingOccurrences(of: ",", with: ".")) ?? 0 }
        var duration = number(parts[4])
        if source == .spotify { duration /= 1000 } // Spotify reports milliseconds.
        if source == .spotify, parts.count >= 7 { spotifyArtworkURL = URL(string: parts[6]) }

        return NowPlaying(
            source: source,
            title: parts[1],
            artist: parts[2],
            album: parts[3],
            isPlaying: parts[0] == "playing",
            position: number(parts[5]),
            duration: duration)
    }

    private var spotifyArtworkURL: URL?

    private func loadArtwork(for track: NowPlaying) {
        switch track.source {
        case .music:
            if let data = run("tell application \"Music\" to get raw data of artwork 1 of current track")?.data {
                artwork = NSImage(data: data)
            }
        case .spotify:
            guard let url = spotifyArtworkURL else { return }
            let id = track.trackID
            Task { [weak self] in
                guard let result = try? await URLSession.shared.data(from: url), let image = NSImage(data: result.0) else { return }
                await MainActor.run {
                    guard let self, self.lastTrackID == id else { return }
                    self.artwork = image
                    self.onChange?()
                }
            }
        }
    }

    @discardableResult
    private func run(_ source: String) -> NSAppleEventDescriptor? {
        var error: NSDictionary?
        return NSAppleScript(source: source)?.executeAndReturnError(&error)
    }
}
