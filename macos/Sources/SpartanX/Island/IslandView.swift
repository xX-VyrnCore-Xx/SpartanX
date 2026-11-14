import SpartanXCore
import SwiftUI

/// Notch-like silhouette: flat top that flares into the screen edge, rounded bottom corners.
struct IslandShape: Shape {
    var bottomRadius: Double
    var topFlare: Double = 6

    var animatableData: Double {
        get { bottomRadius }
        set { bottomRadius = newValue }
    }

    func path(in rect: CGRect) -> Path {
        let w = rect.width, h = rect.height
        let f = min(topFlare, w / 4)
        let r = min(bottomRadius, (w - 2 * f) / 2, h - f)
        var p = Path()
        p.move(to: CGPoint(x: 0, y: 0))
        p.addQuadCurve(to: CGPoint(x: f, y: f), control: CGPoint(x: f, y: 0))
        p.addLine(to: CGPoint(x: f, y: h - r))
        p.addQuadCurve(to: CGPoint(x: f + r, y: h), control: CGPoint(x: f, y: h))
        p.addLine(to: CGPoint(x: w - f - r, y: h))
        p.addQuadCurve(to: CGPoint(x: w - f, y: h - r), control: CGPoint(x: w - f, y: h))
        p.addLine(to: CGPoint(x: w - f, y: f))
        p.addQuadCurve(to: CGPoint(x: w, y: 0), control: CGPoint(x: w - f, y: 0))
        p.closeSubpath()
        return p
    }
}

/// The Dynamic Island. Size changes ride a SwiftUI spring; content swaps with a blur transition.
struct IslandView: View {
    let model: IslandModel

    var body: some View {
        let size = model.size
        let flare = 6.0
        ZStack(alignment: .top) {
            IslandShape(bottomRadius: size.cornerRadius, topFlare: flare)
                .fill(Color.black)
                .frame(width: size.width + flare * 2, height: size.height)
                .shadow(color: .black.opacity(model.mode == .compact ? 0 : 0.35), radius: 14, y: 6)

            IslandContent(model: model)
                .id(model.contentKey)
                .transition(AnyTransition(.blurReplace).combined(with: .scale(scale: 0.94, anchor: .top)))
                .frame(width: size.width, height: size.height, alignment: .top)
                .clipped()
        }
        .frame(width: size.width + flare * 2, height: size.height, alignment: .top)
        .contentShape(Rectangle())
        .onTapGesture { model.toggle() }
        .animation(model.spring, value: size)
        .animation(model.spring, value: model.contentKey)
        .environment(\.colorScheme, .dark)
    }
}

/// Picks the content for the current mode and activity.
struct IslandContent: View {
    let model: IslandModel

    private var topInset: Double { model.notch?.height ?? 0 }
    private var notchWidth: Double { model.notch?.width ?? 0 }

    var body: some View {
        switch model.mode {
        case .compact: compact
        case .peek: peek.padding(.top, max(topInset, 6)).padding(.horizontal, 18)
        case .expanded: expanded.padding(.top, max(topInset, 10)).padding(.horizontal, 22).padding(.bottom, 14)
        }
    }

    // MARK: Compact: "ears" either side of the notch

    @ViewBuilder private var compact: some View {
        HStack(spacing: 0) {
            leftEar.frame(maxWidth: .infinity, alignment: .leading)
            Spacer(minLength: notchWidth)
            rightEar.frame(maxWidth: .infinity, alignment: .trailing)
        }
        .padding(.horizontal, 12)
        .frame(maxHeight: .infinity)
    }

    @ViewBuilder private var leftEar: some View {
        switch model.activity {
        case .media: Artwork(image: model.artwork, size: 20, radius: 5)
        case .timer, .timerFinished: Image(systemName: "timer").foregroundStyle(.orange)
        case .battery: Image(systemName: model.battery.symbol).foregroundStyle(model.battery.isLow ? Color.red : Color.green)
        case .weather: Image(systemName: model.weather?.symbol ?? "cloud.fill").symbolRenderingMode(.multicolor)
        case .idle: EmptyView()
        }
    }

