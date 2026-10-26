package com.clipbridge.app.pairing

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.fail
import org.junit.Test

class PairingUriTest {
    @Test
    fun parsesValidPairingUri() {
        val secret = ByteArray(32) { it.toByte() }
        val value = PairingUri.format(PairingInfo("My Laptop", "192.168.1.20", 24891, secret))

        val parsed = PairingUri.parse(value)

        assertEquals("My Laptop", parsed.name)
        assertEquals("192.168.1.20", parsed.host)
        assertEquals(24891, parsed.port)
        assertArrayEquals(secret, parsed.secret)
    }

    @Test
    fun rejectsWrongSchemeMissingSecretAndInvalidPort() {
        listOf(
            "https://example.com",
            "clipbridge://pair?host=x&port=24891",
            "clipbridge://pair?host=x&port=0&secret=nope"
        ).forEach { value ->
            try {
                PairingUri.parse(value)
                fail("Expected invalid pairing URI: $value")
            } catch (_: IllegalArgumentException) {
            }
        }
    }
}
