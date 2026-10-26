package com.clipbridge.app.sync

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class TransientClipboardVaultTest {
    @Test
    fun valueCanBeTakenOnceBeforeExpiry() {
        var now = 1_000L
        val vault = TransientClipboardVault(ttlMillis = 300_000, clock = { now })
        val token = vault.put("private")

        assertEquals("private", vault.take(token))
        assertNull(vault.take(token))
    }

    @Test
    fun expiredValueIsUnavailable() {
        var now = 1_000L
        val vault = TransientClipboardVault(ttlMillis = 300_000, clock = { now })
        val token = vault.put("private")
        now += 300_001

        assertNull(vault.take(token))
    }

    @Test fun discardRemovesValueWithoutAnotherPut() {
        val vault = TransientClipboardVault()
        val token = vault.put("private")
        vault.discard(token)
        assertNull(vault.take(token))
    }
}
