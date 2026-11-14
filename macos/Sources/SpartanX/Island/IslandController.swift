import AppKit
import SpartanXCore
import SwiftUI

/// Borderless, non-activating panel that floats over the menu bar at the top centre of a screen.
final class IslandPanel: NSPanel {
    init(contentRect: NSRect) {
        super.init(contentRect: contentRect, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        level = NSWindow.Level(rawValue: NSWindow.Level.mainMenu.rawValue + 3)
        collectionBehavior = [.canJoinAllSpaces, .stationary, .fullScreenAuxiliary, .ignoresCycle]
        isMovable = false
        hidesOnDeactivate = false
        isReleasedWhenClosed = false
        ignoresMouseEvents = true
        acceptsMouseMovedEvents = true
    }

    // Key (but never main, and never activating the app) so SwiftUI buttons get the first click.
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}

/// Owns the island panel: places it over the notch, keeps it there when displays change, and
/// makes everything outside the island click-through by toggling `ignoresMouseEvents` from a
/// mouse monitor (which also drives hover).
@MainActor
final class IslandController {
    private static let margin: CGFloat = 24

    private let model: IslandModel
    private var panel: IslandPanel?
    private var monitors: [Any] = []
    private var screenObserver: NSObjectProtocol?

    init(model: IslandModel) {
        self.model = model
    }

    func show() {
        let size = NSSize(width: IslandLayout.maxSize.width + Self.margin * 2,
                          height: IslandLayout.maxSize.height + Self.margin)
        let panel = IslandPanel(contentRect: NSRect(origin: .zero, size: size))
        let host = NSHostingView(rootView: IslandView(model: model).frame(width: size.width, height: size.height, alignment: .top))
        host.frame = NSRect(origin: .zero, size: size)
        panel.contentView = host
        self.panel = panel
        place()
        panel.orderFrontRegardless()

        let handler: (NSEvent) -> Void = { [weak self] _ in
            MainActor.assumeIsolated { self?.updatePointer() }
        }
        if let global = NSEvent.addGlobalMonitorForEvents(matching: [.mouseMoved, .leftMouseDragged], handler: handler) {
            monitors.append(global)
        }
        if let local = NSEvent.addLocalMonitorForEvents(matching: [.mouseMoved, .leftMouseDragged, .mouseExited], handler: { event in
            handler(event)
            return event
        }) {
            monitors.append(local)
        }

        screenObserver = NotificationCenter.default.addObserver(
            forName: NSApplication.didChangeScreenParametersNotification, object: nil, queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated { self?.place() }
        }
    }

    func hide() {
        panel?.orderOut(nil)
    }

    func unhide() {
        place()
        panel?.orderFrontRegardless()
    }

    /// The display with a notch if there is one, otherwise the main display.
    private var targetScreen: NSScreen? {
        NSScreen.screens.first { $0.safeAreaInsets.top > 0 } ?? NSScreen.main ?? NSScreen.screens.first
    }

    private func place() {
        guard let panel, let screen = targetScreen else { return }
        model.notch = screen.notchSize
        let frame = screen.frame
        let size = panel.frame.size
        panel.setFrameOrigin(NSPoint(x: frame.midX - size.width / 2, y: frame.maxY - size.height))
    }

    /// Hit-tests the pointer against the island's current rectangle (screen coordinates).
    private func updatePointer() {
        guard let panel, let screen = targetScreen else { return }
        let size = model.size
        let rect = NSRect(x: screen.frame.midX - size.width / 2,
                          y: screen.frame.maxY - size.height,
                          width: size.width,
                          height: size.height).insetBy(dx: -4, dy: -4)
        let inside = rect.contains(NSEvent.mouseLocation)
        panel.ignoresMouseEvents = !inside
        model.setHovered(inside)
    }
}

extension NSScreen {
    /// Physical size of the camera housing, or nil on displays without a notch.
    var notchSize: IslandSize? {
        guard safeAreaInsets.top > 0,
              let left = auxiliaryTopLeftArea,
              let right = auxiliaryTopRightArea else { return nil }
        return IslandSize(width: Double(frame.width - left.width - right.width), height: Double(safeAreaInsets.top))
    }
}
