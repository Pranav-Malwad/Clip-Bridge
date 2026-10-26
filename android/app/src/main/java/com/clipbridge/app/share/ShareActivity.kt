package com.clipbridge.app.share

import android.app.Activity
import android.content.Intent
import android.os.Bundle
import android.widget.Toast
import androidx.core.content.ContextCompat
import com.clipbridge.app.pairing.PairingStore
import com.clipbridge.app.sync.SyncService

class ShareActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val text = SharedTextValidator.extract(intent.action, intent.type, intent.getCharSequenceExtra(Intent.EXTRA_TEXT))
        when {
            text == null -> Toast.makeText(this, "ClipBridge can only send text up to 256 KiB.", Toast.LENGTH_LONG).show()
            PairingStore(this).load() == null -> Toast.makeText(this, "Open ClipBridge and pair your PC first.", Toast.LENGTH_LONG).show()
            else -> {
                val serviceIntent = Intent(this, SyncService::class.java)
                    .setAction(SyncService.ACTION_SEND_TEXT)
                    .putExtra(SyncService.EXTRA_TEXT, text)
                ContextCompat.startForegroundService(this, serviceIntent)
                Toast.makeText(this, "Sending to PC", Toast.LENGTH_SHORT).show()
            }
        }
        finish()
    }
}
