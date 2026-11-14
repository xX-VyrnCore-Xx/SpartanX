import AppKit
import SpartanXCore
import SwiftUI

/// Hosts the Setup Assistant in a transparent, full-size-content window.
@MainActor
final class WelcomeWindowController: NSWindowController, NSWindowDelegate {
    private var onFinish: (() -> Void)?

    init(store: SettingsStore, island: IslandModel, onFinish: @escaping () -> Void) {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 760, height: 540),
            styleMask: [.titled, .closable, .fullSizeContentView],
            backing: .buffered, defer: false)
        window.titlebarAppearsTransparent = true
        window.titleVisibility = .hidden
        window.isMovableByWindowBackground = true
        window.isReleasedWhenClosed = false
        super.init(window: window)
        self.onFinish = onFinish
        window.delegate = self
        window.contentView = NSHostingView(rootView: WelcomeView(store: store, island: island) { [weak self] in
            self?.close()
        })
        window.center()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) { fatalError() }

    func present() {
        NSApp.activate(ignoringOtherApps: true)
        window?.makeKeyAndOrderFront(nil)
    }

    func windowWillClose(_ notification: Notification) {
        // Closing early still counts: SpartanX runs with defaults.
        onFinish?()
        onFinish = nil
    }
}

/// macOS Setup Assistant–style onboarding: "hello" in many languages, then a few focused pages.
struct WelcomeView: View {
    let store: SettingsStore
    let island: IslandModel
    let close: () -> Void

    enum Step: Int, CaseIterable { case hello, island, privacy, done }

    @State private var step: Step = .hello
    @State private var forward = true

    var body: some View {
        ZStack {
            VisualEffect().ignoresSafeArea()
            Aurora().opacity(step == .hello || step == .done ? 1 : 0.35).ignoresSafeArea()

            Group {
                switch step {
                case .hello: HelloPage { go(.island) }
                case .island: IslandPage(store: store)
                case .privacy: PrivacyPage(store: store)
                case .done: DonePage()
                }
            }
            .id(step)
            .transition(.asymmetric(
                insertion: .move(edge: forward ? .trailing : .leading).combined(with: .opacity),
                removal: .move(edge: forward ? .leading : .trailing).combined(with: .opacity)))
            .padding(.bottom, step == .hello ? 0 : 64)

            if step != .hello {
                VStack {
                    Spacer()
                    HStack {
                        if step.rawValue > Step.island.rawValue {
                            Button(L10n.get("welcome.back")) { go(Step(rawValue: step.rawValue - 1)!) }
                                .controlSize(.large)
                        }
                        Spacer()
                        Dots(count: Step.allCases.count - 1, index: step.rawValue - 1)
                        Spacer()
                        Button(step == .done ? L10n.get("welcome.start") : L10n.get("welcome.continue")) {
                            if step == .done { finish() } else { go(Step(rawValue: step.rawValue + 1)!) }
                        }
                        .buttonStyle(.borderedProminent)
                        .controlSize(.large)
                        .keyboardShortcut(.defaultAction)
                    }
                    .padding(.horizontal, 36)
                    .padding(.bottom, 24)
                }
            }
        }
        .frame(width: 760, height: 540)
    }

    private func go(_ next: Step) {
        forward = next.rawValue > step.rawValue
        withAnimation(.spring(response: 0.5, dampingFraction: 0.86)) { step = next }
    }

    private func finish() {
        store.settings.firstRunCompleted = true
        LaunchAtLogin.set(store.settings.launchAtLogin)
        close()
    }
}

private struct HelloPage: View {
    let start: () -> Void
    @State private var index = 0
    @State private var appeared = false

    var body: some View {
        VStack(spacing: 44) {
            Text(L10n.greetings[index])
                .font(.system(size: 96, weight: .light, design: .rounded))
                .id(index)
                .transition(AnyTransition(.blurReplace))
                .frame(height: 140)
            Button(action: start) {
                Image(systemName: "arrow.right")
                    .font(.system(size: 22, weight: .semibold))
                    .frame(width: 60, height: 60)
                    .background(Circle().fill(Color.accentColor))
                    .foregroundStyle(.white)
            }
            .buttonStyle(.plain)
            .keyboardShortcut(.defaultAction)
            .scaleEffect(appeared ? 1 : 0.6)
            .opacity(appeared ? 1 : 0)
            Text(L10n.get("welcome.getStarted")).foregroundStyle(.secondary)
        }
        .task {
            withAnimation(.spring(response: 0.6, dampingFraction: 0.7).delay(0.6)) { appeared = true }
            while !Task.isCancelled {
                try? await Task.sleep(nanoseconds: 2_200_000_000)
                withAnimation(.easeInOut(duration: 0.6)) { index = (index + 1) % L10n.greetings.count }
            }
        }
    }
}

private struct IslandPage: View {
    @Bindable var store: SettingsStore
    @State private var demoOpen = false

