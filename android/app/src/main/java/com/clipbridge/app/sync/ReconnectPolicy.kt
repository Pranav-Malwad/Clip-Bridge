package com.clipbridge.app.sync

object ReconnectPolicy {
    fun shouldReconnect(running: Boolean, pauseUntil: Long, now: Long = System.currentTimeMillis()): Boolean =
        running && pauseUntil <= now
}
