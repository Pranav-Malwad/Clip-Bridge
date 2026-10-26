package com.clipbridge.app.protocol

import java.net.Inet4Address
import java.net.InetAddress
import java.security.MessageDigest
import java.security.SecureRandom
import java.util.Base64
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json

@Serializable data class DiscoveryRequest(val version: Int, val pairingId: String, val challenge: String)
@Serializable data class DiscoveryResponse(
    val version: Int, val pairingId: String, val challenge: String, val host: String,
    val port: Int, val expiresUnixSeconds: Long, val mac: String
)

object DiscoveryProtocol {
    const val VERSION = 2
    const val PORT = 24892
    private val json = Json { encodeDefaults = true; ignoreUnknownKeys = false }

    fun createRequest(pairingId: String): DiscoveryRequest = DiscoveryRequest(
        VERSION, pairingId, Base64.getEncoder().encodeToString(ByteArray(16).also(SecureRandom()::nextBytes))
    )
    fun serialize(request: DiscoveryRequest) = json.encodeToString(DiscoveryRequest.serializer(), request)
    fun parseResponse(value: String) = runCatching { json.decodeFromString(DiscoveryResponse.serializer(), value) }.getOrNull()
    fun verify(request: DiscoveryRequest, response: DiscoveryResponse, nowSeconds: Long, secret: ByteArray): Boolean {
        if (response.version != VERSION || response.pairingId != request.pairingId || response.challenge != request.challenge ||
            response.port !in 1..65535 || response.expiresUnixSeconds !in nowSeconds..(nowSeconds + 60) || !isPrivate(response.host)) return false
        val expected = hmac(secret, canonical(response))
        return runCatching { MessageDigest.isEqual(Base64.getDecoder().decode(response.mac), expected) }.getOrDefault(false)
    }
    private fun canonical(r: DiscoveryResponse) = "${r.version}|${r.pairingId}|${r.challenge}|${r.host}|${r.port}|${r.expiresUnixSeconds}".toByteArray()
    private fun hmac(key: ByteArray, value: ByteArray) = Mac.getInstance("HmacSHA256").apply { init(SecretKeySpec(key, "HmacSHA256")) }.doFinal(value)
    private fun isPrivate(host: String): Boolean = runCatching {
        val b = (InetAddress.getByName(host) as? Inet4Address)?.address ?: return false
        (b[0].toInt() and 255) == 10 || (b[0].toInt() and 255) == 127 ||
            ((b[0].toInt() and 255) == 192 && (b[1].toInt() and 255) == 168) ||
            ((b[0].toInt() and 255) == 172 && (b[1].toInt() and 255) in 16..31)
    }.getOrDefault(false)
}
