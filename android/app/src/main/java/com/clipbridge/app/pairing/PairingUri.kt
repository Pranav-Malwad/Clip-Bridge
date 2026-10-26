package com.clipbridge.app.pairing

import java.net.URI
import java.net.URLDecoder
import java.net.URLEncoder
import java.nio.charset.StandardCharsets
import java.util.Base64
import java.time.Instant
import java.util.UUID

data class PairingInfo(
    val name: String, val host: String, val port: Int, val secret: ByteArray,
    val pairingId: String = UUID.randomUUID().toString().replace("-", ""),
    val expiresAt: Instant = Instant.now().plusSeconds(120)
) {
    fun isInvitationValid(now: Instant = Instant.now()) = !now.isAfter(expiresAt)
}

object PairingUri {
    fun format(info: PairingInfo): String {
        validate(info.host, info.port, info.secret, info.pairingId)
        val secret = Base64.getUrlEncoder().withoutPadding().encodeToString(info.secret)
        return "clipbridge://pair?host=${encode(info.host)}&port=${info.port}&secret=$secret" +
            "&id=${encode(info.pairingId)}&expires=${info.expiresAt.epochSecond}&name=${encode(info.name)}"
    }

    fun parse(value: String): PairingInfo {
        val uri = try { URI(value) } catch (error: Exception) { throw IllegalArgumentException("Invalid pairing URI.", error) }
        require(uri.scheme.equals("clipbridge", true) && uri.host.equals("pair", true)) { "Not a ClipBridge pairing URI." }
        val query = uri.rawQuery.orEmpty().split('&').mapNotNull { part ->
            val separator = part.indexOf('=')
            if (separator <= 0) null else decode(part.substring(0, separator)) to decode(part.substring(separator + 1))
        }.toMap()
        val host = query["host"] ?: throw IllegalArgumentException("Pairing host is missing.")
        val port = query["port"]?.toIntOrNull() ?: throw IllegalArgumentException("Pairing port is invalid.")
        val secret = try {
            Base64.getUrlDecoder().decode(query["secret"] ?: throw IllegalArgumentException("Pairing secret is missing."))
        } catch (error: IllegalArgumentException) {
            throw IllegalArgumentException("Pairing secret is invalid.", error)
        }
        val pairingId = query["id"] ?: throw IllegalArgumentException("Pairing identity is missing.")
        val expires = query["expires"]?.toLongOrNull()?.let(Instant::ofEpochSecond)
            ?: throw IllegalArgumentException("Pairing expiration is invalid.")
        validate(host, port, secret, pairingId)
        return PairingInfo(query["name"].takeUnless { it.isNullOrBlank() } ?: "Windows PC", host, port, secret, pairingId, expires)
    }

    private fun validate(host: String, port: Int, secret: ByteArray, pairingId: String) {
        require(host.isNotBlank() && host.length <= 255 && port in 1..65535 && secret.size == 32 && pairingId.isNotBlank() && pairingId.length <= 128) {
            "Pairing parameters are invalid."
        }
    }

    @Suppress("DEPRECATION")
    private fun encode(value: String) = URLEncoder.encode(value, "UTF-8").replace("+", "%20")

    @Suppress("DEPRECATION")
    private fun decode(value: String) = URLDecoder.decode(value, "UTF-8")
}
