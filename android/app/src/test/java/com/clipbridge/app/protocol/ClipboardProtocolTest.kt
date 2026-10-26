package com.clipbridge.app.protocol

import java.time.Instant
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

class ClipboardProtocolTest {
    private val secret = ByteArray(32) { it.toByte() }
    private val now = Instant.ofEpochSecond(1_800_000_000)

    @Test
    fun encryptThenDecryptPreservesUnicodeText() {
        val envelope = ClipboardProtocol.encrypt("नमस्ते 👋", Direction.ANDROID_TO_WINDOWS, secret, now)
        val result = ClipboardProtocol.decrypt(envelope, secret, now, ReplayCache())

        assertTrue(result.isSuccess)
        assertEquals("नमस्ते 👋", result.text)
    }

    @Test
    fun payloadLimitUsesUtf8Bytes() {
        val withinLimit = "😀".repeat(ClipboardProtocol.MAX_PAYLOAD_BYTES / 4)
        assertEquals(ClipboardProtocol.MAX_PAYLOAD_BYTES, withinLimit.toByteArray().size)

        try {
            ClipboardProtocol.encrypt(withinLimit + "a", Direction.ANDROID_TO_WINDOWS, secret, now)
            fail("Expected PayloadTooLargeException")
        } catch (_: PayloadTooLargeException) {
        }
    }

    @Test
    fun tamperingAndReplayAreRejected() {
        val envelope = ClipboardProtocol.encrypt("once", Direction.WINDOWS_TO_ANDROID, secret, now)
        val replay = ReplayCache()
        assertTrue(ClipboardProtocol.decrypt(envelope, secret, now, replay).isSuccess)
        assertEquals(DecryptFailure.REPLAY, ClipboardProtocol.decrypt(envelope, secret, now, replay).failure)

        val bytes = envelope.ciphertext.decodeBase64()
        bytes[0] = (bytes[0].toInt() xor 1).toByte()
        val modified = envelope.copy(ciphertext = bytes.encodeBase64())
        assertEquals(
            DecryptFailure.AUTHENTICATION_FAILED,
            ClipboardProtocol.decrypt(modified, secret, now, ReplayCache()).failure
        )
    }

    @Test
    fun expiredMessageIsRejected() {
        val envelope = ClipboardProtocol.encrypt("late", Direction.WINDOWS_TO_ANDROID, secret, now)
        assertEquals(
            DecryptFailure.EXPIRED,
            ClipboardProtocol.decrypt(envelope, secret, now.plusSeconds(301), ReplayCache()).failure
        )
    }

    @Test
    fun decryptsSharedCrossPlatformFixture() {
        val envelope = ClipboardEnvelope(
            version = 1,
            messageId = "00112233445566778899aabbccddeeff",
            timestampUnixSeconds = 1_800_000_000,
            direction = Direction.WINDOWS_TO_ANDROID,
            nonce = "AAECAwQFBgcICQoL",
            ciphertext = "LmyifreKsjtv3QQ=",
            tag = "2fzIjtMRKPw1GJs5h9T1MA=="
        )

        assertEquals("interop ✓", ClipboardProtocol.decrypt(envelope, secret, now, ReplayCache()).text)
    }
}
