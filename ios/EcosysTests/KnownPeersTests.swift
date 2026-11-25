import XCTest

/// The same rules the Android JVM tests and the Windows self-test cover, so a
/// behaviour proven on one client holds on the others. State only: the class
/// under test has no UIKit or SwiftUI dependency.
final class KnownPeersTests: XCTestCase {
    private let now: Int64 = 1_700_000_000_000
    private let minute: Int64 = 60_000
    private let hour: Int64 = 3_600_000
    private let day: Int64 = 86_400_000

    func testSightingsAreRecordedWithTheReportedNameAndPlatform() {
        let peers = KnownPeers()
        XCTAssertTrue(peers.record(id: "android-1", name: "Pixel", platform: "android", now: now - 5_000))

        let peer = peers.find("android-1")
        XCTAssertEqual("Pixel", peer?.name)
        XCTAssertEqual("android", peer?.platform)
        XCTAssertEqual(now - 5_000, peer?.lastSeen)
        XCTAssertEqual(false, peer?.trusted)
    }

    func testBlankIdsAreIgnoredAndUnknownIdsAreNeverTrusted() {
        let peers = KnownPeers()
        XCTAssertFalse(peers.record(id: " ", name: "Namenlos", platform: "android", now: now))
        XCTAssertFalse(peers.record(id: nil, name: "Namenlos", platform: "android", now: now))
        XCTAssertTrue(peers.list.isEmpty)

        XCTAssertFalse(peers.isTrusted(nil))
        XCTAssertFalse(peers.isTrusted("android-1"))
        XCTAssertFalse(peers.setTrusted("android-1", true))
        XCTAssertFalse(peers.setTrusted(nil, true))
        XCTAssertFalse(peers.forget("android-1"))
    }

    func testIdsAreTrimmedAndCappedLikeTheOtherClients() {
        let peers = KnownPeers()
        XCTAssertTrue(peers.record(id: "  android-1  ", name: "Pixel", platform: "android", now: now))
        XCTAssertEqual(1, peers.list.count)
        XCTAssertNotNil(peers.find("android-1"))

        let longId = String(repeating: "x", count: 80)
        XCTAssertTrue(peers.record(id: longId, name: "Lang", platform: "android", now: now))
        XCTAssertEqual(64, peers.find(longId)?.id.count)
        // The capped key still answers the full id, so the trust gate cannot miss.
        XCTAssertTrue(peers.setTrusted(longId, true))
        XCTAssertTrue(peers.isTrusted(longId))
    }

    func testASecondSightingKeepsTrustAndRefreshesNameAndTime() {
        let peers = KnownPeers()
        peers.record(id: "android-1", name: "Pixel", platform: "android", now: now - 5_000)
        peers.setTrusted("android-1", true)

        XCTAssertTrue(peers.record(id: "android-1", name: "Pixel 8", platform: "android", now: now))
        XCTAssertEqual("Pixel 8", peers.find("android-1")?.name)
        XCTAssertEqual(now, peers.find("android-1")?.lastSeen)
        XCTAssertTrue(peers.isTrusted("android-1"))
        XCTAssertFalse(peers.setTrusted("android-1", true))
    }

    func testUnchangedSightingsReportNoChange() {
        let peers = KnownPeers()
        peers.record(id: "android-1", name: "Pixel", platform: "android", now: now)
        XCTAssertFalse(peers.record(id: "android-1", name: "Pixel", platform: "android", now: now))
    }

    func testHistoryIsOrderedByTheLastSighting() {
        let peers = KnownPeers()
        peers.record(id: "android-1", name: "Pixel", platform: "android", now: now - 5_000)
        peers.record(id: "windows-1", name: "Büro-PC", platform: "windows", now: now)
        XCTAssertEqual(["windows-1", "android-1"], peers.list.map { $0.id })
    }

    func testHistoryIsCappedButConfirmedDevicesSurvive() {
        let peers = KnownPeers()
        peers.record(id: "android-kept", name: "Pixel", platform: "android", now: now - 1_000)
        peers.setTrusted("android-kept", true)

        for index in 0..<(KnownPeers.maxPeers + 5) {
            peers.record(
                id: "android-\(index)",
                name: "Gerät \(index)",
                platform: "android",
                now: now - 100_000 + Int64(index)
            )
        }

        XCTAssertEqual(KnownPeers.maxPeers, peers.list.count)
        XCTAssertTrue(peers.isTrusted("android-kept"))
        // The oldest unconfirmed sighting is dropped, the newest ones stay.
        XCTAssertNil(peers.find("android-0"))
        XCTAssertNotNil(peers.find("android-\(KnownPeers.maxPeers + 4)"))
    }

