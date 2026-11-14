import Foundation

public struct ReleaseUpdate: Equatable, Sendable {
    public let version: SemVersion
    public let tag: String
    public let pageURL: URL
    /// The macOS disk image, when the release has one.
    public let dmgURL: URL?
}

/// Finds newer releases of SpartanX on GitHub. macOS assets are named `SpartanX-<version>-macOS.dmg`.
public struct ReleaseChecker: Sendable {
    public static let repository = "xX-VyrnCore-Xx/SpartanX"

    private struct Release: Decodable {
        let tag_name: String
        let html_url: String
        let draft: Bool
        let prerelease: Bool
        let assets: [Asset]?
    }

    private struct Asset: Decodable {
        let name: String
        let browser_download_url: String
    }

    public init() {}

    /// Picks the newest non-draft release strictly newer than `current` from GitHub's JSON.
    public func newest(from json: Data, current: SemVersion, includePrereleases: Bool) throws -> ReleaseUpdate? {
        let releases = try JSONDecoder().decode([Release].self, from: json)
        var best: ReleaseUpdate?
        for r in releases where !r.draft && (includePrereleases || !r.prerelease) {
            guard let v = SemVersion(r.tag_name), v > current, best.map({ v > $0.version }) ?? true,
                  let page = URL(string: r.html_url) else { continue }
            let dmg = r.assets?.first { $0.name.hasSuffix("-macOS.dmg") }.flatMap { URL(string: $0.browser_download_url) }
            best = ReleaseUpdate(version: v, tag: r.tag_name, pageURL: page, dmgURL: dmg)
        }

        return best
    }

    public func check(current: SemVersion, includePrereleases: Bool, session: URLSession = .shared) async throws -> ReleaseUpdate? {
        var request = URLRequest(url: URL(string: "https://api.github.com/repos/\(Self.repository)/releases?per_page=20")!)
        request.setValue("SpartanX-macOS", forHTTPHeaderField: "User-Agent")
        let (data, _) = try await session.data(for: request)
        return try newest(from: data, current: current, includePrereleases: includePrereleases)
    }
}
