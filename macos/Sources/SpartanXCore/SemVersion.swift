import Foundation

/// Minimal SemVer 2.0 (major.minor.patch[-prerelease]) with correct precedence.
public struct SemVersion: Comparable, CustomStringConvertible, Sendable {
    public let major: Int
    public let minor: Int
    public let patch: Int
    public let prerelease: String?

    public init?(_ text: String) {
        var s = text.trimmingCharacters(in: .whitespaces)
        if s.hasPrefix("v") || s.hasPrefix("V") { s.removeFirst() }
        if let plus = s.firstIndex(of: "+") { s = String(s[..<plus]) }
        var pre: String?
        if let dash = s.firstIndex(of: "-") {
            pre = String(s[s.index(after: dash)...])
            s = String(s[..<dash])
        }

        let parts = s.split(separator: ".", omittingEmptySubsequences: false)
        guard (1...3).contains(parts.count) else { return nil }
        var nums = [0, 0, 0]
        for (i, p) in parts.enumerated() {
            guard let n = Int(p), n >= 0 else { return nil }
            nums[i] = n
        }

        major = nums[0]
        minor = nums[1]
        patch = nums[2]
        prerelease = (pre?.isEmpty ?? true) ? nil : pre
    }

    public var isPrerelease: Bool { prerelease != nil }

    public var description: String {
        "\(major).\(minor).\(patch)" + (prerelease.map { "-\($0)" } ?? "")
    }

    public static func < (a: SemVersion, b: SemVersion) -> Bool {
        if a.major != b.major { return a.major < b.major }
        if a.minor != b.minor { return a.minor < b.minor }
        if a.patch != b.patch { return a.patch < b.patch }
        switch (a.prerelease, b.prerelease) {
        case (nil, nil): return false
        case (nil, _): return false   // release > prerelease
        case (_, nil): return true
        case let (pa?, pb?): return comparePrerelease(pa, pb) < 0
        }
    }

    public static func == (a: SemVersion, b: SemVersion) -> Bool {
        !(a < b) && !(b < a)
    }

    private static func comparePrerelease(_ a: String, _ b: String) -> Int {
        let pa = a.split(separator: "."), pb = b.split(separator: ".")
        for (x, y) in zip(pa, pb) {
            switch (Int(x), Int(y)) {
            case let (nx?, ny?): if nx != ny { return nx < ny ? -1 : 1 }
            case (_?, nil): return -1
            case (nil, _?): return 1
            default: if x != y { return x < y ? -1 : 1 }
            }
        }

        return pa.count == pb.count ? 0 : (pa.count < pb.count ? -1 : 1)
    }
}
