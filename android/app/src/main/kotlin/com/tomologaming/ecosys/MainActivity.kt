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
import android.graphics.Color
import android.graphics.Typeface
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.View
import android.widget.Button
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.core.app.ActivityCompat
import org.json.JSONObject

class MainActivity : ComponentActivity() {
    private lateinit var status: TextView
    private lateinit var devices: LinearLayout
    private lateinit var connectionDot: View
    private lateinit var connectionLabel: TextView
    private var transport: BluetoothTransport? = null

    private val discoveryReceiver = object : BroadcastReceiver() {
        @SuppressLint("MissingPermission")
        override fun onReceive(context: Context, intent: Intent) {
            if (BluetoothDevice.ACTION_FOUND == intent.action) {
                val device = intent.getParcelableExtraCompat<BluetoothDevice>(BluetoothDevice.EXTRA_DEVICE) ?: return
                addDevice(device)
            } else if (BluetoothAdapter.ACTION_DISCOVERY_FINISHED == intent.action) {
                status.text = "Scan complete"
            }
        }
    }

    override fun onCreate(state: Bundle?) {
        super.onCreate(state)
        buildUi()

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

    private fun buildUi() {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(Color.rgb(247, 248, 250))
        }

        val scroll = ScrollView(this).apply {
            isFillViewport = true
            addView(LinearLayout(this@MainActivity).apply {
                orientation = LinearLayout.VERTICAL
                setPadding(dp(20), dp(24), dp(20), dp(28))

                addView(TextView(this@MainActivity).apply {
                    text = "Ecosys"
                    textSize = 32f
                    setTextColor(Color.rgb(22, 24, 29))
                    typeface = Typeface.DEFAULT_BOLD
                }, lp())

                addView(TextView(this@MainActivity).apply {
                    text = "Private. Direct. Yours."
                    textSize = 15f
                    setTextColor(Color.rgb(105, 110, 120))
                }, lp(top = 2))

                val statusCard = LinearLayout(this@MainActivity).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    setPadding(dp(16), dp(14), dp(16), dp(14))
                    background = rounded(Color.WHITE, 20)
                }
                connectionDot = View(this@MainActivity).apply {
                    background = circle(Color.rgb(155, 160, 170))
                }
                statusCard.addView(connectionDot, LinearLayout.LayoutParams(dp(10), dp(10)))
                connectionLabel = TextView(this@MainActivity).apply {
                    text = "Bluetooth starting…"
                    textSize = 14f
                    setTextColor(Color.rgb(55, 59, 68))
                    setPadding(dp(10), 0, 0, 0)
                }
                statusCard.addView(connectionLabel, lp(weight = 1f))
                addView(statusCard, lp(top = 20))

                addView(sectionTitle("This device"), lp(top = 24))
                addView(deviceCard(), lp(top = 8))

                addView(sectionTitle("Nearby devices"), lp(top = 24))
                status = TextView(this@MainActivity).apply {
                    text = "No devices found yet"
                    textSize = 14f
                    setTextColor(Color.rgb(105, 110, 120))
                }
                addView(status, lp(top = 8))

                devices = LinearLayout(this@MainActivity).apply {
                    orientation = LinearLayout.VERTICAL
                }
                addView(devices, lp(top = 8))

                val scan = actionButton("Find nearby devices", true)
                scan.setOnClickListener { startDiscovery() }
                addView(scan, lp(top = 12))

                val discoverable = actionButton("Make this device discoverable", false)
                discoverable.setOnClickListener {
                    startActivity(Intent(BluetoothAdapter.ACTION_REQUEST_DISCOVERABLE).apply {
                        putExtra(BluetoothAdapter.EXTRA_DISCOVERABLE_DURATION, 300)
                    })
                }
                addView(discoverable, lp(top = 8))

                addView(sectionTitle("Quick test"), lp(top = 24))
                val send = actionButton("Send Hello", false)
                send.setOnClickListener {
                    transport?.send(EcosysMessage(
                        "hello", "android-\${Build.MODEL}",
                        payload = JSONObject().put("deviceName", Build.MODEL).put("platform", "android")
                    ))
                    status.text = "Hello sent"
                }
                addView(send, lp(top = 8))

                addView(TextView(this@MainActivity).apply {
                    text = "Ecosys connects your devices directly. Your data stays between your devices."
                    textSize = 12f
                    setTextColor(Color.rgb(125, 129, 138))
                    gravity = Gravity.CENTER
                    setPadding(dp(12), dp(24), dp(12), 0)
                }, lp())
            })
        }
        root.addView(scroll, LinearLayout.LayoutParams(-1, 0, 1f))
        setContentView(root)
    }

    private fun deviceCard(): View {
        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(18), dp(18), dp(18), dp(18))
            background = rounded(Color.WHITE, 20)
        }
        card.addView(TextView(this).apply {
            text = Build.MODEL
            textSize = 20f
            typeface = Typeface.DEFAULT_BOLD
            setTextColor(Color.rgb(22, 24, 29))
        })
        card.addView(TextView(this).apply {
            text = "Android • This device"
            textSize = 14f
            setTextColor(Color.rgb(105, 110, 120))
            setPadding(0, dp(4), 0, 0)
        })
        return card
    }

    @SuppressLint("MissingPermission")
    private fun addDevice(device: BluetoothDevice) {
        val name = device.name ?: "Unknown device"
        if (devices.childCount == 0) status.text = "Devices found"
        devices.addView(LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(dp(16), dp(14), dp(10), dp(14))
            background = rounded(Color.WHITE, 16)

            addView(TextView(this@MainActivity).apply {
                text = name
                textSize = 16f
                setTextColor(Color.rgb(35, 38, 45))
            }, lp(weight = 1f))

            addView(Button(this@MainActivity).apply {
                text = "Connect"
                isAllCaps = false
                setOnClickListener {
                    status.text = "Connecting to \$name…"
                    transport?.connect(device)
                }
            }, lp(width = -2))
        }, lp(bottom = 8))
    }

    @SuppressLint("MissingPermission")
    private fun startBluetooth() {
        val adapter = getSystemService(BluetoothManager::class.java)?.adapter
        if (adapter == null) {
            setConnectionState("Bluetooth unavailable", false)
            return
        }
        transport = BluetoothTransport(adapter,
            { message -> runOnUiThread {
                status.text = "Received: \${message.type}"
                setConnectionState("Connected", true)
            } },
            { state -> runOnUiThread {
                status.text = state
                setConnectionState(state, state.contains("connected", true))
            } }
        )
        transport?.startServer()
        setConnectionState("Ready for nearby devices", true)
    }

    @SuppressLint("MissingPermission")
    private fun startDiscovery() {
        val adapter = getSystemService(BluetoothManager::class.java)?.adapter ?: return
        devices.removeAllViews()
        status.text = "Scanning for nearby devices…"
        if (adapter.isDiscovering) adapter.cancelDiscovery()
        adapter.startDiscovery()
    }

    private fun setConnectionState(text: String, connected: Boolean) {
        connectionLabel.text = text
        connectionDot.background = circle(
            if (connected) Color.rgb(49, 180, 106) else Color.rgb(155, 160, 170)
        )
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<String>, results: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, results)
        if (requestCode == 100 && results.all { it == PackageManager.PERMISSION_GRANTED }) startBluetooth()
        else setConnectionState("Bluetooth permissions required", false)
    }

    override fun onDestroy() {
        runCatching { unregisterReceiver(discoveryReceiver) }
        transport?.close()
        super.onDestroy()
    }

    private fun sectionTitle(text: String) = TextView(this).apply {
        this.text = text
        textSize = 18f
        typeface = Typeface.DEFAULT_BOLD
        setTextColor(Color.rgb(35, 38, 45))
    }

    private fun actionButton(text: String, primary: Boolean) = Button(this).apply {
        this.text = text
        isAllCaps = false
        textSize = 15f
        minHeight = dp(50)
        if (primary) {
            setTextColor(Color.WHITE)
            background = rounded(Color.rgb(42, 91, 220), 16)
        } else {
            setTextColor(Color.rgb(42, 91, 220))
            background = rounded(Color.WHITE, 16)
        }
    }

    private fun rounded(color: Int, radius: Int) =
        android.graphics.drawable.GradientDrawable().apply {
            setColor(color)
            cornerRadius = dp(radius).toFloat()
        }

    private fun circle(color: Int) =
        android.graphics.drawable.GradientDrawable().apply {
            shape = android.graphics.drawable.GradientDrawable.OVAL
            setColor(color)
        }

    private fun lp(top: Int = 0, bottom: Int = 0, width: Int = -1, weight: Float = 0f) =
        LinearLayout.LayoutParams(width, if (weight > 0f) 0 else -2, weight).apply {
            if (top != 0 || bottom != 0) setMargins(0, dp(top), 0, dp(bottom))
        }

    private fun dp(value: Int) = (value * resources.displayMetrics.density).toInt()
}

private inline fun <reified T : android.os.Parcelable> Intent.getParcelableExtraCompat(name: String): T? =
    if (Build.VERSION.SDK_INT >= 33) getParcelableExtra(name, T::class.java)
    else @Suppress("DEPRECATION") getParcelableExtra(name)

private fun ComponentActivity.registerReceiverCompat(receiver: BroadcastReceiver, filter: IntentFilter) {
    if (Build.VERSION.SDK_INT >= 33) registerReceiver(receiver, filter, Context.RECEIVER_NOT_EXPORTED)
    else @Suppress("DEPRECATION") registerReceiver(receiver, filter)
}
