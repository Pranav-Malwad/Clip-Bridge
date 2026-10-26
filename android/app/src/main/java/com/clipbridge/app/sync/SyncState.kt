package com.clipbridge.app.sync

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow

enum class ConnectionStatus { UNPAIRED, CONNECTING, CONNECTED, PAUSED, ERROR }

data class SyncStatus(val status: ConnectionStatus, val detail: String)

object SyncState {
    private val mutable = MutableStateFlow(SyncStatus(ConnectionStatus.UNPAIRED, "Pair this phone with your Windows PC."))
    val status = mutable.asStateFlow()
    fun update(status: ConnectionStatus, detail: String) { mutable.value = SyncStatus(status, detail) }
}
