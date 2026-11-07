package com.tomologaming.ecosys

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.security.KeyPair
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.security.MessageDigest
import java.security.PrivateKey
import java.security.PublicKey
import java.security.SecureRandom
import java.security.Signature
import java.security.spec.ECGenParameterSpec
import javax.crypto.Cipher
import javax.crypto.KeyAgreement
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

/**
 * Long-term ECDSA identity backed by the Android Keystore plus the cryptographic helpers used by the
 * pairing protocol. Only the public key ever leaves the device.
 */
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

    val publicKey: PublicKey
        get() = keyStore.getCertificate(alias).publicKey

    val publicKeyEncoded: ByteArray
        get() = publicKey.encoded

    val deviceId: String
        get() = sha256(publicKeyEncoded).copyOfRange(0, 12)
            .joinToString("") { "%02x".format(it.toInt() and 0xFF) }

    fun sign(data: ByteArray): ByteArray {
        val privateKey = keyStore.getKey(alias, null) as PrivateKey
        return Signature.getInstance("SHA256withECDSA").run {
            initSign(privateKey)
            update(data)
            sign()
        }
    }

    fun verify(data: ByteArray, signature: ByteArray, signer: PublicKey = publicKey): Boolean =
        Signature.getInstance("SHA256withECDSA").run {
            initVerify(signer)
            update(data)
            verify(signature)
        }

    companion object {
        /** Ephemeral P-256 key pair for one session. Kept out of the Keystore because ECDH needs PURPOSE_AGREE_KEY. */
        fun newEphemeralKeyPair(): KeyPair =
            KeyPairGenerator.getInstance("EC").apply {
                initialize(ECGenParameterSpec("secp256r1"), SecureRandom())
            }.generateKeyPair()

        fun deriveSharedSecret(privateKey: PrivateKey, peerPublicKey: PublicKey): ByteArray =
            KeyAgreement.getInstance("ECDH").run {
                init(privateKey)
                doPhase(peerPublicKey, true)
                generateSecret()
            }

        fun aesGcmEncrypt(key: ByteArray, plaintext: ByteArray, aad: ByteArray = ByteArray(0)): ByteArray {
            val nonce = ByteArray(12).also { SecureRandom().nextBytes(it) }
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
            MessageDigest.getInstance("SHA-256").digest(data)
    }
}