    func testClearingKeepsConfirmedDevices() {
        let peers = KnownPeers()
        peers.record(id: "android-1", name: "Pixel", platform: "android", now: now - 1_000)
        peers.setTrusted("android-1", true)
        peers.record(id: "android-2", name: "Tablet", platform: "android", now: now)

        XCTAssertEqual(1, peers.forgetUntrusted())
        XCTAssertEqual(1, peers.list.count)
        XCTAssertTrue(peers.isTrusted("android-1"))
        XCTAssertNil(peers.find("android-2"))
        XCTAssertEqual(0, peers.forgetUntrusted())
    }

    func testEncodedHistoryRoundTrips() {
        let peers = KnownPeers()
        peers.record(id: "android-1", name: "Pixel", platform: "android", now: now - 5_000)
        peers.setTrusted("android-1", true)

        let restored = KnownPeers(json: peers.snapshot())
        XCTAssertEqual(1, restored.list.count)
        XCTAssertEqual("Pixel", restored.find("android-1")?.name)
        XCTAssertEqual("android", restored.find("android-1")?.platform)
        XCTAssertEqual(now - 5_000, restored.find("android-1")?.lastSeen)
        XCTAssertTrue(restored.isTrusted("android-1"))
    }

    func testDamagedHistoryIsSkippedInsteadOfLosingEverything() {
        XCTAssertTrue(KnownPeers.decode("{nope").isEmpty)
        XCTAssertTrue(KnownPeers.decode(nil).isEmpty)
        XCTAssertTrue(KnownPeers.decode("").isEmpty)
        XCTAssertTrue(KnownPeers.decode("   ").isEmpty)
        XCTAssertTrue(KnownPeers.decode("{\"peers\":\"keine Liste\"}").isEmpty)
        XCTAssertTrue(KnownPeers.decode("[]").isEmpty)

        // One unreadable entry must not discard the readable ones around it.
        let mixed = KnownPeers.decode(
            "{\"version\":1,\"peers\":[{\"name\":\"ohne id\"},\"kaputt\",{\"id\":\"a\",\"name\":\"Pixel\",\"lastSeen\":5}]}"
        )
        XCTAssertEqual(["a"], mixed.map { $0.id })

        let duplicates = KnownPeers.decode(
            "{\"version\":1,\"peers\":[{\"id\":\"a\",\"lastSeen\":5,\"trusted\":false},"
                + "{\"id\":\"a\",\"lastSeen\":9,\"trusted\":true}]}"
        )
        XCTAssertEqual(1, duplicates.count)
        XCTAssertEqual(9, duplicates.first?.lastSeen)
        XCTAssertEqual(true, duplicates.first?.trusted)

        let fallback = KnownPeers.decode("{\"peers\":[{\"id\":\"b\"}]}")
        XCTAssertEqual("Unbekanntes Gerät", fallback.first?.name)
        XCTAssertEqual("unbekannt", fallback.first?.platform)
        XCTAssertEqual(0, fallback.first?.lastSeen)
    }

    func testLastSeenIsDescribedInGerman() {
        XCTAssertEqual("gerade eben", KnownPeers.describeLastSeen(now: now, lastSeen: now - 30_000))
        XCTAssertEqual("gerade eben", KnownPeers.describeLastSeen(now: now, lastSeen: now + 5_000))
        XCTAssertEqual("vor 5 Min", KnownPeers.describeLastSeen(now: now, lastSeen: now - 5 * minute))
        XCTAssertEqual("vor 3 Std", KnownPeers.describeLastSeen(now: now, lastSeen: now - 3 * hour))
        XCTAssertEqual("gestern", KnownPeers.describeLastSeen(now: now, lastSeen: now - 30 * hour))
        XCTAssertEqual("vor 3 Tagen", KnownPeers.describeLastSeen(now: now, lastSeen: now - 3 * day))

        let older = KnownPeers.describeLastSeen(now: now, lastSeen: now - 40 * day)
        XCTAssertNotNil(older.range(of: "^\\d{2}\\.\\d{2}\\.\\d{4}$", options: .regularExpression), older)
    }
}
