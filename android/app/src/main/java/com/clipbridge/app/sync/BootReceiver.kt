package com.clipbridge.app.sync

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.core.content.ContextCompat
import com.clipbridge.app.pairing.PairingStore

class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val store = PairingStore(context)
        if (intent.action == Intent.ACTION_BOOT_COMPLETED && store.load() != null && store.isSyncEnabled()) {
            ContextCompat.startForegroundService(context, Intent(context, SyncService::class.java).setAction(SyncService.ACTION_START))
        }
    }
}
