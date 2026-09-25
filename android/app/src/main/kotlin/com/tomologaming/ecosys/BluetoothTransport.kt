package com.tomologaming.ecosys

import android.annotation.SuppressLint
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import java.io.BufferedReader
import java.io.InputStreamReader
import java.io.PrintWriter
import java.util.UUID
import kotlin.concurrent.thread

class BluetoothTransport(private val adapter: BluetoothAdapter, private val onMessage: (EcosysMessage) -> Unit, private val onState: (String) -> Unit) {
    companion object { val SERVICE_UUID: UUID = UUID.fromString("6e6f4d4f-0001-4d4f-4d4f-45434f595300") }
    @Volatile private var running=false
    private var writer: PrintWriter?=null

    @SuppressLint("MissingPermission") fun startServer() { running=true; thread { try { val server=adapter.listenUsingRfcommWithServiceRecord("Ecosys",SERVICE_UUID); onState("Listening for Bluetooth connections"); val socket=server.accept(); server.close(); writer=PrintWriter(socket.outputStream,true); onState("Connected"); BufferedReader(InputStreamReader(socket.inputStream)).use { reader -> while(running) { val line=reader.readLine() ?: break; runCatching { EcosysMessage.fromJson(org.json.JSONObject(line)) }.onSuccess(onMessage).onFailure { onState("Invalid message: ${it.message}") } } } } catch(t:Throwable) { onState("Bluetooth error: ${t.message}") } } }
    @SuppressLint("MissingPermission") fun connect(device: BluetoothDevice) { thread { try { adapter.cancelDiscovery(); val socket=device.createRfcommSocketToServiceRecord(SERVICE_UUID); socket.connect(); writer=PrintWriter(socket.outputStream,true); onState("Connected to ${device.name ?: "device"}") } catch(t:Throwable) { onState("Bluetooth connection error: ${t.message}") } } }
    fun send(message:EcosysMessage) { writer?.println(message.toJson().toString()) }
    fun close() { running=false; writer=null }
}
