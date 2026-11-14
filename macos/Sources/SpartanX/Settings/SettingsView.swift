import SpartanXCore
import SwiftUI

/// macOS System Settings–style preferences: sidebar + grouped forms.
struct SettingsView: View {
    @Environment(SettingsStore.self) private var store
    @Environment(IslandModel.self) private var island
    @State private var section: Section? = .general

    enum Section: String, CaseIterable, Identifiable {
        case general, island, animations, about
        var id: String { rawValue }

        var title: String { L10n.get("settings.\(rawValue)") }

        var symbol: String {
            switch self {
            case .general: return "gearshape.fill"
            case .island: return "capsule.fill"
            case .animations: return "wand.and.stars"
            case .about: return "info.circle.fill"
            }
        }

        var tint: Color {
            switch self {
            case .general: return .gray
            case .island: return .black
            case .animations: return .pink
            case .about: return Color(red: 0.70, green: 0.13, blue: 0.16)
            }
        }
    }

    var body: some View {
        NavigationSplitView {
            List(Section.allCases, selection: $section) { s in
                Label {
                    Text(s.title)
                } icon: {
                    Image(systemName: s.symbol)
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(.white)
                        .frame(width: 22, height: 22)
                        .background(RoundedRectangle(cornerRadius: 6).fill(s.tint.gradient))
                }
                .tag(s)
            }
            .navigationSplitViewColumnWidth(200)
        } detail: {
            Group {
                switch section ?? .general {
                case .general: GeneralPane()
                case .island: IslandPane()
                case .animations: AnimationsPane()
                case .about: AboutPane()
                }
            }
            .formStyle(.grouped)
            .navigationTitle((section ?? .general).title)
        }
        .frame(minWidth: 700, minHeight: 480)
        .onChange(of: store.settings) { island.settingsChanged() }
    }
}

private struct GeneralPane: View {
    @Environment(SettingsStore.self) private var store
    @State private var loginFailed = false
    @State private var updateStatus: String?
    @State private var update: ReleaseUpdate?

    var body: some View {
        @Bindable var store = store
        Form {
            Section {
                Picker(L10n.get("settings.language"), selection: $store.settings.language) {
                    Text(L10n.get("settings.language.system")).tag(AppSettings.Language.system)
                    Text("English").tag(AppSettings.Language.en)
                    Text("Italiano").tag(AppSettings.Language.it)
                }
                Toggle(L10n.get("settings.launchAtLogin"), isOn: $store.settings.launchAtLogin)
                    .onChange(of: store.settings.launchAtLogin) { _, enabled in
                        loginFailed = !LaunchAtLogin.set(enabled)
                    }
                if loginFailed {
                    Text(L10n.get("settings.launchAtLogin.failed")).font(.caption).foregroundStyle(.orange)
                }
            } footer: {
                Text(L10n.get("settings.restartForLanguage")).font(.caption).foregroundStyle(.secondary)
            }

            Section {
                Toggle(L10n.get("settings.updates"), isOn: $store.settings.checkForUpdates)
                Toggle(L10n.get("settings.prereleases"), isOn: $store.settings.includePrereleases)
                HStack {
                    Text(updateStatus ?? "")
                        .foregroundStyle(.secondary)
                    Spacer()
                    if let update {
                        Button(L10n.get("settings.download")) {
                            NSWorkspace.shared.open(update.dmgURL ?? update.pageURL)
                        }
                        .buttonStyle(.borderedProminent)
                    }
                    Button(L10n.get("settings.checkNow")) { Task { await checkNow() } }
                }
            }

            Section {
                Button(L10n.get("settings.runWelcome")) { AppDelegate.shared?.showWelcome() }
            }
        }
    }

    private func checkNow() async {
        updateStatus = L10n.get("settings.checking")
        update = await AppDelegate.shared?.checkForUpdates()
        if let update {
            updateStatus = L10n.format("settings.updateAvailable", update.version.description)
        } else {
            updateStatus = L10n.get("settings.upToDate")
        }
    }
}

