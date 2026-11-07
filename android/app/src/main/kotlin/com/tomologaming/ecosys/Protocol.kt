package com.tomologaming.ecosys

import org.json.JSONArray
import org.json.JSONObject
import java.util.UUID

data class EcosysMessage(
    val type: String,
    val sender: String,
    val timestamp: Long = System.currentTimeMillis(),
    val messageId: String = UUID.randomUUID().toString(),
    val payload: JSONObject = JSONObject()
) {
    fun toJson(): JSONObject = JSONObject()
        .put("protocol", PROTOCOL)
        .put("type", type)
        .put("messageId", messageId)
        .put("sender", sender)
        .put("timestamp", timestamp)
        .put("payload", payload)

    companion object {
        const val PROTOCOL = "ecosys/1"

        fun fromJson(json: JSONObject): EcosysMessage {
            require(json.optString("protocol") == PROTOCOL) { "Unsupported protocol" }
            val type = json.optString("type")
            require(type.isNotEmpty()) { "Missing message type" }
            return EcosysMessage(
                type = type,
                sender = json.optString("sender"),
                timestamp = json.optLong("timestamp", System.currentTimeMillis()),
                messageId = json.optString("messageId").ifEmpty { UUID.randomUUID().toString() },
                payload = json.optJSONObject("payload") ?: JSONObject()
            )
        }

        fun hello(sender: String, deviceName: String): EcosysMessage = EcosysMessage(
            type = "hello",
            sender = sender,
            payload = JSONObject()
                .put("deviceName", deviceName)
                .put("platform", "android")
                .put("capabilities", JSONArray().put("hello").put("ping").put("clipboard"))
        )

        fun clipboardSet(sender: String, update: ClipboardUpdate): EcosysMessage = EcosysMessage(
            type = "clipboard.set",
            sender = sender,
            payload = JSONObject()
                .put("text", update.text)
                .put("format", ClipboardSync.FORMAT)
                .put("hash", update.hash)
                .put("bytes", update.bytes)
        )

        fun clipboardState(sender: String, enabled: Boolean): EcosysMessage = EcosysMessage(
            type = "clipboard.state",
            sender = sender,
            payload = JSONObject()
                .put("enabled", enabled)
                .put("maxChars", ClipboardSync.MAX_CHARS)
        )
    }
}
