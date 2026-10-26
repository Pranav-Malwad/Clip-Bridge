package com.clipbridge.app.pairing

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

class PairingStore(context: Context) {
    private val preferences = EncryptedSharedPreferences.create(
        context,
        "clipbridge_pairing",
        MasterKey.Builder(context).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build(),
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )

    fun load(): PairingInfo? = preferences.getString(KEY_URI, null)?.let { value ->
        runCatching { PairingUri.parse(value) }.getOrNull()
    }

    fun save(info: PairingInfo) {
        preferences.edit().putString(KEY_URI, PairingUri.format(info)).apply()
    }

    fun clear() {
        preferences.edit().clear().apply()
    }

    fun isSyncEnabled(): Boolean = preferences.getBoolean(KEY_SYNC_ENABLED, false)

    fun setSyncEnabled(enabled: Boolean) {
        preferences.edit().putBoolean(KEY_SYNC_ENABLED, enabled).apply()
    }

    private companion object {
        const val KEY_URI = "pairing_uri"
        const val KEY_SYNC_ENABLED = "sync_enabled"
    }
}
