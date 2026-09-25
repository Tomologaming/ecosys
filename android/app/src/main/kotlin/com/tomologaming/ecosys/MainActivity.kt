package com.tomologaming.ecosys

import android.Manifest
import android.annotation.SuppressLint
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import android.bluetooth.BluetoothManager
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.core.app.ActivityCompat
import org.json.JSONObject

class MainActivity : ComponentActivity() {
    private lateinit var status: TextView
    private lateinit var devices: LinearLayout
    private var transport: BluetoothTransport? = null

    private val discoveryReceiver = object : BroadcastReceiver() {
        @SuppressLint("MissingPermission")
        override fun onReceive(context: Context, intent: Intent) {
            if (BluetoothDevice.ACTION_FOUND == intent.action) {
                val device = intent.getParcelableExtraCompat<BluetoothDevice>(BluetoothDevice.EXTRA_DEVICE) ?: return
                val button = Button(this@MainActivity).apply {
                    text = "Connect: ${device.name ?: "Unknown device"}"
                    setOnClickListener { transport?.connect(device) }
                }
                devices.addView(button)
            } else if (BluetoothAdapter.ACTION_DISCOVERY_FINISHED == intent.action) {
                status.text = "Discovery finished"
            }
        }
    }

    override fun onCreate(state: Bundle?) {
        super.onCreate(state)
        status = TextView(this).apply { text = "Ecosys starting…" }
        devices = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }

        val discoverable = Button(this).apply {
            text = "Make this device discoverable"
            setOnClickListener {
                startActivity(Intent(BluetoothAdapter.ACTION_REQUEST_DISCOVERABLE).apply {
                    putExtra(BluetoothAdapter.EXTRA_DISCOVERABLE_DURATION, 300)
                })
            }
        }
        val scan = Button(this).apply {
            text = "Find nearby devices"
            setOnClickListener { startDiscovery() }
        }
        val send = Button(this).apply {
            text = "Send Hello"
            setOnClickListener {
                transport?.send(EcosysMessage(
                    "hello", "android-${Build.MODEL}",
                    payload = JSONObject().put("deviceName", Build.MODEL).put("platform", "android")
                ))
            }
        }

        setContentView(LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(32, 32, 32, 32)
            addView(status)
            addView(discoverable)
            addView(scan)
            addView(send)
            addView(devices)
        })

        registerReceiverCompat(discoveryReceiver, IntentFilter().apply {
            addAction(BluetoothDevice.ACTION_FOUND)
            addAction(BluetoothAdapter.ACTION_DISCOVERY_FINISHED)
        })

        if (Build.VERSION.SDK_INT >= 31 &&
            checkSelfPermission(Manifest.permission.BLUETOOTH_CONNECT) != PackageManager.PERMISSION_GRANTED) {
            ActivityCompat.requestPermissions(this, arrayOf(
                Manifest.permission.BLUETOOTH_SCAN,
                Manifest.permission.BLUETOOTH_CONNECT,
                Manifest.permission.BLUETOOTH_ADVERTISE
            ), 100)
            return
        }
        startBluetooth()
    }

    @SuppressLint("MissingPermission")
    private fun startBluetooth() {
        val adapter = getSystemService(BluetoothManager::class.java)?.adapter
        if (adapter == null) {
            status.text = "Bluetooth is unavailable"
            return
        }
        transport = BluetoothTransport(adapter,
            { message -> runOnUiThread { status.text = "Received: ${message.type}" } },
            { state -> runOnUiThread { status.text = state } }
        )
        transport?.startServer()
    }

    @SuppressLint("MissingPermission")
    private fun startDiscovery() {
        val adapter = getSystemService(BluetoothManager::class.java)?.adapter ?: return
        devices.removeAllViews()
        status.text = "Discovering nearby Bluetooth devices…"
        if (adapter.isDiscovering) adapter.cancelDiscovery()
        adapter.startDiscovery()
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<String>, results: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, results)
        if (requestCode == 100 && results.all { it == PackageManager.PERMISSION_GRANTED }) startBluetooth()
        else status.text = "Bluetooth permissions are required"
    }

    override fun onDestroy() {
        runCatching { unregisterReceiver(discoveryReceiver) }
        transport?.close()
        super.onDestroy()
    }
}

private inline fun <reified T : android.os.Parcelable> Intent.getParcelableExtraCompat(name: String): T? =
    if (Build.VERSION.SDK_INT >= 33) getParcelableExtra(name, T::class.java)
    else @Suppress("DEPRECATION") getParcelableExtra(name)

private fun ComponentActivity.registerReceiverCompat(receiver: BroadcastReceiver, filter: IntentFilter) {
    if (Build.VERSION.SDK_INT >= 33) registerReceiver(receiver, filter, Context.RECEIVER_NOT_EXPORTED)
    else @Suppress("DEPRECATION") registerReceiver(receiver, filter)
}
