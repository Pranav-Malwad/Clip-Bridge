package com.clipbridge.app.sync

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Intent
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import androidx.core.app.NotificationCompat
import com.clipbridge.app.MainActivity
import com.clipbridge.app.pairing.PairingInfo
import com.clipbridge.app.pairing.PairingStore
import com.clipbridge.app.protocol.ClipboardProtocol
import com.clipbridge.app.protocol.ClientHello
import com.clipbridge.app.protocol.Direction
import com.clipbridge.app.protocol.SessionKeys
import com.clipbridge.app.protocol.SessionProtocol
import java.security.KeyPair
import java.util.concurrent.TimeUnit
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener

class SyncService : Service() {
    private val handler = Handler(Looper.getMainLooper())
    private val pending = PendingTextQueue(20)
    private val client = OkHttpClient.Builder().pingInterval(20, TimeUnit.SECONDS).build()
    private var socket: WebSocket? = null
    private var pairing: PairingInfo? = null
    private var processor: InboundMessageProcessor? = null
    private var running = false
    private var connected = false
    private var reconnectDelay = 1_000L
    private var pendingKey: KeyPair? = null
    private var pendingHello: ClientHello? = null
    private var sessionKeys: SessionKeys? = null

    override fun onCreate() {
        super.onCreate()
        createChannels()
        startForeground(ONGOING_NOTIFICATION_ID, ongoingNotification("Starting…"))
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                running = false
                PairingStore(this).setSyncEnabled(false)
                SyncState.update(ConnectionStatus.PAUSED, "Clipboard sync is paused.")
                stopSelf()
                return START_NOT_STICKY
            }
            ACTION_SEND_TEXT -> intent.getStringExtra(EXTRA_TEXT)?.let(pending::add)
            ACTION_PRIVATE_10 -> {
                val store = PairingStore(this)
                store.setPauseUntil(System.currentTimeMillis() + 10 * 60_000L)
                connected = false
                sessionKeys = null
                socket?.cancel()
                socket = null
                SyncState.update(ConnectionStatus.PAUSED, "Private Mode is on for 10 minutes.")
                updateOngoing("Private Mode — resumes automatically")
                handler.postDelayed({
                    if (running) {
                        store.setPauseUntil(0)
                        connect()
                    }
                }, 10 * 60_000L)
                return START_STICKY
            }
        }

        val storedPairing = PairingStore(this).load()
        if (storedPairing == null) {
            SyncState.update(ConnectionStatus.UNPAIRED, "Pair this phone before sending text.")
            stopSelf()
            return START_NOT_STICKY
        }

        if (pairing?.secret?.contentEquals(storedPairing.secret) != true) {
            processor = InboundMessageProcessor(storedPairing.secret)
        }
        pairing = storedPairing

        running = true
        PairingStore(this).setSyncEnabled(true)
        val remainingPause = PairingStore(this).pauseUntil() - System.currentTimeMillis()
        if (remainingPause > 0) {
            SyncState.update(ConnectionStatus.PAUSED, "Private Mode is on.")
            updateOngoing("Private Mode — resumes automatically")
            handler.postDelayed({ if (running) connect() }, remainingPause)
            return START_STICKY
        }
        PairingStore(this).setPauseUntil(0)
        if (connected) drainPending() else if (socket == null) connect()
        return START_STICKY
    }

    private fun connect() {
        val paired = pairing ?: return
        SyncState.update(ConnectionStatus.CONNECTING, "Connecting to ${paired.name}…")
        updateOngoing("Connecting to ${paired.name}…")
        val request = Request.Builder().url("ws://${paired.host}:${paired.port}/clipbridge").build()
        socket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                reconnectDelay = 1_000L
                val key = SessionProtocol.createEphemeralKey()
                val hello = SessionProtocol.createClientHello(paired.pairingId, paired.secret, key)
                pendingKey = key
                pendingHello = hello
                webSocket.send(SessionProtocol.serialize(hello))
                handler.postDelayed({
                    if (!connected && socket === webSocket) webSocket.cancel()
                }, 5_000L)
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                if (!connected) {
                    val key = pendingKey ?: return webSocket.cancel()
                    val hello = pendingHello ?: return webSocket.cancel()
                    val serverHello = SessionProtocol.parseServer(text) ?: return webSocket.cancel()
                    val result = SessionProtocol.completeClientHandshake(hello, serverHello, paired.secret, key)
                    sessionKeys = result.keys ?: return webSocket.cancel()
                    processor = InboundMessageProcessor(sessionKeys!!.windowsToAndroid)
                    val finished = ClipboardProtocol.encrypt("", Direction.ANDROID_TO_WINDOWS, sessionKeys!!.androidToWindows)
                    if (!webSocket.send(ClipboardProtocol.serialize(finished))) return webSocket.cancel()
                    pendingKey = null
                    pendingHello = null
                    connected = true
                    SyncState.update(ConnectionStatus.CONNECTED, "Connected to ${paired.name}")
                    updateOngoing("Connected to ${paired.name}")
                    drainPending()
                    return
                }
                if (!ReconnectPolicy.shouldReconnect(running, PairingStore(this@SyncService).pauseUntil())) return
                val result = processor?.process(text) ?: return
                result.text?.let(::deliverIncoming)
            }

            override fun onFailure(webSocket: WebSocket, error: Throwable, response: Response?) {
                connected = false
                sessionKeys = null
                socket = null
                SyncState.update(ConnectionStatus.ERROR, "Cannot reach ${paired.name}; retrying.")
                updateOngoing("Disconnected; retrying…")
                if (!ReconnectPolicy.shouldReconnect(running, PairingStore(this@SyncService).pauseUntil())) return
                val discovered = DiscoveryClient.discover(paired)
                if (discovered != null) {
                    pairing = discovered
                    PairingStore(this@SyncService).save(discovered)
                    reconnectDelay = 1_000L
                }
                scheduleReconnect()
            }

            override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                connected = false
                webSocket.close(code, reason)
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                connected = false
                sessionKeys = null
                socket = null
                if (running) scheduleReconnect()
            }
        })
    }

    private fun drainPending() {
        if (!ReconnectPolicy.shouldReconnect(running, PairingStore(this).pauseUntil())) return
        val active = socket ?: return
        val paired = pairing ?: return
        pending.drain { text -> sendNow(active, text, paired) }
    }

    private fun sendNow(webSocket: WebSocket, text: String, paired: PairingInfo): Boolean {
        val key = sessionKeys?.androidToWindows ?: return false
        val envelope = ClipboardProtocol.encrypt(text, Direction.ANDROID_TO_WINDOWS, key)
        return webSocket.send(ClipboardProtocol.serialize(envelope))
    }

    private fun scheduleReconnect() {
        if (!ReconnectPolicy.shouldReconnect(running, PairingStore(this).pauseUntil())) return
        handler.postDelayed({
            if (ReconnectPolicy.shouldReconnect(running, PairingStore(this).pauseUntil()) && socket == null) connect()
        }, reconnectDelay)
        reconnectDelay = (reconnectDelay * 2).coerceAtMost(30_000L)
    }

    private fun deliverIncoming(text: String) {
        handler.post {
            ClipboardWriter.write(this, text)
            val token = ClipboardVault.instance.put(text)
            handler.postDelayed({ ClipboardVault.instance.discard(token) }, 300_000L)
            val copyIntent = Intent(this, CopyReceiver::class.java).putExtra(CopyReceiver.EXTRA_TOKEN, token)
            val copyAction = PendingIntent.getBroadcast(
                this,
                token.hashCode(),
                copyIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            val notification = NotificationCompat.Builder(this, TRANSFER_CHANNEL)
                .setSmallIcon(android.R.drawable.ic_menu_set_as)
                .setContentTitle("Text received from Windows")
                .setContentText("Ready to paste. Tap Copy if Android cleared it.")
                .setVisibility(NotificationCompat.VISIBILITY_PRIVATE)
                .setAutoCancel(true)
                .addAction(0, "Copy", copyAction)
                .build()
            getSystemService(NotificationManager::class.java).notify(TRANSFER_NOTIFICATION_ID, notification)
        }
    }

    private fun createChannels() {
        val manager = getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(NotificationChannel(ONGOING_CHANNEL, "Clipboard connection", NotificationManager.IMPORTANCE_LOW))
        manager.createNotificationChannel(NotificationChannel(TRANSFER_CHANNEL, "Clipboard transfers", NotificationManager.IMPORTANCE_DEFAULT))
    }

    private fun ongoingNotification(detail: String): android.app.Notification {
        val openIntent = PendingIntent.getActivity(
            this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        return NotificationCompat.Builder(this, ONGOING_CHANNEL)
            .setSmallIcon(android.R.drawable.stat_sys_upload_done)
            .setContentTitle("ClipBridge is active")
            .setContentText(detail)
            .setContentIntent(openIntent)
            .setOngoing(true)
            .setVisibility(NotificationCompat.VISIBILITY_PRIVATE)
            .build()
    }

    private fun updateOngoing(detail: String) {
        getSystemService(NotificationManager::class.java).notify(ONGOING_NOTIFICATION_ID, ongoingNotification(detail))
    }

    override fun onDestroy() {
        running = false
        connected = false
        sessionKeys = null
        ClipboardVault.instance.clear()
        handler.removeCallbacksAndMessages(null)
        socket?.cancel()
        socket = null
        client.dispatcher.executorService.shutdown()
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    companion object {
        const val ACTION_START = "com.clipbridge.app.action.START"
        const val ACTION_STOP = "com.clipbridge.app.action.STOP"
        const val ACTION_SEND_TEXT = "com.clipbridge.app.action.SEND_TEXT"
        const val ACTION_PRIVATE_10 = "com.clipbridge.app.action.PRIVATE_10"
        const val EXTRA_TEXT = "com.clipbridge.app.extra.TEXT"
        private const val ONGOING_CHANNEL = "clipbridge_connection"
        private const val TRANSFER_CHANNEL = "clipbridge_transfers"
        private const val ONGOING_NOTIFICATION_ID = 1001
        private const val TRANSFER_NOTIFICATION_ID = 1002
    }
}
