package com.clipbridge.app.protocol

import java.nio.charset.StandardCharsets
import java.security.KeyFactory
import java.security.KeyPair
import java.security.KeyPairGenerator
import java.security.MessageDigest
import java.security.SecureRandom
import java.security.spec.ECGenParameterSpec
import java.security.spec.X509EncodedKeySpec
import java.util.Base64
import javax.crypto.KeyAgreement
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json

@Serializable data class ClientHello(val version: Int, val pairingId: String, val challenge: String, val publicKey: String, val mac: String)
@Serializable data class ServerHello(val version: Int, val challenge: String, val publicKey: String, val mac: String)
data class SessionKeys(val windowsToAndroid: ByteArray, val androidToWindows: ByteArray)
data class ServerHandshake(val hello: ServerHello, val keys: SessionKeys)
data class ClientHandshake(val keys: SessionKeys? = null, val error: String? = null) { val isSuccess get() = keys != null }

object SessionProtocol {
    const val CURRENT_VERSION = 2
    private val random = SecureRandom()
    private val json = Json { encodeDefaults = true; ignoreUnknownKeys = false }

    fun createEphemeralKey(): KeyPair = KeyPairGenerator.getInstance("EC").apply {
        initialize(ECGenParameterSpec("secp256r1"))
    }.generateKeyPair()

    fun createClientHello(pairingId: String, secret: ByteArray, key: KeyPair, challenge: ByteArray = ByteArray(16).also(random::nextBytes)): ClientHello {
        require(secret.size == 32)
        val unsigned = ClientHello(CURRENT_VERSION, pairingId, challenge.b64(), key.public.encoded.b64(), "")
        return unsigned.copy(mac = mac(secret, clientCanonical(unsigned)))
    }

    fun verifyClientHello(hello: ClientHello, secret: ByteArray): Boolean = try {
        secret.size == 32 && hello.version == CURRENT_VERSION && hello.pairingId.isNotBlank() &&
            hello.challenge.b64decode().size == 16 && decodePublic(hello.publicKey) != null &&
            verifyMac(secret, clientCanonical(hello), hello.mac)
    } catch (_: Exception) { false }

    fun createServerHello(client: ClientHello, secret: ByteArray, key: KeyPair,
                          challenge: ByteArray = ByteArray(16).also(random::nextBytes)): ServerHandshake {
        require(verifyClientHello(client, secret)) { "Client hello authentication failed." }
        val unsigned = ServerHello(CURRENT_VERSION, challenge.b64(), key.public.encoded.b64(), "")
        val hello = unsigned.copy(mac = mac(secret, serverCanonical(client, unsigned)))
        return ServerHandshake(hello, deriveKeys(client, hello, key))
    }

    fun completeClientHandshake(client: ClientHello, server: ServerHello, secret: ByteArray, key: KeyPair): ClientHandshake = try {
        if (server.version != CURRENT_VERSION || server.challenge.b64decode().size != 16 ||
            !verifyMac(secret, serverCanonical(client, server), server.mac)) ClientHandshake(error = "Server authentication failed.")
        else ClientHandshake(keys = deriveKeys(client, server, key))
    } catch (_: Exception) { ClientHandshake(error = "Server hello is invalid.") }

    fun serialize(hello: ClientHello) = json.encodeToString(ClientHello.serializer(), hello)
    fun serialize(hello: ServerHello) = json.encodeToString(ServerHello.serializer(), hello)
    fun parseClient(value: String): ClientHello? = try { json.decodeFromString(ClientHello.serializer(), value) } catch (_: Exception) { null }
    fun parseServer(value: String): ServerHello? = try { json.decodeFromString(ServerHello.serializer(), value) } catch (_: Exception) { null }

    private fun deriveKeys(client: ClientHello, server: ServerHello, own: KeyPair): SessionKeys {
        val ownIsClient = own.public.encoded.contentEquals(client.publicKey.b64decode())
        val peer = decodePublic(if (ownIsClient) server.publicKey else client.publicKey)!!
        val agreement = KeyAgreement.getInstance("ECDH").apply { init(own.private); doPhase(peer, true) }
        val shared = agreement.generateSecret()
        val salt = MessageDigest.getInstance("SHA-256").digest("${client.pairingId}|${client.challenge}|${server.challenge}".toByteArray())
        val output = hkdf(shared, salt, "ClipBridge/session/v2".toByteArray(), 64)
        shared.fill(0)
        return SessionKeys(output.copyOfRange(0, 32), output.copyOfRange(32, 64))
    }

    private fun decodePublic(value: String) = KeyFactory.getInstance("EC").generatePublic(X509EncodedKeySpec(value.b64decode()))
    private fun clientCanonical(h: ClientHello) = "client|${h.version}|${h.pairingId}|${h.challenge}|${h.publicKey}"
    private fun serverCanonical(c: ClientHello, s: ServerHello) = "server|${s.version}|${c.pairingId}|${c.challenge}|${c.publicKey}|${s.challenge}|${s.publicKey}"
    private fun mac(secret: ByteArray, value: String) = hmac(secret, value.toByteArray(StandardCharsets.UTF_8)).b64()
    private fun verifyMac(secret: ByteArray, value: String, supplied: String) = MessageDigest.isEqual(supplied.b64decode(), hmac(secret, value.toByteArray()))
    private fun hmac(key: ByteArray, value: ByteArray) = Mac.getInstance("HmacSHA256").apply { init(SecretKeySpec(key, "HmacSHA256")) }.doFinal(value)
    private fun hkdf(ikm: ByteArray, salt: ByteArray, info: ByteArray, length: Int): ByteArray {
        val prk = hmac(salt, ikm); val result = ByteArray(length); var previous = ByteArray(0); var offset = 0; var counter = 1
        while (offset < length) {
            previous = hmac(prk, previous + info + byteArrayOf(counter++.toByte()))
            val count = minOf(previous.size, length - offset); previous.copyInto(result, offset, 0, count); offset += count
        }
        prk.fill(0); return result
    }
    private fun ByteArray.b64() = Base64.getEncoder().encodeToString(this)
    private fun String.b64decode() = Base64.getDecoder().decode(this)
}
