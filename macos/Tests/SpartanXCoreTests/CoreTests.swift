import XCTest
@testable import SpartanXCore

final class ActivityManagerTests: XCTestCase {
    func testHighestPriorityWinsAndTransientsExpire() {
        var now = Date(timeIntervalSince1970: 0)
        let m = ActivityManager(now: { now })

        m.setPersistent(.media)
        XCTAssertEqual(m.current.kind, .media)

        m.showTransient(.battery, for: 2)
        XCTAssertEqual(m.current.kind, .battery)

        now = now.addingTimeInterval(3)
        m.tick()
        XCTAssertEqual(m.current.kind, .media)

        m.remove(.media)
        XCTAssertEqual(m.current.kind, .idle)
    }

    func testOnChangeFiresOnlyOnChanges() {
        let m = ActivityManager()
        var changes = 0
        m.onChange = { _ in changes += 1 }
        m.setPersistent(.media)
        m.setPersistent(.media)
        m.tick()
        XCTAssertEqual(changes, 1)
    }
}

final class CountdownTests: XCTestCase {
    func testPauseResumeComplete() {
        var now = Date(timeIntervalSince1970: 0)
        let t = CountdownTimer(now: { now })
        var done = 0
        t.onCompleted = { done += 1 }

        t.start(60)
        now = now.addingTimeInterval(20)
        t.pause()
        now = now.addingTimeInterval(300)
        XCTAssertEqual(t.remaining, 40, accuracy: 0.001)

        t.resume()
        now = now.addingTimeInterval(40)
        t.tick()
        XCTAssertEqual(done, 1)
        XCTAssertFalse(t.isActive)
    }

    func testFormat() {
        XCTAssertEqual(CountdownTimer.format(65), "1:05")
        XCTAssertEqual(CountdownTimer.format(3725), "1:02:05")
        XCTAssertEqual(CountdownTimer.format(0.2), "0:01")
    }
}

final class LayoutTests: XCTestCase {
    func testSizesGrowWithMode() {
        for notch in [nil, IslandSize(width: 185, height: 32)] {
            for kind in ActivityKind.allCases {
                let c = IslandLayout.size(mode: .compact, activity: kind, notch: notch)
                let p = IslandLayout.size(mode: .peek, activity: kind, notch: notch)
                let e = IslandLayout.size(mode: .expanded, activity: kind, notch: notch)
                XCTAssertLessThan(c.height, p.height)
                XCTAssertLessThanOrEqual(p.height, e.height)
                XCTAssertLessThanOrEqual(e.width, IslandLayout.maxSize.width)
                XCTAssertLessThanOrEqual(p.width, IslandLayout.maxSize.width)
            }
        }
    }

    func testCompactIdleMatchesNotch() {
        let notch = IslandSize(width: 185, height: 32)
        XCTAssertEqual(IslandLayout.size(mode: .compact, activity: .idle, notch: notch), notch)
    }
}

final class SemVersionTests: XCTestCase {
    func testPrecedence() {
        func v(_ s: String) -> SemVersion { SemVersion(s)! }
        XCTAssertGreaterThan(v("v1.2.3"), v("1.2.2"))
        XCTAssertGreaterThan(v("1.0.0"), v("1.0.0-beta.2"))
        XCTAssertLessThan(v("1.0.0-beta.2"), v("1.0.0-beta.10"))
        XCTAssertLessThan(v("1.0.0-alpha"), v("1.0.0-alpha.1"))
        XCTAssertEqual(v("1.0.0+abc"), v("1.0.0"))
        XCTAssertNil(SemVersion("latest"))
    }
}

final class ReleaseCheckerTests: XCTestCase {
    let json = """
    [
      {"tag_name":"v0.3.0-beta.1","html_url":"https://x/3b","draft":false,"prerelease":true,
       "assets":[{"name":"SpartanX-0.3.0-beta.1-macOS.dmg","browser_download_url":"https://x/3b.dmg"}]},
      {"tag_name":"v0.2.0","html_url":"https://x/2","draft":false,"prerelease":false,
       "assets":[{"name":"SpartanX-Setup-0.2.0-x64.exe","browser_download_url":"https://x/2.exe"},
                 {"name":"SpartanX-0.2.0-macOS.dmg","browser_download_url":"https://x/2.dmg"}]},
      {"tag_name":"v9.9.9","html_url":"https://x/9","draft":true,"prerelease":false,"assets":[]}
    ]
    """.data(using: .utf8)!

    func testPicksNewestStableWithDmg() throws {
        let u = try ReleaseChecker().newest(from: json, current: SemVersion("0.1.0")!, includePrereleases: false)
        XCTAssertEqual(u?.tag, "v0.2.0")
        XCTAssertEqual(u?.dmgURL?.absoluteString, "https://x/2.dmg")
    }

    func testPrereleasesWhenAsked() throws {
        let u = try ReleaseChecker().newest(from: json, current: SemVersion("0.1.0")!, includePrereleases: true)
        XCTAssertEqual(u?.tag, "v0.3.0-beta.1")
    }

    func testUpToDate() throws {
        XCTAssertNil(try ReleaseChecker().newest(from: json, current: SemVersion("0.2.0")!, includePrereleases: false))
    }
}

final class SettingsAndStringsTests: XCTestCase {
    func testOldSettingsJsonGetsDefaults() throws {
        let s = try JSONDecoder().decode(AppSettings.self, from: #"{"firstRunCompleted":true,"motion":"bouncy"}"#.data(using: .utf8)!)
        XCTAssertTrue(s.firstRunCompleted)
        XCTAssertEqual(s.motion, .bouncy)
        XCTAssertTrue(s.mediaModule)
    }

    func testRoundTrip() throws {
        var s = AppSettings()
        s.weatherCity = "Roma"
        s.language = .it
        let back = try JSONDecoder().decode(AppSettings.self, from: JSONEncoder().encode(s))
        XCTAssertEqual(back, s)
    }

    func testLanguagesHaveSameKeys() {
        XCTAssertEqual(Set(L10n.tables["en"]!.keys), Set(L10n.tables["it"]!.keys))
    }

    func testResolve() {
        XCTAssertEqual(L10n.resolve("it"), "it")
        XCTAssertEqual(L10n.resolve("en"), "en")
    }
}
