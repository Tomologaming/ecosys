package com.tomologaming.ecosys

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class KnownPeersTest {

    private val now = 1_700_000_000_000L

    @Test
    fun `sightings are recorded with the reported name and platform`() {
        val peers = KnownPeers()
        assertTrue(peers.record("android-1", "Pixel", "android", now - 5_000))

        val peer = peers.find("android-1")!!
        assertEquals("Pixel", peer.name)
        assertEquals("android", peer.platform)
        assertEquals(now - 5_000, peer.lastSeen)
        assertFalse(peer.trusted)
    }

    @Test
    fun `blank ids are ignored and unknown ids are never trusted`() {
        val peers = KnownPeers()
        assertFalse(peers.record(" ", "Namenlos", "android", now))
        assertFalse(peers.record(null, "Namenlos", "android", now))
        assertTrue(peers.list().isEmpty())

        assertFalse(peers.isTrusted(null))
        assertFalse(peers.isTrusted("android-1"))
        assertFalse(peers.setTrusted("android-1", true))
    }

    @Test
    fun `a second sighting keeps trust and refreshes name and time`() {
        val peers = KnownPeers()
        peers.record("android-1", "Pixel", "android", now - 5_000)
        peers.setTrusted("android-1", true)

        assertTrue(peers.record("android-1", "Pixel 8", "android", now))
        assertEquals("Pixel 8", peers.find("android-1")?.name)
        assertEquals(now, peers.find("android-1")?.lastSeen)
        assertTrue(peers.isTrusted("android-1"))
    }

    @Test
    fun `unchanged sightings report no change`() {
        val peers = KnownPeers()
        peers.record("android-1", "Pixel", "android", now)
        assertFalse(peers.record("android-1", "Pixel", "android", now))
    }

    @Test
    fun `history is ordered by the last sighting`() {
        val peers = KnownPeers()
        peers.record("android-1", "Pixel", "android", now - 5_000)
        peers.record("windows-1", "Büro-PC", "windows", now)
        assertEquals(listOf("windows-1", "android-1"), peers.list().map { it.id })
    }

    @Test
    fun `history is capped but confirmed devices survive`() {
        val peers = KnownPeers()
        peers.record("android-kept", "Pixel", "android", now - 1_000)
        peers.setTrusted("android-kept", true)

        repeat(KnownPeers.MAX_PEERS + 5) { index ->
            peers.record("android-$index", "Gerät $index", "android", now - 100_000 + index)
        }

        assertEquals(KnownPeers.MAX_PEERS, peers.list().size)
        assertTrue(peers.isTrusted("android-kept"))
    }

    @Test
    fun `clearing keeps confirmed devices`() {
        val peers = KnownPeers()
        peers.record("android-1", "Pixel", "android", now - 1_000)
        peers.setTrusted("android-1", true)
        peers.record("android-2", "Tablet", "android", now)

        assertEquals(1, peers.forgetUntrusted())
        assertEquals(1, peers.list().size)
        assertTrue(peers.isTrusted("android-1"))
        assertNull(peers.find("android-2"))
    }

    @Test
    fun `encoded history round-trips`() {
        val peers = KnownPeers()
        peers.record("android-1", "Pixel", "android", now - 5_000)
        peers.setTrusted("android-1", true)

        val restored = KnownPeers()
        restored.load(peers.snapshot())

        assertEquals(1, restored.list().size)
        assertEquals("Pixel", restored.find("android-1")?.name)
        assertEquals("android", restored.find("android-1")?.platform)
        assertTrue(restored.isTrusted("android-1"))
    }

    @Test
    fun `damaged history is skipped instead of losing everything`() {
        assertTrue(KnownPeers.decode("{nope").isEmpty())
        assertTrue(KnownPeers.decode(null).isEmpty())
        assertTrue(KnownPeers.decode("").isEmpty())
        assertTrue(KnownPeers.decode("""{"peers":"keine Liste"}""").isEmpty())

        val broken = KnownPeers.decode(
            """{"version":1,"peers":[{"name":"ohne id"},{"id":"a","lastSeen":5,"trusted":false},{"id":"a","lastSeen":9,"trusted":true}]}"""
        )
        assertEquals(1, broken.size)
        assertEquals("a", broken[0].id)
        assertEquals(9L, broken[0].lastSeen)
        assertTrue(broken[0].trusted)

        val fallback = KnownPeers.decode("""{"peers":[{"id":"b"}]}""")
        assertEquals("Unbekanntes Gerät", fallback[0].name)
        assertEquals("unbekannt", fallback[0].platform)
    }

    @Test
    fun `last seen is described in german`() {
        assertEquals("gerade eben", KnownPeers.describeLastSeen(now, now - 30_000))
        assertEquals("gerade eben", KnownPeers.describeLastSeen(now, now + 5_000))
        assertEquals("vor 5 Min", KnownPeers.describeLastSeen(now, now - 5 * 60_000))
        assertEquals("vor 3 Std", KnownPeers.describeLastSeen(now, now - 3 * 3_600_000))
        assertEquals("gestern", KnownPeers.describeLastSeen(now, now - 30 * 3_600_000))
        assertEquals("vor 3 Tagen", KnownPeers.describeLastSeen(now, now - 3 * 86_400_000))
        assertTrue(
            KnownPeers.describeLastSeen(now, now - 40L * 86_400_000)
                .matches(Regex("""\d{2}\.\d{2}\.\d{4}"""))
        )
    }
}
