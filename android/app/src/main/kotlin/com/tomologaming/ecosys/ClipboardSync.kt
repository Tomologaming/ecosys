package com.tomologaming.ecosys

import java.security.MessageDigest

/**
 * A clipboard value that passed validation, together with the fields the
 * `clipboard.set` envelope carries.
 */
data class ClipboardUpdate(val text: String, val hash: String, val bytes: Int)

/**
 * Clipboard synchronisation rules.
 *
 * Kept free of Android and JSON types so the behaviour that matters — limits,
 * hashing, echo suppression and gating — can be unit tested on the JVM.
 */
class ClipboardSync {

    companion object {
        /** Largest text a single `clipboard.set` may carry. */
        const val MAX_CHARS = 65_536
        const val FORMAT = "text/plain"

        /** `sha256:` plus the first 16 hex characters of the UTF-8 SHA-256 digest. */
        fun hashOf(text: String): String {
            val digest = MessageDigest.getInstance("SHA-256").digest(text.toByteArray(Charsets.UTF_8))
            return "sha256:" + digest.take(8).joinToString("") { "%02x".format(it) }
        }

        /** Builds an update, or null when the text is empty or too large to send. */
        fun create(text: String?): ClipboardUpdate? {
            if (text.isNullOrEmpty()) return null
            if (text.length > MAX_CHARS) return null
            return ClipboardUpdate(text, hashOf(text), text.toByteArray(Charsets.UTF_8).size)
        }

        /**
         * Validates an incoming payload. Returns null when the content is empty,
         * oversized, or does not match the declared hash or byte length.
         * A missing byte count (-1) skips the length check; the hash is always required.
         */
        fun accept(text: String?, hash: String?, bytes: Int = -1): ClipboardUpdate? {
            if (hash.isNullOrEmpty()) return null
            val update = create(text) ?: return null
            if (hash != update.hash) return null
            if (bytes >= 0 && bytes != update.bytes) return null
            return update
        }
    }

    var enabled: Boolean = false
        private set

    private var lastSentHash: String? = null
    private var lastAppliedHash: String? = null

    /** Returns true when the state actually changed. */
    fun setEnabled(value: Boolean): Boolean {
        if (enabled == value) return false
        enabled = value
        lastSentHash = null
        return true
    }

    /**
     * Forgets the last sent value so the explicit "send now" action can push an
     * unchanged clipboard a second time.
     */
    fun allowResend() {
        lastSentHash = null
    }

    /**
     * The update to send for the current local clipboard, or null when sharing is
     * off, the text is unusable, or the value is unchanged or an echo of a
     * remote update.
     */
    fun outgoing(text: String?): ClipboardUpdate? {
        if (!enabled) return null
        val update = create(text) ?: return null
        if (update.hash == lastSentHash) return null
        if (update.hash == lastAppliedHash) return null
        lastSentHash = update.hash
        return update
    }

    /**
     * Accepts an incoming `clipboard.set`. Returns the update to put on the local
     * clipboard, or null when sharing is off or the payload is invalid.
     *
     * An accepted value is remembered as both applied and sent so the resulting
     * clipboard change is not echoed back to the peer.
     */
    fun incoming(text: String?, hash: String?, bytes: Int = -1): ClipboardUpdate? {
        if (!enabled) return null
        val update = accept(text, hash, bytes) ?: return null
        lastAppliedHash = update.hash
        lastSentHash = update.hash
        return update
    }
}
