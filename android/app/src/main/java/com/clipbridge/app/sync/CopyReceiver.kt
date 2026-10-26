package com.clipbridge.app.sync

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.widget.Toast

class CopyReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val token = intent.getStringExtra(EXTRA_TOKEN) ?: return
        val text = ClipboardVault.instance.take(token) ?: return
        ClipboardWriter.write(context, text)
        Toast.makeText(context, "Copied from Windows", Toast.LENGTH_SHORT).show()
    }

    companion object {
        const val EXTRA_TOKEN = "com.clipbridge.app.extra.TOKEN"
    }
}
