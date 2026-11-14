// swift-tools-version:5.10
// SpartanX for macOS: a native SwiftUI Dynamic Island around the notch.
// Build:  swift build -c release      Test:  swift test      Package:  scripts/build-dmg.sh <version>
import PackageDescription

let package = Package(
    name: "SpartanX",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "SpartanX", targets: ["SpartanX"]),
    ],
    targets: [
        // Platform-independent logic (state machine, timer, updates, weather, strings). Unit-tested.
        .target(name: "SpartanXCore"),
        // The SwiftUI / AppKit app.
        .executableTarget(
            name: "SpartanX",
            dependencies: ["SpartanXCore"],
            linkerSettings: [.linkedFramework("IOKit"), .linkedFramework("ServiceManagement")]
        ),
        .testTarget(name: "SpartanXCoreTests", dependencies: ["SpartanXCore"]),
    ]
)
