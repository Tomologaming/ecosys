package com.tomologaming.ecosys

import android.Manifest
import android.bluetooth.BluetoothManager
import android.content.pm.PackageManager
import android.os.Bundle
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.core.app.ActivityCompat
import org.json.JSONObject

class MainActivity : ComponentActivity() {
    private lateinit var status: TextView
    private var transport: BluetoothTransport? = null
    override fun onCreate(state: Bundle?) {
        super.onCreate(state)
        status=TextView(this).apply { text="Ecosys starting…" }
        val send=Button(this).apply { text="Send Hello"; setOnClickListener { transport?.send(EcosysMessage("hello","android-${android.os.Build.MODEL}",payload=JSONObject().put("deviceName",android.os.Build.MODEL).put("platform","android"))) } }
        setContentView(LinearLayout(this).apply { orientation=LinearLayout.VERTICAL; setPadding(32,32,32,32); addView(status); addView(send) })
        if (android.os.Build.VERSION.SDK_INT>=31 && checkSelfPermission(Manifest.permission.BLUETOOTH_CONNECT)!=PackageManager.PERMISSION_GRANTED) { ActivityCompat.requestPermissions(this,arrayOf(Manifest.permission.BLUETOOTH_SCAN,Manifest.permission.BLUETOOTH_CONNECT,Manifest.permission.BLUETOOTH_ADVERTISE),100); return }
        startBluetooth()
    }
    private fun startBluetooth() { val adapter=getSystemService(BluetoothManager::class.java)?.adapter ?: return; transport=BluetoothTransport(adapter,{m->runOnUiThread{status.text="Received: ${m.type}"}},{s->runOnUiThread{status.text=s}}); transport?.startServer() }
    override fun onDestroy(){transport?.close();super.onDestroy()}
}
