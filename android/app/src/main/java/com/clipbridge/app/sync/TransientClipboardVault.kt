package com.clipbridge.app.sync

import java.util.UUID

class TransientClipboardVault(
    private val ttlMillis: Long = 300_000,
    private val clock: () -> Long = System::currentTimeMillis
) {
    private val values = mutableMapOf<String, Pair<String, Long>>()

    @Synchronized fun put(text: String): String {
        purge()
        val token = UUID.randomUUID().toString()
        values[token] = text to (clock() + ttlMillis)
        return token
    }

    @Synchronized fun take(token: String): String? {
        val item = values.remove(token) ?: return null
        return item.first.takeIf { clock() <= item.second }
    }

    @Synchronized fun discard(token: String) { values.remove(token) }
    @Synchronized fun clear() { values.clear() }

    private fun purge() {
        val now = clock()
        values.entries.removeAll { now > it.value.second }
    }
}

object ClipboardVault {
    val instance = TransientClipboardVault()
}
