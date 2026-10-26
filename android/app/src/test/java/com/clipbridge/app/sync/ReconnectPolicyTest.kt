package com.clipbridge.app.sync

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ReconnectPolicyTest {
    @Test fun privateModeSuppressesReconnectUntilDeadline() {
        assertFalse(ReconnectPolicy.shouldReconnect(true, pauseUntil = 601_000, now = 1_000))
        assertTrue(ReconnectPolicy.shouldReconnect(true, pauseUntil = 1_000, now = 1_000))
        assertFalse(ReconnectPolicy.shouldReconnect(false, pauseUntil = 0, now = 1_000))
    }
}
