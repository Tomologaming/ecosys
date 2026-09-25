package com.tomologaming.ecosys

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.nio.charset.StandardCharsets
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.security.PrivateKey
import java.security.Signature
import java.security.spec.ECGenParameterSpec
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.KeyAgreement
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

class DeviceIdentity(private val alias: String = "ecosys.identity") {
    private val keyStore: KeyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }

    init {
        if (!keyStore.containsAlias(alias)) {
            val generator = KeyPairGenerator.getInstance(
                KeyProperties.KEY_ALGORITHM_EC,
                "AndroidKeyStore"
            )
            generator.initialize(
                KeyGenParameterSpec.Builder(
                    alias,
                    KeyProperties.PURPOSE_SIGN or KeyProperties.PURPOSE_VERIFY
                )
                    .setAlgorithmParameterSpec(ECGenParameterSpec("secp256r1"))
                    .setDigests(KeyProperties.DIGEST_SHA256)
                    .build()
            )
            generator.generateKeyPair()
        }
    }

    val publicKeyEncoded: ByteArray
        get() = keyStore.getCertificate(alias).publicKey.encoded

    val deviceId: String
        get() = sha256(publicKeyEncoded).copyOfRange(0, 12).joinToString("") { "%02x".format(it) }

    fun sign(data: ByteArray): ByteArray {
        val privateKey = keyStore.getKey(alias, null) as PrivateKey
        return Signature.getInstance("SHA256withECDSA").run {
            initSign(privateKey)
            update(data)
            sign()
        }
    }

    fun verify(data: ByteArray, signature: ByteArray): Boolean =
        Signature.getInstance("SHA256withECDSA").run {
            initVerify(keyStore.getCertificate(alias).publicKey)
            update(data)
            verify(signature)
        }

    companion object {
        fun deriveSharedSecret(privateKey: PrivateKey, peerPublicKey: java.security.PublicKey): ByteArray =
            KeyAgreement.getInstance("ECDH").run {
                init(privateKey)
                doPhase(peerPublicKey, true)
                generateSecret()
            }

        fun aesGcmEncrypt(key: ByteArray, plaintext: ByteArray, aad: ByteArray = ByteArray(0)): ByteArray {
            val nonce = ByteArray(12).also { java.security.SecureRandom().nextBytes(it) }
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, nonce))
            cipher.updateAAD(aad)
            return nonce + cipher.doFinal(plaintext)
        }

        fun aesGcmDecrypt(key: ByteArray, sealed: ByteArray, aad: ByteArray = ByteArray(0)): ByteArray {
            require(sealed.size >= 12 + 16) { "Ciphertext too short" }
            val nonce = sealed.copyOfRange(0, 12)
            val ciphertext = sealed.copyOfRange(12, sealed.size)
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, nonce))
            cipher.updateAAD(aad)
            return cipher.doFinal(ciphertext)
        }

        private fun sha256(data: ByteArray): ByteArray =
            java.security.MessageDigest.getInstance("SHA-256").digest(data)
    }
}
