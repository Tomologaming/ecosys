package com.tomologaming.ecosys

import org.json.JSONArray
import org.json.JSONObject
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** A device this client has exchanged messages with before. */
data class KnownPeer(
    val id: String,
    val name: String,
    val platform: String,
    val lastSeen: Long,
    val trusted: Boolean
)

/**
 * Local history of peers plus the user's confirmation for each of them.
 *
 * State and rules only — no Android or UI types — so the parts that matter
 * (cap, ordering, sanitising, tolerant decoding and the trust check the
 * clipboard gate uses) are covered by JVM unit tests.
 */
class KnownPeers {

    private val peers = LinkedHashMap<String, KnownPeer>()

    /** Newest first. */
    fun list(): List<KnownPeer> = peers.values.sortedByDescending { it.lastSeen }

    fun find(id: String?): KnownPeer? = if (id == null) null else peers[id]

    /** True only for a device the user confirmed; unknown ids are never trusted. */
    fun isTrusted(id: String?): Boolean = find(id)?.trusted == true

    /**
     * Records a sighting of [id]. Trust survives, the name and the timestamp do not.
     * Returns true when the stored entry changed and the caller should persist.
     */
    fun record(
        id: String?,
        name: String?,
        platform: String?,
        now: Long = System.currentTimeMillis()
    ): Boolean {
        if (id.isNullOrBlank()) return false
        val key = id.trim().take(MAX_ID)
        val existing = peers[key]
        val next = KnownPeer(
            id = key,
            name = cleanName(name, existing?.name),
            platform = cleanPlatform(platform, existing?.platform),
            lastSeen = now.coerceAtLeast(0L),
            trusted = existing?.trusted == true
        )
        if (next == existing) return false
        peers[key] = next
        prune()
        return true
    }

    /** Returns true when the flag changed. Unknown ids are ignored. */
    fun setTrusted(id: String?, trusted: Boolean): Boolean {
        val peer = find(id) ?: return false
        if (peer.trusted == trusted) return false
        peers[peer.id] = peer.copy(trusted = trusted)
        return true
    }

    fun forget(id: String?): Boolean = id != null && peers.remove(id) != null

    /**
     * Clears the history of devices that were never confirmed. Confirmed devices
     * stay, because dropping them would silently revoke the user's decision.
     * Returns how many entries were removed.
     */
    fun forgetUntrusted(): Int {
        val before = peers.size
        peers.entries.removeAll { !it.value.trusted }
        return before - peers.size
    }

    fun load(json: String?) {
        peers.clear()
        for (peer in decode(json)) peers[peer.id] = peer
    }

    fun snapshot(): String = encode(list())

    private fun prune() {
        while (peers.size > MAX_PEERS) {
            val oldest = peers.values.filter { !it.trusted }.minByOrNull { it.lastSeen } ?: return
            peers.remove(oldest.id)
        }
    }

    companion object {
        /** Upper bound for unconfirmed history entries. */
        const val MAX_PEERS = 20
        const val VERSION = 1

        private const val MAX_ID = 64
        private const val MAX_NAME = 64
        private const val MAX_PLATFORM = 16

        private const val MINUTE = 60_000L
        private const val HOUR = 60 * MINUTE
        private const val DAY = 24 * HOUR

        fun encode(entries: List<KnownPeer>): String {
            val array = JSONArray()
            for (peer in entries) {
                array.put(
                    JSONObject()
                        .put("id", peer.id)
                        .put("name", peer.name)
                        .put("platform", peer.platform)
                        .put("lastSeen", peer.lastSeen)
                        .put("trusted", peer.trusted)
                )
            }
            return JSONObject().put("version", VERSION).put("peers", array).toString()
        }

        /**
         * Reads a stored list back. Damaged entries are skipped instead of
         * discarding the whole history, and duplicate ids keep the newest entry.
         */
        fun decode(json: String?): List<KnownPeer> {
            if (json.isNullOrBlank()) return emptyList()
            val stored = runCatching { JSONObject(json) }.getOrNull() ?: return emptyList()
            val array = stored.optJSONArray("peers") ?: return emptyList()
            val result = LinkedHashMap<String, KnownPeer>()
            for (index in 0 until array.length()) {
                val entry = array.optJSONObject(index) ?: continue
                val id = entry.optString("id").trim().take(MAX_ID)
                if (id.isEmpty()) continue
                val peer = KnownPeer(
                    id = id,
                    name = cleanName(entry.optString("name"), null),
                    platform = cleanPlatform(entry.optString("platform"), null),
                    lastSeen = entry.optLong("lastSeen", 0L).coerceAtLeast(0L),
                    trusted = entry.optBoolean("trusted", false)
                )
                val previous = result[peer.id]
                if (previous == null || previous.lastSeen <= peer.lastSeen) result[peer.id] = peer
            }
            // Bounded even when the stored file was edited by hand.
            return result.values.sortedByDescending { it.lastSeen }.take(MAX_PEERS * 2)
        }

        /** Compact German description of when a peer was last seen. */
        fun describeLastSeen(now: Long, lastSeen: Long): String {
            val diff = now - lastSeen
            if (diff < MINUTE) return "gerade eben"
            if (diff < HOUR) return "vor ${diff / MINUTE} Min"
            if (diff < DAY) return "vor ${diff / HOUR} Std"
            if (diff < 2 * DAY) return "gestern"
            if (diff < 30 * DAY) return "vor ${diff / DAY} Tagen"
            return SimpleDateFormat("dd.MM.yyyy", Locale.GERMANY).format(Date(lastSeen))
        }

        private fun cleanName(raw: String?, fallback: String?): String {
            val trimmed = raw?.trim().orEmpty()
            val value = if (trimmed.isEmpty()) fallback?.trim().orEmpty() else trimmed
            if (value.isEmpty()) return "Unbekanntes Gerät"
            return value.take(MAX_NAME)
        }

        private fun cleanPlatform(raw: String?, fallback: String?): String {
            val trimmed = raw?.trim()?.lowercase(Locale.ROOT).orEmpty()
            val value = if (trimmed.isEmpty()) fallback?.trim().orEmpty() else trimmed
            return if (value.isEmpty()) "unbekannt" else value.take(MAX_PLATFORM)
        }
    }
}
