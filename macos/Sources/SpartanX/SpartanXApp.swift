import AppKit
import SpartanXCore
import SwiftUI

@main
struct SpartanXApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate

    var body: some Scene {
        MenuBarExtra {
            MenuContent(delegate: delegate)
        } label: {
            Image(systemName: "capsule.fill")
        }

        Settings {
            SettingsView()
                .environment(delegate.store)
                .environment(delegate.island)
        }
    }
}

private struct MenuContent: View {
    let delegate: AppDelegate

    var body: some View {
        SettingsLink { Text(L10n.get("menu.settings")) }
            .keyboardShortcut(",")
        Button(L10n.get("menu.welcome")) { delegate.showWelcome() }
        Button(L10n.get("menu.updates")) {
            Task {
                if let update = await delegate.checkForUpdates() {
                    NSWorkspace.shared.open(update.dmgURL ?? update.pageURL)
                }
            }
        }
        Divider()
        Button(L10n.get("menu.quit")) { NSApp.terminate(nil) }
            .keyboardShortcut("q")
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    static weak var shared: AppDelegate?

    static var version: String {
        Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.0.0"
    }

    let store = SettingsStore()
    lazy var island = IslandModel(store: store)
    private var islandController: IslandController?
    private var welcome: WelcomeWindowController?

    func applicationWillFinishLaunching(_ notification: Notification) {
        AppDelegate.shared = self
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        L10n.language = L10n.resolve(store.settings.language.rawValue)

        let controller = IslandController(model: island)
        controller.show()
        islandController = controller
        island.start()

        if store.settings.firstRunCompleted {
            LaunchAtLogin.set(store.settings.launchAtLogin)
        } else {
            showWelcome()
        }

        if store.settings.checkForUpdates {
            Task { _ = await checkForUpdates() }
        }
    }

    func showWelcome() {
        if let welcome {
            welcome.present()
            return
        }

        let controller = WelcomeWindowController(store: store, island: island) { [weak self] in
            self?.store.settings.firstRunCompleted = true
            self?.island.settingsChanged()
            self?.welcome = nil
        }
        welcome = controller
        controller.present()
    }

    /// Returns a newer release, if any.
    func checkForUpdates() async -> ReleaseUpdate? {
        guard let current = SemVersion(Self.version) else { return nil }
        return try? await ReleaseChecker().check(current: current, includePrereleases: store.settings.includePrereleases)
    }
}