    var body: some View {
        HStack(spacing: 36) {
            ZStack(alignment: .top) {
                RoundedRectangle(cornerRadius: 16).fill(LinearGradient(
                    colors: [Color(red: 0.06, green: 0.16, blue: 0.26), Color(red: 0.37, green: 0.36, blue: 0.90)],
                    startPoint: .topLeading, endPoint: .bottomTrailing))
                IslandShape(bottomRadius: demoOpen ? 20 : 10)
                    .fill(Color.black)
                    .frame(width: demoOpen ? 240 : 100, height: demoOpen ? 64 : 20)
            }
            .frame(width: 320, height: 260)
            .task {
                while !Task.isCancelled {
                    try? await Task.sleep(nanoseconds: 1_800_000_000)
                    withAnimation(.spring(response: 0.5, dampingFraction: 0.62)) { demoOpen.toggle() }
                }
            }

            VStack(alignment: .leading, spacing: 12) {
                Text(L10n.get("welcome.island.title")).font(.largeTitle.bold())
                Text(L10n.get("welcome.island.subtitle")).foregroundStyle(.secondary)
                Toggle(L10n.get("settings.media"), isOn: $store.settings.mediaModule)
                Toggle(L10n.get("settings.timer"), isOn: $store.settings.timerModule)
                Toggle(L10n.get("settings.battery"), isOn: $store.settings.batteryModule)
                Toggle(L10n.get("settings.weather"), isOn: $store.settings.weatherModule)
                if store.settings.weatherModule {
                    TextField(L10n.get("settings.city"), text: $store.settings.weatherCity, prompt: Text(L10n.get("settings.cityPlaceholder")))
                        .textFieldStyle(.roundedBorder)
                }
            }
            .toggleStyle(.checkbox)
            .frame(width: 300)
        }
        .padding(40)
    }
}

private struct PrivacyPage: View {
    @Bindable var store: SettingsStore

    var body: some View {
        VStack(spacing: 18) {
            Image(systemName: "lock.shield.fill").font(.system(size: 44)).foregroundStyle(Color.accentColor)
            Text(L10n.get("welcome.privacy.title")).font(.largeTitle.bold())
            Text(L10n.get("welcome.privacy.subtitle")).foregroundStyle(.secondary)
            Form {
                Toggle(L10n.get("settings.launchAtLogin"), isOn: $store.settings.launchAtLogin)
                Toggle(L10n.get("settings.updates"), isOn: $store.settings.checkForUpdates)
                Text(L10n.get("welcome.privacy.automation")).font(.callout).foregroundStyle(.secondary)
            }
            .formStyle(.grouped)
            .frame(width: 480, height: 170)
            .scrollDisabled(true)
            Text(L10n.get("welcome.privacy.noTelemetry")).font(.caption).foregroundStyle(.secondary)
                .multilineTextAlignment(.center).frame(width: 460)
        }
        .padding(40)
    }
}

private struct DonePage: View {
    @State private var appeared = false

    var body: some View {
        VStack(spacing: 18) {
            Image(nsImage: NSApp.applicationIconImage).resizable().frame(width: 120, height: 120)
                .scaleEffect(appeared ? 1 : 0.5).opacity(appeared ? 1 : 0)
            Text(L10n.get("welcome.done.title")).font(.largeTitle.bold())
            Text(L10n.get("welcome.done.subtitle")).foregroundStyle(.secondary)
                .multilineTextAlignment(.center).frame(width: 440)
        }
        .onAppear { withAnimation(.spring(response: 0.6, dampingFraction: 0.6).delay(0.1)) { appeared = true } }
    }
}

private struct Dots: View {
    let count: Int
    let index: Int

    var body: some View {
        HStack(spacing: 7) {
            ForEach(0..<count, id: \.self) { i in
                Capsule()
                    .fill(i == index ? Color.accentColor : Color.secondary.opacity(0.35))
                    .frame(width: i == index ? 22 : 7, height: 7)
            }
        }
        .animation(.spring(response: 0.4, dampingFraction: 0.8), value: index)
    }
}

/// Slowly drifting colour field behind "hello".
private struct Aurora: View {
    var body: some View {
        TimelineView(.animation) { context in
            let t = context.date.timeIntervalSinceReferenceDate
            ZStack {
                blob(Color(red: 0.84, green: 0.19, blue: 0.19), x: -180 + 90 * sin(t / 4.5), y: -150 + 60 * cos(t / 5.5), size: 520)
                blob(Color(red: 0.93, green: 0.71, blue: 0.38), x: 200 + 80 * cos(t / 6), y: 160 + 50 * sin(t / 4), size: 480)
                blob(Color(red: 0.37, green: 0.36, blue: 0.90), x: 120 + 70 * sin(t / 7), y: -180 + 70 * cos(t / 6.5), size: 440)
            }
            .blur(radius: 60)
        }
    }

    private func blob(_ color: Color, x: Double, y: Double, size: Double) -> some View {
        Circle().fill(color.opacity(0.55)).frame(width: size, height: size).offset(x: x, y: y)
    }
}

/// NSVisualEffectView: the native translucent window material.
struct VisualEffect: NSViewRepresentable {
    func makeNSView(context: Context) -> NSVisualEffectView {
        let v = NSVisualEffectView()
        v.material = .underWindowBackground
        v.blendingMode = .behindWindow
        v.state = .active
        return v
    }

    func updateNSView(_ nsView: NSVisualEffectView, context: Context) {}
}
