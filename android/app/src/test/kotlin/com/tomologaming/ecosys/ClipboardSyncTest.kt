package com.tomologaming.ecosys

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ClipboardSyncTest {

    @Test
    fun `hash matches the documented sha256 prefix`() {
        // sha256("hallo") = d3751d33f9cd5049... trimmed to 16 hex characters.
        assertEquals("sha256:d3751d33f9cd5049", ClipboardSync.hashOf("hallo"))
    }

    @Test
    fun `empty and oversized text is rejected`() {
        assertNull(ClipboardSync.create(null))
        assertNull(ClipboardSync.create(""))
        assertNull(ClipboardSync.create("a".repeat(ClipboardSync.MAX_CHARS + 1)))
        assertNotNull(ClipboardSync.create("a".repeat(ClipboardSync.MAX_CHARS)))
    }

    @Test
    fun `byte length counts utf8 not characters`() {
        val update = ClipboardSync.create("grün")!!
        assertEquals(5, update.bytes)
        assertEquals(4, update.text.length)
    }

    @Test
    fun `incoming values are validated`() {
        val update = ClipboardSync.create("hallo")!!
        assertEquals(update, ClipboardSync.accept("hallo", update.hash, update.bytes))
        // Mismatching hash
        assertNull(ClipboardSync.accept("hallo", "sha256:0000000000000000", update.bytes))
        // Mismatching byte count
        assertNull(ClipboardSync.accept("hallo", update.hash, 99))
        // Missing hash
        assertNull(ClipboardSync.accept("hallo", null, update.bytes))
        // Missing byte count is tolerated, the hash still has to match
        assertEquals(update, ClipboardSync.accept("hallo", update.hash))
    }

    @Test
    fun `sharing is off by default and sends nothing`() {
        val sync = ClipboardSync()
        assertFalse(sync.enabled)
        assertNull(sync.outgoing("hallo"))
        assertNull(sync.incoming("hallo", ClipboardSync.hashOf("hallo")))
    }

    @Test
    fun `enabled sharing sends once per distinct value`() {
        val sync = ClipboardSync()
        assertTrue(sync.setEnabled(true))
        assertFalse(sync.setEnabled(true))

        val first = sync.outgoing("hallo")
        assertEquals("hallo", first?.text)
        assertNull(sync.outgoing("hallo"))
        assertNull(sync.outgoing(""))
        assertNotNull(sync.outgoing("welt"))
    }

    @Test
    fun `applied remote value is not echoed back`() {
        val sync = ClipboardSync()
        sync.setEnabled(true)

        val remote = ClipboardSync.create("von windows")!!
        val applied = sync.incoming(remote.text, remote.hash, remote.bytes)
        assertEquals("von windows", applied?.text)

        // The clipboard listener fires after the value was applied locally.
        assertNull(sync.outgoing("von windows"))

        // A genuinely new local value still goes out.
        assertEquals("lokal", sync.outgoing("lokal")?.text)
    }

    @Test
    fun `disabling sharing stops both directions and resets the send cache`() {
        val sync = ClipboardSync()
        sync.setEnabled(true)
        assertNotNull(sync.outgoing("hallo"))

        assertTrue(sync.setEnabled(false))
        assertNull(sync.outgoing("hallo"))
        assertNull(sync.incoming("hallo", ClipboardSync.hashOf("hallo")))

        sync.setEnabled(true)
        assertNotNull(sync.outgoing("hallo"))
    }
}