    @ViewBuilder private var rightEar: some View {
        switch model.activity {
        case .media: AudioBars(isPlaying: model.media?.isPlaying == true, maxHeight: 14)
        case .timer: Text(model.timerText).font(.system(size: 13, weight: .semibold).monospacedDigit()).foregroundStyle(.orange)
        case .timerFinished: Text(L10n.get("island.timerDone")).font(.system(size: 11, weight: .semibold)).foregroundStyle(.orange)
        case .battery: Text("\(model.battery.percent)%").font(.system(size: 12, weight: .semibold)).foregroundStyle(.white)
        case .weather: Text(model.weather?.temperatureText ?? "").font(.system(size: 13, weight: .semibold)).foregroundStyle(.white)
        case .idle: EmptyView()
        }
    }

    // MARK: Peek: one rich line under the notch

    @ViewBuilder private var peek: some View {
        switch model.activity {
        case .media:
            HStack(spacing: 12) {
                Artwork(image: model.artwork, size: 36, radius: 8)
                VStack(alignment: .leading, spacing: 1) {
                    Text(model.media?.title ?? "").font(.system(size: 13, weight: .semibold)).lineLimit(1)
                    Text(model.media?.artist ?? "").font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(1)
                }
                Spacer(minLength: 8)
                AudioBars(isPlaying: model.media?.isPlaying == true, maxHeight: 18)
            }
        case .timer, .timerFinished:
            HStack {
                Label(L10n.get(model.activity == .timer ? "island.timer" : "island.timerDone"), systemImage: "timer")
                    .font(.system(size: 13)).foregroundStyle(.orange)
                Spacer()
                Text(model.timerText).font(.system(size: 24, weight: .semibold).monospacedDigit()).foregroundStyle(.orange)
            }
        case .battery:
            HStack {
                Text(batteryText).font(.system(size: 14, weight: .semibold))
                Spacer()
                Image(systemName: model.battery.symbol).font(.system(size: 22))
                    .foregroundStyle(model.battery.isLow ? Color.red : Color.green)
            }
        case .weather, .idle:
            HStack(spacing: 10) {
                Text(model.now, format: .dateTime.hour().minute()).font(.system(size: 15, weight: .semibold))
                Text(model.now, format: .dateTime.weekday(.abbreviated).day().month(.abbreviated))
                    .font(.system(size: 13)).foregroundStyle(.secondary)
                Spacer()
                if let w = model.weather {
                    Image(systemName: w.symbol).symbolRenderingMode(.multicolor)
                    Text(w.temperatureText).font(.system(size: 13))
                }
            }
        }
    }

    private var batteryText: String {
        let b = model.battery
        if b.isCharging { return "\(L10n.get("island.charging")) · \(b.percent)%" }
        if b.isPluggedIn { return "\(L10n.get("island.pluggedIn")) · \(b.percent)%" }
        if b.isLow { return "\(L10n.get("island.lowBattery")) · \(b.percent)%" }
        return "\(b.percent)%"
    }

    // MARK: Expanded

    @ViewBuilder private var expanded: some View {
        switch model.contentKey {
        case "expanded.timer": ExpandedTimer(model: model)
        case "expanded.media": ExpandedMedia(model: model)
        default: Dashboard(model: model)
        }
    }
}

struct ExpandedMedia: View {
    let model: IslandModel

    var body: some View {
        VStack(spacing: 12) {
            HStack(spacing: 14) {
                Artwork(image: model.artwork, size: 64, radius: 14)
                VStack(alignment: .leading, spacing: 2) {
                    Text(model.media?.title ?? L10n.get("island.notPlaying")).font(.system(size: 16, weight: .semibold)).lineLimit(1)
                    Text(model.media?.artist ?? "").font(.system(size: 13)).foregroundStyle(.secondary).lineLimit(1)
                }
                Spacer()
                AudioBars(isPlaying: model.media?.isPlaying == true, maxHeight: 22)
            }
            ProgressView(value: model.media?.progress ?? 0).tint(.white)
            HStack(spacing: 36) {
                ControlButton(symbol: "backward.fill", size: 18) { model.previous() }
                ControlButton(symbol: model.media?.isPlaying == true ? "pause.fill" : "play.fill", size: 26) { model.playPause() }
                ControlButton(symbol: "forward.fill", size: 18) { model.next() }
            }
        }
    }
}

