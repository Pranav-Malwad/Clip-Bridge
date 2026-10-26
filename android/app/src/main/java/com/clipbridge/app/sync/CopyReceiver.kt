package com.clipbridge.app.sync

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.widget.Toast

class CopyReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val text = intent.getStringExtra(EXTRA_TEXT) ?: return
        ClipboardWriter.write(context, text)
        Toast.makeText(context, "Copied from Windows", Toast.LENGTH_SHORT).show()
    }

    companion object {
        const val EXTRA_TEXT = "com.clipbridge.app.extra.TEXT"
    }
}
