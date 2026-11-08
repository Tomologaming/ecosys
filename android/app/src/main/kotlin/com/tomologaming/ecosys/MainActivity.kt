package com.tomologaming.ecosys

import android.Manifest
import android.annotation.SuppressLint
import android.app.AlertDialog
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import android.bluetooth.BluetoothManager
import android.content.BroadcastReceiver
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.SharedPreferences
import android.content.pm.PackageManager
import android.content.res.ColorStateList
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.Drawable
import android.graphics.drawable.GradientDrawable
import android.graphics.drawable.RippleDrawable
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.ContextCompat
import org.json.JSONObject
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

class MainActivity : ComponentActivity() {

    private lateinit var statusText: TextView
    private lateinit var statusDot: View
    private lateinit var deviceCountText: TextView
    private lateinit var devicesContainer: LinearLayout
    private lateinit var connectionTitle: TextView
    private lateinit var connectionDetail: TextView
    private lateinit var activityText: TextView
    private lateinit var deviceIdText: TextView
    private lateinit var scanButton: Button
    private lateinit var sendButton: Button
    private lateinit var disconnectButton: Button
    private lateinit var clipboardDetail: TextView
    private lateinit var clipboardToggle: Button
    private lateinit var clipboardSendButton: Button
    private lateinit var knownPeersDetail: TextView
    private lateinit var knownPeersContainer: LinearLayout

    private lateinit var prefs: SharedPreferences
    private var transport: BluetoothTransport? = null
    private var identity: DeviceIdentity? = null
    private var peerId: String? = null
    private val knownPeers = KnownPeers()
    private val clipboardSync = ClipboardSync()
    private var clipboardManager: ClipboardManager? = null
    private val clipboardListener = ClipboardManager.OnPrimaryClipChangedListener {
        runOnUiThread { shareLocalClipboard() }
    }
    private val knownDevices = LinkedHashMap<String, BluetoothDevice>()
    private val log = ArrayDeque<String>()
    private var receiverRegistered = false
    private var hasScanned = false

    private val permissionsLauncher =
        registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { result ->
            if (result.values.all { it }) {
                startBluetooth()
            } else {
                setStatus("Bluetooth-Berechtigungen fehlen")
            }
        }

    private val enableBluetoothLauncher =
        registerForActivityResult(ActivityResultContracts.StartActivityForResult()) {
            startBluetooth()
        }

    private val discoveryReceiver = object : BroadcastReceiver() {
        @SuppressLint("MissingPermission")
        override fun onReceive(context: Context, intent: Intent) {
            when (intent.action) {
                BluetoothDevice.ACTION_FOUND -> {
                    val device = intent.getParcelableExtraCompat<BluetoothDevice>(BluetoothDevice.EXTRA_DEVICE)
                        ?: return
                    val address = device.address ?: return
                    if (knownDevices.containsKey(address)) return
                    knownDevices[address] = device
                    rebuildDeviceList()
                    deviceCountText.text = "${knownDevices.size} Gerät${if (knownDevices.size == 1) "" else "e"} gefunden"
                }

                BluetoothAdapter.ACTION_DISCOVERY_STARTED -> {
                    hasScanned = true
                    scanButton.isEnabled = false
                    scanButton.text = "Suche …"
                    setStatus("Suche nach Geräten in der Nähe …")
                }

                BluetoothAdapter.ACTION_DISCOVERY_FINISHED -> {
                    scanButton.isEnabled = true
                    scanButton.text = "Suchen"
                    setStatus(
                        if (knownDevices.isEmpty()) "Keine Geräte gefunden"
                        else "${knownDevices.size} Gerät${if (knownDevices.size == 1) "" else "e"} gefunden"
                    )
                }

                BluetoothAdapter.ACTION_STATE_CHANGED -> {
                    if (intent.getIntExtra(BluetoothAdapter.EXTRA_STATE, -1) == BluetoothAdapter.STATE_ON) {
                        startBluetooth()
                    } else {
                        setStatus("Bluetooth ist ausgeschaltet")
                    }
                }
            }
        }
    }

