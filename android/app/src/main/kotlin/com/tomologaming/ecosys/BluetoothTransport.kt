package com.tomologaming.ecosys

import android.annotation.SuppressLint
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import android.bluetooth.BluetoothServerSocket
import android.bluetooth.BluetoothSocket
import org.json.JSONObject
import java.io.BufferedReader
import java.io.InputStreamReader
import java.io.PrintWriter
import java.util.UUID
import kotlin.concurrent.thread

/**
 * Classic Bluetooth (RFCOMM) transport. The app accepts incoming Ecosys connections and can also
 * connect to a peer. Messages are newline-delimited UTF-8 JSON envelopes.
 */
class BluetoothTransport(
    private val adapter: BluetoothAdapter,
    private val onMessage: (EcosysMessage) -> Unit,
    private val onState: (String) -> Unit,
    private val onConnected: (Boolean) -> Unit
) {
    companion object {
        val SERVICE_UUID: UUID = UUID.fromString("6e6f4d4f-0001-4d4f-4d4f-45434f595300")
    }

    @Volatile private var running = false
    @Volatile private var writer: PrintWriter? = null
    @Volatile private var socket: BluetoothSocket? = null
    @Volatile private var serverSocket: BluetoothServerSocket? = null

    @Volatile var peerName: String? = null
        private set

    val isConnected: Boolean
        get() = socket?.isConnected == true && writer != null

    @SuppressLint("MissingPermission")
    fun startServer() {
        if (running) return
        running = true
        thread(name = "ecosys-bt-server", isDaemon = true) {
            while (running) {
                var server: BluetoothServerSocket? = null
                try {
                    server = adapter.listenUsingRfcommWithServiceRecord("Ecosys", SERVICE_UUID)
                    serverSocket = server
                    if (!isConnected) onState("Warte auf Ecosys-Verbindung …")
                    val accepted = server.accept()
                    attach(accepted)
                } catch (t: Throwable) {
                    if (running) {
                        onState("Bluetooth-Server: ${t.message ?: t.javaClass.simpleName}")
                        runCatching { Thread.sleep(1200) }
                    }
                } finally {
                    runCatching { server?.close() }
                    serverSocket = null
                }
            }
        }
    }

    @SuppressLint("MissingPermission")
    fun connect(device: BluetoothDevice) {
        thread(name = "ecosys-bt-client", isDaemon = true) {
            try {
                adapter.cancelDiscovery()
                onState("Verbinde mit ${device.name ?: "Gerät"} …")
                val client = device.createRfcommSocketToServiceRecord(SERVICE_UUID)
                client.connect()
                attach(client)
            } catch (t: Throwable) {
                onState("Verbindung fehlgeschlagen: ${t.message ?: t.javaClass.simpleName}")
                onConnected(false)
            }
        }
    }

    @SuppressLint("MissingPermission")
    private fun attach(connected: BluetoothSocket) {
        runCatching { socket?.close() }
        socket = connected
        peerName = connected.remoteDevice.name ?: "Ecosys-Gerät"
        writer = PrintWriter(connected.outputStream, true)
        onConnected(true)
        onState("Verbunden mit $peerName")
        thread(name = "ecosys-bt-reader", isDaemon = true) { readLoop(connected) }
    }

    private fun readLoop(connected: BluetoothSocket) {
        try {
            BufferedReader(InputStreamReader(connected.inputStream)).use { reader ->
                while (running) {
                    val line = reader.readLine() ?: break
                    if (line.isBlank()) continue
                    runCatching { EcosysMessage.fromJson(JSONObject(line)) }
                        .onSuccess(onMessage)
                        .onFailure { onState("Ungültige Nachricht: ${it.message}") }
                }
            }
        } catch (t: Throwable) {
            if (running) onState("Verbindung beendet: ${t.message ?: t.javaClass.simpleName}")
        } finally {
            if (socket === connected) {
                writer = null
                socket = null
                peerName = null
                onConnected(false)
            }
        }
    }

    fun send(message: EcosysMessage): Boolean {
        val out = writer ?: return false
        return runCatching {
            out.println(message.toJson().toString())
            out.flush()
            true
        }.getOrDefault(false)
    }

    fun disconnect() {
        runCatching { writer?.close() }
        writer = null
        runCatching { socket?.close() }
        socket = null
        peerName = null
        onConnected(false)
        if (running) onState("Verbindung getrennt")
    }

    fun close() {
        running = false
        runCatching { serverSocket?.close() }
        serverSocket = null
        disconnect()
    }
}
