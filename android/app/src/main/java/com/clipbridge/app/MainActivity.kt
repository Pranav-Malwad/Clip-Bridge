package com.clipbridge.app

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import com.clipbridge.app.pairing.PairingStore
import com.clipbridge.app.pairing.PairingUri
import com.clipbridge.app.sync.ConnectionStatus
import com.clipbridge.app.sync.SyncService
import com.clipbridge.app.sync.SyncState
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions

class MainActivity : ComponentActivity() {
    private lateinit var store: PairingStore
    private val scanner = registerForActivityResult(ScanContract()) { result -> result.contents?.let(::pair) }
    private val notifications = registerForActivityResult(ActivityResultContracts.RequestPermission()) { }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        store = PairingStore(this)
        requestNotificationPermission()
        if (store.load() != null && store.isSyncEnabled()) startSync()
        setContent {
            MaterialTheme {
                Surface(modifier = Modifier.fillMaxSize(), color = Color(0xFFF8FAFC)) {
                    ClipBridgeScreen(
                        pairedName = store.load()?.name,
                        onScan = {
                            scanner.launch(
                                ScanOptions()
                                    .setDesiredBarcodeFormats("QR_CODE")
                                    .setPrompt("Scan the ClipBridge code on Windows")
                                    .setBeepEnabled(false)
                            )
                        },
                        onManualPair = ::pair,
                        onStart = ::startSync,
                        onStop = ::stopSync,
                        onPrivate = ::enablePrivateMode,
                        onForget = {
                            stopSync()
                            store.clear()
                            recreate()
                        }
                    )
                }
            }
        }
    }

    private fun pair(value: String) {
        runCatching { PairingUri.parse(value) }
            .mapCatching { require(it.isInvitationValid()) { "That pairing invitation has expired." }; it }
            .onSuccess {
                store.save(it)
                startSync()
                recreate()
            }
            .onFailure { SyncState.update(ConnectionStatus.ERROR, "That pairing code is invalid.") }
    }

    private fun startSync() {
        store.setSyncEnabled(true)
        ContextCompat.startForegroundService(this, Intent(this, SyncService::class.java).setAction(SyncService.ACTION_START))
    }

    private fun stopSync() {
        store.setSyncEnabled(false)
        startService(Intent(this, SyncService::class.java).setAction(SyncService.ACTION_STOP))
    }

    private fun enablePrivateMode() {
        ContextCompat.startForegroundService(this, Intent(this, SyncService::class.java).setAction(SyncService.ACTION_PRIVATE_10))
    }

    private fun requestNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
            checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) {
            notifications.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }
}

@Composable
private fun ClipBridgeScreen(
    pairedName: String?,
    onScan: () -> Unit,
    onManualPair: (String) -> Unit,
    onStart: () -> Unit,
    onStop: () -> Unit,
    onPrivate: () -> Unit,
    onForget: () -> Unit
) {
    val sync by SyncState.status.collectAsState()
    var manual by remember { mutableStateOf("") }
    Column(
        modifier = Modifier.fillMaxSize().safeDrawingPadding().verticalScroll(rememberScrollState()).padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        Text("ClipBridge", style = MaterialTheme.typography.headlineLarge)
        Spacer(Modifier.height(8.dp))
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
        ) {
            Column(Modifier.padding(20.dp)) {
                Text(pairedName?.let { "Trusted PC: $it" } ?: "Set up your trusted PC", style = MaterialTheme.typography.titleMedium)
                Text(sync.status.name.lowercase().replaceFirstChar(Char::uppercase), style = MaterialTheme.typography.headlineSmall)
                Text(sync.detail, modifier = Modifier.padding(top = 8.dp))
            }
        }

        if (pairedName == null) {
            Button(onClick = onScan, modifier = Modifier.fillMaxWidth()) { Text("Scan Windows pairing code") }
            Spacer(Modifier.height(16.dp))
            OutlinedTextField(
                value = manual,
                onValueChange = { manual = it },
                label = { Text("Or paste pairing link") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )
            Button(onClick = { onManualPair(manual) }, enabled = manual.isNotBlank(), modifier = Modifier.fillMaxWidth()) {
                Text("Pair")
            }
        } else {
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                Button(onClick = onStart) { Text("Enable sync") }
                OutlinedButton(onClick = onStop) { Text("Pause") }
            }
            OutlinedButton(onClick = onPrivate, modifier = Modifier.fillMaxWidth().height(48.dp)) {
                Text("Private Mode for 10 minutes")
            }
            Spacer(Modifier.height(12.dp))
            OutlinedButton(onClick = onForget) { Text("Forget this PC") }
            Spacer(Modifier.height(24.dp))
            Text("To send text from Android: select it in any app, tap Share, then choose Send to PC.")
            Text("ClipBridge stays on your local network and does not keep clipboard history.", style = MaterialTheme.typography.bodySmall)
        }
    }
}
