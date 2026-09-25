package com.tomologaming.ecosys

import org.json.JSONObject
import java.util.UUID

data class EcosysMessage(val type: String, val sender: String, val timestamp: Long = System.currentTimeMillis(), val messageId: String = UUID.randomUUID().toString(), val payload: JSONObject = JSONObject()) {
    fun toJson() = JSONObject().put("protocol","ecosys/1").put("type",type).put("messageId",messageId).put("sender",sender).put("timestamp",timestamp).put("payload",payload)
    companion object { fun fromJson(j: JSONObject): EcosysMessage { require(j.optString("protocol")=="ecosys/1") { "Unsupported protocol" }; return EcosysMessage(j.getString("type"),j.getString("sender"),j.getLong("timestamp"),j.getString("messageId"),j.getJSONObject("payload")) } }
}
