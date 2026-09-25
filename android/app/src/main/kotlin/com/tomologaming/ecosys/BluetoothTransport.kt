package com.tomologaming.ecosys

import android.annotation.SuppressLint
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import java.io.BufferedReader
import java.io.InputStreamReader
import java.io.PrintWriter
import java.util.UUID
import kotlin.concurrent.thread

class BluetoothTransport(
    private val adapter: BluetoothAdapter,
    private val onMessage: (EcosysMessage) -> Unit,
    private val onState: (String) -> Unit
) {
    companion object {
        val SERVICE_UUID: UUID = UUID.fromString("6e6f4d4f-0001-4d4f-4d4f-45434f595300")
    }

    @Volatile private var running = false
    private var writer: PrintWriter? = null
    private var socket: android.bluetooth.BluetoothSocket? = null

    @SuppressLint("MissingPermission")
    fun startServer() {
        running = true
        thread(name = "ecosys-bt-server") {
            try {
                val server = adapter.listenUsingRfcommWithServiceRecord("Ecosys", SERVICE_UUID)
                onState("Listening for Ecosys connections")
                val accepted = server.accept()
                server.close()
                attachSocket(accepted, accepted.remoteDevice.name ?: "device")
            } catch (t: Throwable) {
                if (running) onState("Bluetooth server error: ${t.message}")
            }
        }
    }

    @SuppressLint("MissingPermission")
    fun connect(device: BluetoothDevice) {
        thread(name = "ecosys-bt-client") {
            try {
                adapter.cancelDiscovery()
                onState("Connecting to ${device.name ?: "device"}…")
                val connected = device.createRfcommSocketToServiceRecord(SERVICE_UUID)
                connected.connect()
                attachSocket(connected, device.name ?: "device")
            } catch (t: Throwable) {
                onState("Bluetooth connection error: ${t.message}")
            }
        }
    }

    private fun attachSocket(connected: android.bluetooth.BluetoothSocket, peerName: String) {
        socket?.close()
        socket = connected
        writer = PrintWriter(connected.outputStream, true)
        onState("Connected to $peerName")
        thread(name = "ecosys-bt-reader") {
            try {
                BufferedReader(InputStreamReader(connected.inputStream)).use { reader ->
                    while (running) {
                        val line = reader.readLine() ?: break
                        runCatching { EcosysMessage.fromJson(org.json.JSONObject(line)) }
                            .onSuccess(onMessage)
                            .onFailure { onState("Invalid message: ${it.message}") }
                    }
                }
            } catch (t: Throwable) {
                if (running) onState("Bluetooth read error: ${t.message}")
            }
        }
    }

    fun send(message: EcosysMessage) {
        writer?.println(message.toJson().toString())
    }

    fun close() {
        running = false
        writer = null
        runCatching { socket?.close() }
        socket = null
    }
}