private struct IslandPane: View {
    @Environment(SettingsStore.self) private var store

    var body: some View {
        @Bindable var store = store
        Form {
            Section {
                Picker(L10n.get("settings.interaction"), selection: $store.settings.interaction) {
                    Text(L10n.get("settings.interaction.hoverToPeek")).tag(AppSettings.Interaction.hoverToPeek)
                    Text(L10n.get("settings.interaction.clickOnly")).tag(AppSettings.Interaction.clickOnly)
                }
                Toggle(L10n.get("settings.autoExpand"), isOn: $store.settings.autoExpandOnEvents)
                LabeledContent(L10n.get("settings.autoCollapse")) {
                    HStack {
                        Slider(value: $store.settings.autoCollapseSeconds, in: 1...12, step: 0.5).frame(width: 180)
                        Text("\(store.settings.autoCollapseSeconds, specifier: "%.1f") s").monospacedDigit().frame(width: 44)
                    }
                }
            }

            Section(L10n.get("settings.modules")) {
                Toggle(L10n.get("settings.media"), isOn: $store.settings.mediaModule)
                Toggle(L10n.get("settings.timer"), isOn: $store.settings.timerModule)
                Toggle(L10n.get("settings.battery"), isOn: $store.settings.batteryModule)
                Toggle(L10n.get("settings.weather"), isOn: $store.settings.weatherModule)
                if store.settings.weatherModule {
                    TextField(L10n.get("settings.city"), text: $store.settings.weatherCity, prompt: Text(L10n.get("settings.cityPlaceholder")))
                    Toggle(L10n.get("settings.fahrenheit"), isOn: $store.settings.fahrenheit)
                }
            }
        }
    }
}

private struct AnimationsPane: View {
    @Environment(SettingsStore.self) private var store
    @State private var expanded = false

    var body: some View {
        @Bindable var store = store
        let s = store.settings.motion.spring
        Form {
            Section {
                ZStack(alignment: .top) {
                    LinearGradient(colors: [Color(red: 0.17, green: 0.10, blue: 0.24), Color(red: 0.70, green: 0.13, blue: 0.16)],
                                   startPoint: .topLeading, endPoint: .bottomTrailing)
                    IslandShape(bottomRadius: expanded ? 26 : 16)
                        .fill(Color.black)
                        .frame(width: expanded ? 360 : 140, height: expanded ? 110 : 32)
                    Text(L10n.get("settings.tapToPreview"))
                        .font(.caption).foregroundStyle(.white.opacity(0.8))
                        .frame(maxHeight: .infinity, alignment: .bottom).padding(.bottom, 10)
                }
                .frame(height: 170)
                .clipShape(RoundedRectangle(cornerRadius: 10))
                .contentShape(Rectangle())
                .onTapGesture { expanded.toggle() }
                .animation(store.settings.reduceMotion ? .linear(duration: 0.01) : .spring(response: s.response, dampingFraction: s.damping), value: expanded)
            }

            Section {
                Picker(L10n.get("settings.motion"), selection: $store.settings.motion) {
                    ForEach(AppSettings.Motion.allCases, id: \.self) { m in
                        Text(L10n.get("settings.motion.\(m.rawValue)")).tag(m)
                    }
                }
                .pickerStyle(.segmented)
                Toggle(L10n.get("settings.reduceMotion"), isOn: $store.settings.reduceMotion)
            }
        }
    }
}

private struct AboutPane: View {
    var body: some View {
        Form {
            Section {
                VStack(spacing: 8) {
                    Image(nsImage: NSApp.applicationIconImage).resizable().frame(width: 96, height: 96)
                    Text("SpartanX").font(.title.bold())
                    Text(L10n.format("settings.version", AppDelegate.version)).foregroundStyle(.secondary)
                    Text(L10n.get("settings.tagline")).foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity)
                .padding(.vertical, 8)
            }

            Section {
                Link(L10n.get("settings.source"), destination: URL(string: "https://github.com/\(ReleaseChecker.repository)")!)
                LabeledContent("License", value: "MIT")
            }
        }
    }
}