struct ExpandedTimer: View {
    let model: IslandModel

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text(L10n.get("island.timer")).font(.system(size: 14)).foregroundStyle(.orange)
                Text(model.timerText).font(.system(size: 48, weight: .semibold).monospacedDigit()).foregroundStyle(.orange)
                ProgressView(value: model.timerProgress).tint(.orange).frame(width: 200)
            }
            Spacer()
            HStack(spacing: 12) {
                CircleButton(symbol: model.timerPaused ? "play.fill" : "pause.fill", tint: .orange) { model.toggleTimerPause() }
                CircleButton(symbol: "xmark", tint: .white) { model.cancelTimer() }
            }
        }
        .frame(maxHeight: .infinity)
    }
}

struct Dashboard: View {
    let model: IslandModel

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(model.now, format: .dateTime.hour().minute()).font(.system(size: 34, weight: .semibold))
                    Text(model.now, format: .dateTime.weekday(.wide).day().month(.wide)).font(.system(size: 13)).foregroundStyle(.secondary)
                }
                Spacer()
                if let w = model.weather {
                    VStack(alignment: .trailing, spacing: 2) {
                        HStack(spacing: 6) {
                            Image(systemName: w.symbol).symbolRenderingMode(.multicolor).font(.system(size: 22))
                            Text(w.temperatureText).font(.system(size: 24, weight: .light))
                        }
                        Text("\(w.city) · \(L10n.get(w.descriptionKey))").font(.system(size: 12)).foregroundStyle(.secondary)
                    }
                }
            }
            Spacer(minLength: 0)
            if model.settings.timerModule {
                VStack(alignment: .leading, spacing: 6) {
                    Text(L10n.get("island.quickTimer")).font(.system(size: 12)).foregroundStyle(.secondary)
                    HStack(spacing: 8) {
                        ForEach([1.0, 5, 10, 25], id: \.self) { m in
                            Button("\(Int(m)) min") { model.startTimer(minutes: m) }
                                .buttonStyle(.bordered)
                                .controlSize(.small)
                        }
                    }
                }
            }
        }
    }
}

// MARK: Small building blocks

struct Artwork: View {
    let image: NSImage?
    let size: Double
    let radius: Double

    var body: some View {
        ZStack {
            RoundedRectangle(cornerRadius: radius).fill(Color.white.opacity(0.12))
            if let image {
                Image(nsImage: image).resizable().aspectRatio(contentMode: .fill)
            } else {
                Image(systemName: "music.note").font(.system(size: size * 0.45)).foregroundStyle(.secondary)
            }
        }
        .frame(width: size, height: size)
        .clipShape(RoundedRectangle(cornerRadius: radius))
    }
}

struct ControlButton: View {
    let symbol: String
    let size: Double
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Image(systemName: symbol).font(.system(size: size)).frame(width: 44, height: 44).contentShape(Circle())
        }
        .buttonStyle(.plain)
        .foregroundStyle(.white)
    }
}

struct CircleButton: View {
    let symbol: String
    let tint: Color
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Image(systemName: symbol).font(.system(size: 18, weight: .semibold))
                .frame(width: 50, height: 50)
                .background(Circle().fill(tint.opacity(0.22)))
        }
        .buttonStyle(.plain)
        .foregroundStyle(tint)
    }
}

/// "Now playing" equaliser: bars bounce with springs while music plays.
struct AudioBars: View {
    let isPlaying: Bool
    let maxHeight: Double

    var body: some View {
        TimelineView(.animation(minimumInterval: 0.12, paused: !isPlaying)) { context in
            let t = context.date.timeIntervalSinceReferenceDate
            HStack(spacing: 2.5) {
                ForEach(0..<4, id: \.self) { i in
                    Capsule()
                        .fill(Color.accentColor)
                        .frame(width: 3, height: height(i, t))
                        .animation(.spring(response: 0.25, dampingFraction: 0.6), value: height(i, t))
                }
            }
            .frame(height: maxHeight)
        }
    }

    private func height(_ i: Int, _ t: Double) -> Double {
        guard isPlaying else { return 3 }
        let weights = [0.65, 1.0, 0.8, 0.55]
        let wave = (sin(t * (5.0 + Double(i) * 1.7) + Double(i)) + 1) / 2
        return max(3, maxHeight * weights[i] * (0.35 + 0.65 * wave))
    }
}
