package com.clipbridge.app.protocol

import java.nio.charset.StandardCharsets
import java.security.GeneralSecurityException
import java.security.SecureRandom
import java.time.Instant
import java.util.Base64
import java.util.UUID
import javax.crypto.AEADBadTagException
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json

@Serializable
enum class Direction(val aadName: String) {
    @SerialName("windowsToAndroid")
    WINDOWS_TO_ANDROID("WindowsToAndroid"),

    @SerialName("androidToWindows")
    ANDROID_TO_WINDOWS("AndroidToWindows")
}

@Serializable
data class ClipboardEnvelope(
    val version: Int,
    val messageId: String,
    val timestampUnixSeconds: Long,
    val direction: Direction,
    val nonce: String,
    val ciphertext: String,
    val tag: String
)

enum class DecryptFailure {
    NONE,
    INVALID_ENVELOPE,
    AUTHENTICATION_FAILED,
    EXPIRED,
    REPLAY,
    PAYLOAD_TOO_LARGE
}

data class DecryptResult(val text: String? = null, val failure: DecryptFailure = DecryptFailure.NONE) {
    val isSuccess: Boolean get() = failure == DecryptFailure.NONE
}

class PayloadTooLargeException(actualBytes: Int) :
    IllegalArgumentException("Clipboard payload is $actualBytes bytes; the limit is ${ClipboardProtocol.MAX_PAYLOAD_BYTES} bytes.")

class ReplayCache {
    private val seen = mutableSetOf<String>()
    private val ordered = ArrayDeque<Pair<String, Long>>()

    @Synchronized
    fun tryRemember(messageId: String, timestamp: Long, now: Long): Boolean {
        while (ordered.isNotEmpty() && now - ordered.first().second > ClipboardProtocol.ALLOWED_AGE_SECONDS) {
            seen.remove(ordered.removeFirst().first)
        }
        if (!seen.add(messageId)) return false
        ordered.addLast(messageId to timestamp)
        return true
    }
}

object ClipboardProtocol {
    const val CURRENT_VERSION = 1
    const val MAX_PAYLOAD_BYTES = 256 * 1024
    const val ALLOWED_AGE_SECONDS = 5 * 60L
    private val random = SecureRandom()
    private val json = Json { encodeDefaults = true; ignoreUnknownKeys = false }

    fun encrypt(text: String, direction: Direction, secret: ByteArray, now: Instant = Instant.now()): ClipboardEnvelope {
        require(secret.size == 32) { "Pairing secret must be exactly 32 bytes." }
        val plaintext = text.toByteArray(StandardCharsets.UTF_8)
        if (plaintext.size > MAX_PAYLOAD_BYTES) throw PayloadTooLargeException(plaintext.size)

        val id = UUID.randomUUID().toString().replace("-", "")
        val timestamp = now.epochSecond
        val nonce = ByteArray(12).also(random::nextBytes)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(secret, "AES"), GCMParameterSpec(128, nonce))
        cipher.updateAAD(additionalData(CURRENT_VERSION, id, timestamp, direction))
        val encryptedWithTag = cipher.doFinal(plaintext)
        val ciphertext = encryptedWithTag.copyOfRange(0, encryptedWithTag.size - 16)
        val tag = encryptedWithTag.copyOfRange(encryptedWithTag.size - 16, encryptedWithTag.size)

        return ClipboardEnvelope(
            CURRENT_VERSION,
            id,
            timestamp,
            direction,
            nonce.encodeBase64(),
            ciphertext.encodeBase64(),
            tag.encodeBase64()
        )
    }

    fun decrypt(
        envelope: ClipboardEnvelope,
        secret: ByteArray,
        now: Instant = Instant.now(),
        replayCache: ReplayCache
    ): DecryptResult {
        require(secret.size == 32) { "Pairing secret must be exactly 32 bytes." }
        if (envelope.version != CURRENT_VERSION || !envelope.messageId.matches(Regex("[0-9a-fA-F]{32}"))) {
            return DecryptResult(failure = DecryptFailure.INVALID_ENVELOPE)
        }
        if (kotlin.math.abs(now.epochSecond - envelope.timestampUnixSeconds) > ALLOWED_AGE_SECONDS) {
            return DecryptResult(failure = DecryptFailure.EXPIRED)
        }

        return try {
            val nonce = envelope.nonce.decodeBase64()
            val ciphertext = envelope.ciphertext.decodeBase64()
            val tag = envelope.tag.decodeBase64()
            if (nonce.size != 12 || tag.size != 16) return DecryptResult(failure = DecryptFailure.INVALID_ENVELOPE)
            if (ciphertext.size > MAX_PAYLOAD_BYTES) return DecryptResult(failure = DecryptFailure.PAYLOAD_TOO_LARGE)

            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(secret, "AES"), GCMParameterSpec(128, nonce))
            cipher.updateAAD(additionalData(envelope.version, envelope.messageId, envelope.timestampUnixSeconds, envelope.direction))
            val plaintext = cipher.doFinal(ciphertext + tag)
            if (!replayCache.tryRemember(envelope.messageId, envelope.timestampUnixSeconds, now.epochSecond)) {
                return DecryptResult(failure = DecryptFailure.REPLAY)
            }
            DecryptResult(text = String(plaintext, StandardCharsets.UTF_8))
        } catch (_: AEADBadTagException) {
            DecryptResult(failure = DecryptFailure.AUTHENTICATION_FAILED)
        } catch (_: GeneralSecurityException) {
            DecryptResult(failure = DecryptFailure.INVALID_ENVELOPE)
        } catch (_: IllegalArgumentException) {
            DecryptResult(failure = DecryptFailure.INVALID_ENVELOPE)
        }
    }

    fun serialize(envelope: ClipboardEnvelope): String = json.encodeToString(ClipboardEnvelope.serializer(), envelope)

    fun deserialize(value: String): ClipboardEnvelope? = try {
        json.decodeFromString(ClipboardEnvelope.serializer(), value)
    } catch (_: SerializationException) {
        null
    } catch (_: IllegalArgumentException) {
        null
    }

    private fun additionalData(version: Int, id: String, timestamp: Long, direction: Direction): ByteArray =
        "$version|$id|$timestamp|${direction.aadName}".toByteArray(StandardCharsets.UTF_8)
}

fun ByteArray.encodeBase64(): String = Base64.getEncoder().encodeToString(this)
fun String.decodeBase64(): ByteArray = Base64.getDecoder().decode(this)
