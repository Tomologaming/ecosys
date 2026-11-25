import Foundation

/// A device this client has exchanged messages with before.
struct KnownPeer: Identifiable, Equatable {
    let id: String
    let name: String
    let platform: String
    let lastSeen: Int64
    let trusted: Bool
}

/// Local history of peers plus the user's confirmation for each of them.
///
/// State and rules only — no UIKit or SwiftUI types — so the cap, the
/// sanitising, the tolerant decoding and the trust check stay independent of
/// the UI. The document is the same JSON Android keeps in its preferences and
/// Windows keeps in `%APPDATA%\Ecosys\peers.json`, so rules learned on one
/// client hold on the others.
final class KnownPeers {
    /// Upper bound for unconfirmed history entries. Confirmed devices are never pruned.
    static let maxPeers = 20
    static let formatVersion = 1

    private static let maxIdLength = 64
    private static let maxNameLength = 64
    private static let maxPlatformLength = 16

    private static let minute: Int64 = 60_000
    private static let hour: Int64 = 60 * minute
    private static let day: Int64 = 24 * hour

    private var peers: [String: KnownPeer] = [:]

    init(json: String? = nil) {
        load(json)
    }

    /// Newest first.
    var list: [KnownPeer] {
        peers.values.sorted { $0.lastSeen > $1.lastSeen }
    }

    func find(_ id: String?) -> KnownPeer? {
        guard let key = KnownPeers.key(id) else { return nil }
        return peers[key]
    }

    /// True only for a device the user confirmed; unknown ids are never trusted.
    func isTrusted(_ id: String?) -> Bool {
        find(id)?.trusted == true
    }

    /// Records a sighting. Trust survives, the name and the timestamp do not.
    /// Returns true when the stored entry changed and the caller should persist.
    @discardableResult
    func record(id: String?, name: String?, platform: String?, now: Int64 = KnownPeers.now()) -> Bool {
        guard let key = KnownPeers.key(id) else { return false }

        let existing = peers[key]
        let next = KnownPeer(
            id: key,
            name: KnownPeers.cleanName(name, fallback: existing?.name),
            platform: KnownPeers.cleanPlatform(platform, fallback: existing?.platform),
            lastSeen: max(0, now),
            trusted: existing?.trusted == true
        )

        guard next != existing else { return false }

        peers[key] = next
        prune()
        return true
    }

    /// Returns true when the flag changed. Unknown ids are ignored.
    @discardableResult
    func setTrusted(_ id: String?, _ trusted: Bool) -> Bool {
        guard let peer = find(id), peer.trusted != trusted else { return false }
        peers[peer.id] = KnownPeer(
            id: peer.id,
            name: peer.name,
            platform: peer.platform,
            lastSeen: peer.lastSeen,
            trusted: trusted
        )
        return true
    }

    @discardableResult
    func forget(_ id: String?) -> Bool {
        guard let key = KnownPeers.key(id) else { return false }
        return peers.removeValue(forKey: key) != nil
    }

    /// Clears the history of devices that were never confirmed. Confirmed
    /// devices stay, because dropping them would silently revoke a decision the
    /// user made. Returns how many entries were removed.
    @discardableResult
    func forgetUntrusted() -> Int {
        let before = peers.count
        peers = peers.filter { $0.value.trusted }
        return before - peers.count
    }

    func load(_ json: String?) {
        peers.removeAll()
        for peer in KnownPeers.decode(json) {
            peers[peer.id] = peer
        }
    }

    func snapshot() -> String {
        KnownPeers.encode(list)
    }

    private func prune() {
        while peers.count > KnownPeers.maxPeers {
            let oldest = peers.values
                .filter { !$0.trusted }
                .min { $0.lastSeen < $1.lastSeen }
            guard let oldest else { return }
            peers.removeValue(forKey: oldest.id)
        }
    }

    // MARK: - Encoding

    static func now() -> Int64 {
        Int64((Date().timeIntervalSince1970 * 1000).rounded())
    }

    static func encode(_ entries: [KnownPeer]) -> String {
        let array: [[String: Any]] = entries.map { peer in
            [
                "id": peer.id,
                "name": peer.name,
                "platform": peer.platform,
                "lastSeen": peer.lastSeen,
                "trusted": peer.trusted
            ]
        }
        let document: [String: Any] = ["version": formatVersion, "peers": array]

        guard let data = try? JSONSerialization.data(withJSONObject: document),
              let json = String(data: data, encoding: .utf8) else {
            return "{\"version\":\(formatVersion),\"peers\":[]}"
        }
        return json
    }

    /// Reads a stored list back. Damaged entries are skipped instead of
    /// discarding the whole history, and duplicate ids keep the newest entry.
    static func decode(_ json: String?) -> [KnownPeer] {
        guard let json,
              !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let data = json.data(using: .utf8),
              let document = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let array = document["peers"] as? [Any] else {
            return []
        }

        var result: [String: KnownPeer] = [:]
        for entry in array {
            guard let entry = entry as? [String: Any] else { continue }

            let rawId = (entry["id"] as? String ?? "")
                .trimmingCharacters(in: .whitespacesAndNewlines)
            guard !rawId.isEmpty else { continue }

            let peer = KnownPeer(
                id: String(rawId.prefix(maxIdLength)),
                name: cleanName(entry["name"] as? String, fallback: nil),
                platform: cleanPlatform(entry["platform"] as? String, fallback: nil),
                lastSeen: max(0, (entry["lastSeen"] as? NSNumber)?.int64Value ?? 0),
                trusted: (entry["trusted"] as? NSNumber)?.boolValue ?? false
            )

            if let previous = result[peer.id], previous.lastSeen > peer.lastSeen { continue }
            result[peer.id] = peer
        }

        // Bounded even when the stored document was edited by hand.
        return result.values
            .sorted { $0.lastSeen > $1.lastSeen }
            .prefix(maxPeers * 2)
            .map { $0 }
    }

    /// Compact German description of when a peer was last seen.
    static func describeLastSeen(now: Int64, lastSeen: Int64) -> String {
        let diff = now - lastSeen
        if diff < minute { return "gerade eben" }
        if diff < hour { return "vor \(diff / minute) Min" }
        if diff < day { return "vor \(diff / hour) Std" }
        if diff < 2 * day { return "gestern" }
        if diff < 30 * day { return "vor \(diff / day) Tagen" }

        let date = Date(timeIntervalSince1970: TimeInterval(lastSeen) / 1000)
        return dateFormatter.string(from: date)
    }

    private static let dateFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "de_DE")
        formatter.dateFormat = "dd.MM.yyyy"
        return formatter
    }()

    /// Stored key for an id: trimmed and capped exactly as `record` writes it,
    /// so a padded or over-long sender still finds its entry.
    private static func key(_ id: String?) -> String? {
        guard let id else { return nil }
        let trimmed = id.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        return String(trimmed.prefix(maxIdLength))
    }

    private static func cleanName(_ raw: String?, fallback: String?) -> String {
        let trimmed = (raw ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        let value = trimmed.isEmpty
            ? (fallback ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
            : trimmed
        if value.isEmpty { return "Unbekanntes Gerät" }
        return String(value.prefix(maxNameLength))
    }

    private static func cleanPlatform(_ raw: String?, fallback: String?) -> String {
        let trimmed = (raw ?? "").trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        let value = trimmed.isEmpty
            ? (fallback ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
            : trimmed
        if value.isEmpty { return "unbekannt" }
        return String(value.prefix(maxPlatformLength))
    }
}