    override fun onCreate(state: Bundle?) {
        super.onCreate(state)
        prefs = getSharedPreferences(PREFS, MODE_PRIVATE)
        knownPeers.load(prefs.getString(PEERS_KEY, null))
        buildUi()

        identity = runCatching { DeviceIdentity() }.getOrNull()
        deviceIdText.text = identity?.deviceId?.let(::shortId) ?: "nicht verfügbar"

        clipboardManager = getSystemService(ClipboardManager::class.java)
        clipboardManager?.addPrimaryClipChangedListener(clipboardListener)
        updateClipboardUi()

        registerDiscoveryReceiver()
        addLog("Ecosys gestartet")

        if (hasRequiredPermissions()) {
            startBluetooth()
        } else {
            permissionsLauncher.launch(requiredPermissions())
        }
    }

    // ---------------------------------------------------------------- UI

    private fun buildUi() {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(color(R.color.ecosys_page))
        }

        root.addView(buildHero())

        val content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(18.dp, 18.dp, 18.dp, 28.dp)
        }
        root.addView(content)

        content.addView(sectionLabel("DIESES GERÄT"), topMargin(0))
        content.addView(buildIdentityCard(), topMargin(8))

        content.addView(buildNearbyHeader(), topMargin(22))
        content.addView(buildDeviceCard(), topMargin(8))

        val discoverable = actionButton("Dieses Gerät sichtbar machen", primary = false)
        discoverable.setOnClickListener { requestDiscoverable() }
        content.addView(discoverable, topMargin(10))

        content.addView(buildHintCard(), topMargin(18))

        content.addView(sectionLabel("VERBINDUNG"), topMargin(22))
        content.addView(buildConnectionCard(), topMargin(8))

        content.addView(sectionLabel("ZWISCHENABLAGE"), topMargin(22))
        content.addView(buildClipboardCard(), topMargin(8))

        content.addView(sectionLabel("BEKANNTE GERÄTE"), topMargin(22))
        content.addView(buildKnownPeersCard(), topMargin(8))

        content.addView(sectionLabel("AKTIVITÄT"), topMargin(22))
        content.addView(buildActivityCard(), topMargin(8))

        content.addView(
            label(
                "Direkt zwischen deinen Geräten · keine Cloud · keine Server",
                11f,
                color(R.color.ecosys_faint)
            ).apply {
                gravity = Gravity.CENTER
                setPadding(0, 22.dp, 0, 0)
            },
            topMargin(0)
        )

        val scroll = ScrollView(this).apply {
            isFillViewport = true
            setBackgroundColor(color(R.color.ecosys_page))
            addView(root)
        }
        setContentView(scroll)
    }

    private fun buildHero(): View {
        val hero = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(22.dp, 26.dp, 22.dp, 22.dp)
            background = GradientDrawable(
                GradientDrawable.Orientation.TL_BR,
                intArrayOf(color(R.color.ecosys_navy), color(R.color.ecosys_teal))
            ).apply {
                val radius = 28.dp.toFloat()
                cornerRadii = floatArrayOf(0f, 0f, 0f, 0f, radius, radius, radius, radius)
            }
        }

        val brand = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }
        brand.addView(ImageView(this).apply {
            setImageResource(R.mipmap.ic_launcher)
            scaleType = ImageView.ScaleType.FIT_CENTER
        }, LinearLayout.LayoutParams(56.dp, 56.dp))

        val brandText = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(14.dp, 0, 0, 0)
        }
        brandText.addView(label("Ecosys", 28f, Color.WHITE, medium = true))
        brandText.addView(
            label("PRIVAT. DIREKT. DEINS.", 9f, 0xFF9ED6C3.toInt(), medium = true).apply {
                letterSpacing = 0.16f
                setPadding(0, 2.dp, 0, 0)
            }
        )
        brand.addView(brandText)
        hero.addView(brand)

        hero.addView(
            label("Geräte verbinden sich direkt – ohne Cloud, ohne Konto.", 13f, 0xFFCFE0E4.toInt())
                .apply { setPadding(0, 14.dp, 0, 0) }
        )

        val statusRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(0, 16.dp, 0, 0)
        }
        statusDot = View(this).apply {
            background = GradientDrawable().apply {
                shape = GradientDrawable.OVAL
                setColor(0xFF8496A0.toInt())
            }
        }
        statusRow.addView(statusDot, LinearLayout.LayoutParams(9.dp, 9.dp))
        statusText = label("Bluetooth wird geprüft …", 13f, 0xFFE8F1F3.toInt())
        statusRow.addView(statusText, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply {
            leftMargin = 10.dp
        })
        hero.addView(statusRow)

        return hero
    }

    private fun buildIdentityCard(): View {
        val card = cardContainer()
        val row = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }

        row.addView(ImageView(this).apply {
            setImageResource(R.mipmap.ic_launcher)
            scaleType = ImageView.ScaleType.FIT_CENTER
        }, LinearLayout.LayoutParams(42.dp, 42.dp))

        val texts = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(14.dp, 0, 8.dp, 0)
        }
        texts.addView(label(Build.MODEL, 16f, color(R.color.ecosys_ink), medium = true))
        texts.addView(label("Android · Ecosys Bluetooth", 12f, color(R.color.ecosys_muted)))
        row.addView(texts, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))

        val idColumn = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.END
        }
        idColumn.addView(
            label("GERÄTE-ID", 9f, color(R.color.ecosys_faint), medium = true)
                .apply { letterSpacing = 0.08f }
        )
        deviceIdText = label("…", 10f, color(R.color.ecosys_muted)).apply {
            typeface = Typeface.MONOSPACE
        }
        idColumn.addView(deviceIdText)
        row.addView(idColumn)

        card.addView(row)
        return card
    }

    private fun buildNearbyHeader(): View {
        val row = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }

        val texts = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }
        texts.addView(sectionLabel("ECOSYS-GERÄTE IN DER NÄHE"))
        deviceCountText = label("Noch nicht gesucht", 12f, color(R.color.ecosys_muted))
            .apply { setPadding(0, 2.dp, 0, 0) }
        texts.addView(deviceCountText)
        row.addView(texts, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))

        scanButton = actionButton("Suchen", primary = true)
        scanButton.setOnClickListener { startDiscovery() }
        row.addView(scanButton)
        return row
    }

    private fun buildDeviceCard(): View {
        val card = cardContainer()
        devicesContainer = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        card.addView(devicesContainer)
        rebuildDeviceList()
        return card
    }

    private fun buildHintCard(): View {
        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(16.dp, 14.dp, 16.dp, 14.dp)
            background = surface(color(R.color.ecosys_mint), 14, color(R.color.ecosys_mint_line))
        }
        card.addView(label("Beide Geräte brauchen Bluetooth", 13f, 0xFF204E3E.toInt(), medium = true))
        card.addView(
            label(
                "Beim ersten Verbinden kann eine Kopplung bestätigt werden müssen.",
                12f,
                0xFF486C5C.toInt()
            ).apply { setPadding(0, 4.dp, 0, 0) }
        )
        return card
    }

    private fun buildConnectionCard(): View {
        val card = cardContainer()

        connectionTitle = label("Kein Gerät verbunden", 15f, color(R.color.ecosys_ink), medium = true)
        connectionDetail = label(
            "Wähle oben ein Gerät aus, um eine Verbindung aufzubauen.",
            12f,
            color(R.color.ecosys_muted)
        ).apply { setPadding(0, 4.dp, 0, 0) }

        card.addView(connectionTitle)
        card.addView(connectionDetail)

        val buttons = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }

        sendButton = actionButton("Hello senden", primary = true)
        sendButton.setOnClickListener { sendHello() }
        buttons.addView(sendButton, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))

        disconnectButton = actionButton("Trennen", primary = false)
        disconnectButton.setOnClickListener { disconnect() }
        buttons.addView(disconnectButton, LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT,
            ViewGroup.LayoutParams.WRAP_CONTENT
        ).apply { leftMargin = 8.dp })

        card.addView(buttons, topMargin(14))
        updateConnectionButtons(false)
        return card
    }

    private fun buildClipboardCard(): View {
        val card = cardContainer()

        card.addView(label("Text zwischen den Geräten teilen", 15f, color(R.color.ecosys_ink), medium = true))
        clipboardDetail = label(
            "Verbinde zuerst ein Gerät.",
            12f,
            color(R.color.ecosys_muted)
        ).apply { setPadding(0, 4.dp, 0, 0) }
        card.addView(clipboardDetail)

        val buttons = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }

        clipboardToggle = actionButton("Teilen aktivieren", primary = false)
        clipboardToggle.setOnClickListener { toggleClipboardSharing() }
        buttons.addView(clipboardToggle, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))

        clipboardSendButton = actionButton("Jetzt senden", primary = true)
        clipboardSendButton.setOnClickListener { shareLocalClipboard(force = true) }
        buttons.addView(clipboardSendButton, LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT,
            ViewGroup.LayoutParams.WRAP_CONTENT
        ).apply { leftMargin = 8.dp })

        card.addView(buttons, topMargin(14))
        return card
    }

    private fun buildKnownPeersCard(): View {
        val card = cardContainer()

        card.addView(label("Frühere Verbindungen", 15f, color(R.color.ecosys_ink), medium = true))
        knownPeersDetail = label("", 12f, color(R.color.ecosys_muted))
            .apply { setPadding(0, 4.dp, 0, 0) }
        card.addView(knownPeersDetail)

        knownPeersContainer = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        card.addView(knownPeersContainer, topMargin(6))

        val forget = actionButton("Verlauf leeren", primary = false)
        forget.setOnClickListener { forgetPeerHistory() }
        card.addView(forget, topMargin(12))

        renderKnownPeers()
        return card
    }

    private fun buildActivityCard(): View {
        val card = cardContainer()
        activityText = label("Noch keine Aktivität.", 10f, color(R.color.ecosys_muted)).apply {
            typeface = Typeface.MONOSPACE
            setLineSpacing(3.dp.toFloat(), 1f)
        }
        card.addView(activityText)
        return card
    }

    // ------------------------------------------------------------ helpers

    private fun cardContainer(): LinearLayout = LinearLayout(this).apply {
        orientation = LinearLayout.VERTICAL
        setPadding(16.dp, 16.dp, 16.dp, 16.dp)
        background = surface(color(R.color.ecosys_card), 18, color(R.color.ecosys_line))
    }

    private fun label(text: String, size: Float, textColor: Int, medium: Boolean = false): TextView =
        TextView(this).apply {
            this.text = text
            textSize = size
            setTextColor(textColor)
            typeface = if (medium) Typeface.create("sans-serif-medium", Typeface.NORMAL)
            else Typeface.create("sans-serif", Typeface.NORMAL)
        }

    private fun sectionLabel(text: String): TextView =
        label(text, 10.5f, color(R.color.ecosys_muted), medium = true).apply {
            letterSpacing = 0.12f
        }

    private fun actionButton(text: String, primary: Boolean): Button = Button(this).apply {
        this.text = text
        isAllCaps = false
        textSize = 13f
        typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
        minHeight = 0
        minimumHeight = 0
        minWidth = 0
        minimumWidth = 0
        stateListAnimator = null
        setPadding(18.dp, 11.dp, 18.dp, 11.dp)
        if (primary) {
            setTextColor(Color.WHITE)
            background = rippleSurface(color(R.color.ecosys_green), 12, null, 0x40FFFFFF)
        } else {
            setTextColor(color(R.color.ecosys_blue))
            background = rippleSurface(color(R.color.ecosys_card), 12, color(R.color.ecosys_line))
        }
    }

    private fun rebuildDeviceList() {
        devicesContainer.removeAllViews()

        if (knownDevices.isEmpty()) {
            devicesContainer.addView(
                label(
                    if (hasScanned) "Keine Geräte gefunden"
                    else "Noch nicht gesucht – tippe auf „Suchen“.",
                    13f,
                    color(R.color.ecosys_muted)
                ).apply { setPadding(2.dp, 6.dp, 2.dp, 6.dp) }
            )
            return
        }

        var first = true
        for (device in knownDevices.values) {
            if (!first) {
                devicesContainer.addView(View(this).apply { setBackgroundColor(color(R.color.ecosys_line)) },
                    LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 1.dp).apply {
                        topMargin = 10.dp
                        bottomMargin = 10.dp
                    })
            }
            first = false
            devicesContainer.addView(deviceRow(device))
        }
    }

    @SuppressLint("MissingPermission")
    private fun deviceRow(device: BluetoothDevice): View {
        val row = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }

        val texts = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        texts.addView(
            label(device.name ?: "Unbekanntes Gerät", 14f, color(R.color.ecosys_ink), medium = true)
        )
        texts.addView(label("Bluetooth Classic · RFCOMM", 11f, color(R.color.ecosys_muted)))
        row.addView(texts, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))

        val connect = actionButton("Verbinden", primary = false)
        connect.setOnClickListener {
            transport?.connect(device)
            setStatus("Verbinde mit ${device.name ?: "Gerät"} …")
        }
        row.addView(connect)
        return row
    }

    // --------------------------------------------------------- behaviour

    @SuppressLint("MissingPermission")
    private fun startBluetooth() {
        val adapter = getSystemService(BluetoothManager::class.java)?.adapter
        if (adapter == null) {
            setStatus("Bluetooth wird nicht unterstützt")
            return
        }
        if (!adapter.isEnabled) {
            setStatus("Bluetooth ist ausgeschaltet")
            return
        }
        if (transport == null) {
            transport = BluetoothTransport(
                adapter = adapter,
                onMessage = { message ->
                    runOnUiThread {
                        when (message.type) {
                            "clipboard.set" -> handleIncomingClipboard(message.payload)
                            "hello" -> handleHello(message)
                            else -> {
                                setStatus("Empfangen: ${message.type}")
                                val name = message.payload.optString("deviceName")
                                addLog("← ${message.type}${if (name.isEmpty()) "" else " · $name"}")
                            }
                        }
                    }
                },
                onState = { state ->
                    runOnUiThread {
                        setStatus(state)
                        addLog(state)
                    }
                },
                onConnected = { connected ->
                    runOnUiThread {
                        updateConnectionButtons(connected)
                        val now = transport?.peerName
                        connectionTitle.text = if (connected && now != null) now else "Kein Gerät verbunden"
                        connectionDetail.text = if (connected)
                            "Bluetooth Classic · Nachrichten werden direkt übertragen."
                        else "Wähle oben ein Gerät aus, um eine Verbindung aufzubauen."
                        setStatus(if (connected) "Verbunden mit ${now ?: "Gerät"}" else "Bereit für Geräte in der Nähe")
                        if (connected) {
                            announceClipboardState()
                        } else {
                            clipboardSync.setEnabled(false)
                            peerId = null
                        }
                        renderKnownPeers()
                        updateClipboardUi()
                    }
                }
            ).also { it.startServer() }
        } else {
            setStatus("Bereit für Geräte in der Nähe")
        }
    }

    @SuppressLint("MissingPermission")
    private fun startDiscovery() {
        val adapter = getSystemService(BluetoothManager::class.java)?.adapter ?: return
        if (!hasRequiredPermissions()) {
            permissionsLauncher.launch(requiredPermissions())
            return
        }
        if (!adapter.isEnabled) {
            enableBluetoothLauncher.launch(Intent(BluetoothAdapter.ACTION_REQUEST_ENABLE))
            return
        }

        knownDevices.clear()
        hasScanned = true
        rebuildDeviceList()
        deviceCountText.text = "Suche läuft …"
        if (adapter.isDiscovering) adapter.cancelDiscovery()
        val started = adapter.startDiscovery()
        if (!started) setStatus("Suche konnte nicht gestartet werden")
    }

    @SuppressLint("MissingPermission")
    private fun requestDiscoverable() {
        if (!hasRequiredPermissions()) {
            permissionsLauncher.launch(requiredPermissions())
            return
        }
        startActivity(Intent(BluetoothAdapter.ACTION_REQUEST_DISCOVERABLE).apply {
            putExtra(BluetoothAdapter.EXTRA_DISCOVERABLE_DURATION, 300)
        })
    }

    private fun sendHello() {
        val active = transport
        if (active == null || !active.isConnected) {
            setStatus("Kein Gerät verbunden")
            return
        }
        val sender = identity?.deviceId ?: "android-${Build.MODEL}"
        val sent = active.send(EcosysMessage.hello(sender, Build.MODEL))
        setStatus(if (sent) "Hello gesendet" else "Senden fehlgeschlagen")
        addLog(if (sent) "→ hello gesendet" else "→ hello fehlgeschlagen")
    }

    private fun announceClipboardState() {
        val active = transport ?: return
        if (!active.isConnected) return
        active.send(EcosysMessage.clipboardState(senderId(), clipboardSync.enabled))
    }

    private fun disconnect() {
        clipboardSync.setEnabled(false)
        peerId = null
        transport?.disconnect()
        updateConnectionButtons(false)
        renderKnownPeers()
        updateClipboardUi()
    }

    // ---------------------------------------------------------- clipboard

    private fun senderId(): String = identity?.deviceId ?: "android-${Build.MODEL}"

    private fun localClipboardText(): String? {
        val clip = clipboardManager?.primaryClip ?: return null
        if (clip.itemCount == 0) return null
        return clip.getItemAt(0).coerceToText(this)?.toString()
    }

    private fun applyClipboard(text: String) {
        runCatching { clipboardManager?.setPrimaryClip(ClipData.newPlainText("Ecosys", text)) }
            .onFailure { addLog("Zwischenablage konnte nicht gesetzt werden: ${it.message}") }
    }

    /**
     * Sends the local clipboard when sharing is on. [force] is the explicit
     * "Jetzt senden" action and also re-sends a value that was already sent.
     */
    private fun shareLocalClipboard(force: Boolean = false) {
        if (!clipboardSync.enabled) {
            if (force) {
                setStatus("Zwischenablage ist aus")
                addLog("Zwischenablage: erst teilen aktivieren")
            }
            return
        }
        val active = transport
        if (active == null || !active.isConnected) {
            if (force) {
                setStatus("Kein Gerät verbunden")
                addLog("Zwischenablage braucht eine Verbindung")
            }
            return
        }

        if (force) clipboardSync.allowResend()
        val update = clipboardSync.outgoing(localClipboardText())
        if (update == null) {
            if (force) {
                setStatus("Nichts zu senden")
                addLog("Zwischenablage leer oder zu groß")
            }
            return
        }

        val sent = active.send(EcosysMessage.clipboardSet(senderId(), update))
        if (sent) {
            setStatus("Zwischenablage gesendet")
            addLog("→ clipboard.set · ${update.text.length} Zeichen")
        } else {
            addLog("→ clipboard.set fehlgeschlagen")
        }
    }

    private fun handleIncomingClipboard(payload: JSONObject) {
        val applied = clipboardSync.incoming(
            payload.optString("text"),
            payload.optString("hash"),
            payload.optInt("bytes", -1)
        )
        if (applied == null) {
            addLog("← clipboard.set ignoriert")
            return
        }
        applyClipboard(applied.text)
        setStatus("Zwischenablage empfangen")
        addLog("← clipboard.set · ${applied.text.length} Zeichen")
        updateClipboardUi()
    }

    private fun toggleClipboardSharing() {
        val active = transport
        if (active == null || !active.isConnected) {
            setStatus("Kein Gerät verbunden")
            addLog("Zwischenablage braucht eine Verbindung")
            return
        }

        if (clipboardSync.enabled) {
            clipboardSync.setEnabled(false)
            active.send(EcosysMessage.clipboardState(senderId(), false))
            setStatus("Teilen beendet")
            addLog("Zwischenablage: aus")
            updateClipboardUi()
            return
        }

        val id = peerId
        if (id == null) {
            setStatus("Gerät noch nicht erkannt")
            addLog("Zwischenablage: erst Hello senden")
            return
        }

        if (!knownPeers.isTrusted(id)) {
            val name = knownPeers.find(id)?.name ?: "Die Gegenstelle"
            AlertDialog.Builder(this)
                .setTitle("Gerät bestätigen")
                .setMessage(
                    "$name ist auf diesem Gerät noch nicht bestätigt.\n\n" +
                        "Die Zwischenablage läuft im Klartext über Bluetooth – die verschlüsselte " +
                        "Sitzung ist noch nicht fertig.\n\nGerät bestätigen und Teilen aktivieren?"
                )
                .setNegativeButton("Abbrechen", null)
                .setPositiveButton("Bestätigen") { _, _ ->
                    knownPeers.setTrusted(id, true)
                    savePeers()
                    addLog("$name bestätigt")
                    renderKnownPeers()
                    enableClipboardSharing(active)
                }
                .show()
            return
        }

        enableClipboardSharing(active)
    }

    private fun enableClipboardSharing(active: BluetoothTransport) {
        clipboardSync.setEnabled(true)
        active.send(EcosysMessage.clipboardState(senderId(), true))
        setStatus("Teilen aktiv")
        addLog("Zwischenablage: an")
        updateClipboardUi()
        shareLocalClipboard()
    }

    private fun stopClipboardSharing() {
        clipboardSync.setEnabled(false)
        val active = transport
        if (active != null && active.isConnected) {
            active.send(EcosysMessage.clipboardState(senderId(), false))
        }
        addLog("Zwischenablage: aus (Gerät nicht mehr bestätigt)")
        updateClipboardUi()
    }

    private fun updateClipboardUi() {
        if (!::clipboardDetail.isInitialized) return
        val connected = transport?.isConnected == true
        val sharing = clipboardSync.enabled
        val id = peerId
        val trusted = knownPeers.isTrusted(id)

        clipboardToggle.text = if (sharing) "Teilen beenden" else "Teilen aktivieren"
        clipboardToggle.isEnabled = connected
        clipboardToggle.alpha = if (connected) 1f else 0.45f
        clipboardSendButton.isEnabled = connected && sharing
        clipboardSendButton.alpha = if (connected && sharing) 1f else 0.45f

        clipboardDetail.text = when {
            !connected -> "Verbinde zuerst ein Gerät."
            id == null -> "Sende zuerst Hello, damit die Gegenstelle bekannt ist."
            sharing -> {
                val suffix = if (trusted) " (bestätigtes Gerät)." else "."
                "An · Text wird in beide Richtungen übertragen$suffix"
            }
            trusted -> "Aus · Gerät ist bestätigt, Teilen bleibt eine bewusste Aktion."
            else -> "Aus · Gerät ist nicht bestätigt. Aktivieren verlangt eine Freigabe."
        }
    }

    private fun updateConnectionButtons(connected: Boolean) {
        for (button in listOf(sendButton, disconnectButton)) {
            button.isEnabled = connected
            button.alpha = if (connected) 1f else 0.45f
        }
        updateClipboardUi()
        statusDot.background = GradientDrawable().apply {
            shape = GradientDrawable.OVAL
            setColor(if (connected) color(R.color.ecosys_green) else 0xFF8496A0.toInt())
        }
    }

    private fun setStatus(text: String) {
        if (::statusText.isInitialized) statusText.text = text
    }

    private fun addLog(message: String) {
        val stamp = SimpleDateFormat("HH:mm:ss", Locale.GERMANY).format(Date())
        log.addFirst("$stamp  $message")
        while (log.size > 7) log.removeLast()
        if (::activityText.isInitialized) activityText.text = log.joinToString("\n")
    }

    // ------------------------------------------------------ known peers

    private fun renderKnownPeers() {
        if (!::knownPeersContainer.isInitialized) return

        val peers = knownPeers.list()
        val trusted = peers.count { it.trusted }
        val plural = if (peers.size == 1) "" else "e"
        knownPeersDetail.text = when {
            peers.isEmpty() ->
                "Noch keine Geräte bekannt. Nach einem Hello trägt sich die Gegenstelle hier ein."
            trusted == 0 -> "${peers.size} Gerät$plural im Verlauf · keines bestätigt."
            else -> "${peers.size} Gerät$plural im Verlauf · $trusted bestätigt."
        }

        knownPeersContainer.removeAllViews()
        var first = true
        for (peer in peers) {
            if (!first) {
                knownPeersContainer.addView(
                    View(this).apply { setBackgroundColor(color(R.color.ecosys_line)) },
                    LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 1.dp).apply {
                        topMargin = 10.dp
                        bottomMargin = 10.dp
                    }
                )
            }
            first = false
            knownPeersContainer.addView(knownPeerRow(peer))
        }
    }

    private fun knownPeerRow(peer: KnownPeer): View {
        val row = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }

        val texts = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        texts.addView(label(peer.name, 14f, color(R.color.ecosys_ink), medium = true))
        val platform = when (peer.platform) {
            "android" -> "Android"
            "windows" -> "Windows"
            "ios" -> "iOS"
            "macos" -> "macOS"
            else -> "Unbekannt"
        }
        val seen = KnownPeers.describeLastSeen(System.currentTimeMillis(), peer.lastSeen)
        val parts = mutableListOf(platform, "zuletzt $seen")
        if (peer.trusted) parts.add("bestätigt")
        if (peer.id == peerId) parts.add("verbunden")
        texts.addView(label(parts.joinToString(" · "), 11f, color(R.color.ecosys_muted)))
        row.addView(texts, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))

        val toggle = actionButton(if (peer.trusted) "Entziehen" else "Vertrauen", primary = false)
        toggle.setOnClickListener { setPeerTrusted(peer, !peer.trusted) }
        row.addView(toggle)
        return row
    }

    private fun setPeerTrusted(peer: KnownPeer, trusted: Boolean) {
        if (!knownPeers.setTrusted(peer.id, trusted)) return
        savePeers()
        addLog(if (trusted) "${peer.name} bestätigt" else "Bestätigung für ${peer.name} entzogen")
        setStatus(if (trusted) "${peer.name} bestätigt" else "${peer.name} ist nicht mehr bestätigt")
        if (!trusted && peer.id == peerId && clipboardSync.enabled) stopClipboardSharing()
        renderKnownPeers()
        updateClipboardUi()
    }

    private fun forgetPeerHistory() {
        val removed = knownPeers.forgetUntrusted()
        if (removed == 0) {
            setStatus("Verlauf enthält nur bestätigte Geräte")
            addLog("Verlauf: nichts zu leeren")
            return
        }
        savePeers()
        renderKnownPeers()
        val plural = if (removed == 1) "" else "e"
        setStatus("$removed Gerät$plural vergessen")
        addLog("Verlauf geleert · $removed Gerät$plural")
    }

    private fun handleHello(message: EcosysMessage) {
        val name = message.payload.optString("deviceName").ifEmpty { transport?.peerName ?: "Gerät" }
        peerId = message.sender.ifEmpty { null }
        if (knownPeers.record(message.sender, name, message.payload.optString("platform"))) {
            savePeers()
            renderKnownPeers()
        }
        setStatus("Empfangen: hello · $name")
        addLog("← hello · $name")
        updateClipboardUi()
    }

    private fun savePeers() {
        prefs.edit().putString(PEERS_KEY, knownPeers.snapshot()).apply()
    }

    // -------------------------------------------------------- permissions

    private fun requiredPermissions(): Array<String> =
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            arrayOf(
                Manifest.permission.BLUETOOTH_SCAN,
                Manifest.permission.BLUETOOTH_CONNECT,
                Manifest.permission.BLUETOOTH_ADVERTISE
            )
        } else {
            arrayOf(Manifest.permission.ACCESS_FINE_LOCATION)
        }

    private fun hasRequiredPermissions(): Boolean =
        requiredPermissions().all {
            ContextCompat.checkSelfPermission(this, it) == PackageManager.PERMISSION_GRANTED
        }

    // ------------------------------------------------------------ dizayn

    private fun registerDiscoveryReceiver() {
        if (receiverRegistered) return
        val filter = IntentFilter().apply {
            addAction(BluetoothDevice.ACTION_FOUND)
            addAction(BluetoothAdapter.ACTION_DISCOVERY_STARTED)
            addAction(BluetoothAdapter.ACTION_DISCOVERY_FINISHED)
            addAction(BluetoothAdapter.ACTION_STATE_CHANGED)
        }
        ContextCompat.registerReceiver(this, discoveryReceiver, filter, ContextCompat.RECEIVER_NOT_EXPORTED)
        receiverRegistered = true
    }

    override fun onDestroy() {
        clipboardManager?.removePrimaryClipChangedListener(clipboardListener)
        if (receiverRegistered) {
            runCatching { unregisterReceiver(discoveryReceiver) }
            receiverRegistered = false
        }
        transport?.close()
        transport = null
        super.onDestroy()
    }

    private fun surface(fill: Int, radius: Int, stroke: Int? = null): GradientDrawable =
        GradientDrawable().apply {
            setColor(fill)
            cornerRadius = radius.dp.toFloat()
            if (stroke != null) setStroke(1.dp, stroke)
        }

    private fun rippleSurface(fill: Int, radius: Int, stroke: Int?, ripple: Int = 0x242F6FB0): Drawable =
        RippleDrawable(ColorStateList.valueOf(ripple), surface(fill, radius, stroke), null)

    private fun color(id: Int): Int = ContextCompat.getColor(this, id)

    private fun shortId(id: String): String =
        if (id.length > 14) "${id.take(10)}…${id.takeLast(4)}" else id

    private fun topMargin(top: Int) = LinearLayout.LayoutParams(
        ViewGroup.LayoutParams.MATCH_PARENT,
        ViewGroup.LayoutParams.WRAP_CONTENT
    ).apply { topMargin = top.dp }

    private val Int.dp: Int
        get() = (this * resources.displayMetrics.density).toInt()

    private companion object {
        const val PREFS = "ecosys"
        const val PEERS_KEY = "knownPeers"
    }
}

private inline fun <reified T : android.os.Parcelable> Intent.getParcelableExtraCompat(name: String): T? =
    if (Build.VERSION.SDK_INT >= 33) getParcelableExtra(name, T::class.java)
    else @Suppress("DEPRECATION") getParcelableExtra(name)
