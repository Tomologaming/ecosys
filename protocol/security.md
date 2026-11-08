# Ecosys Security and Pairing Protocol

## Goals

Ecosys connections MUST NOT treat Bluetooth connectivity as authorization. A peer becomes trusted only after an explicit pairing decision and successful cryptographic authentication.

The security design is transport-independent so the same session protocol can later run over Bluetooth, Wi-Fi/LAN, or USB.

## Cryptographic building blocks

Ecosys v1 security uses platform-standard primitives:

- Long-term device identity: ECDSA P-256 signing key pair.
- Ephemeral session key agreement: ECDH P-256.
- Key derivation: HKDF-SHA-256.
- Payload encryption: AES-256-GCM.
- Authentication: ECDSA P-256 signatures over the complete handshake transcript.

Implementations MUST use audited platform/library implementations. Ecosys MUST NOT implement these primitives itself.

## Device identity

Each installation creates a random long-term ECDSA P-256 key pair on first start.

The public key is the stable cryptographic identity. The human-readable device ID is derived from it and is not itself a security credential.

Private identity keys MUST remain local to the device and MUST NOT be transmitted.

The Android implementation SHOULD store the private key using Android Keystore-backed protection where the selected crypto provider supports the required ECDSA P-256 operation. Windows SHOULD use the Windows CNG key storage facilities where practical.

## Pairing flow

A transport connection starts in an untrusted state:

1. The initiator sends `pairing.request` with its identity public key, device metadata, and an ephemeral ECDH P-256 public key.
2. The responder presents the peer identity to the user.
3. The responder sends `pairing.response` containing its identity public key, ephemeral public key, an explicit approval result, and a signature.
4. The initiator verifies the responder signature and transcript.
5. The initiator signs the transcript and sends the final authentication message.
6. Both sides derive the same session key using ECDH P-256 + HKDF-SHA-256.
7. Application messages are accepted only after the session reaches `authenticated`.

A rejected pairing MUST terminate the session.

## Trust store

A trusted peer record contains:

- identity public key
- derived device ID
- user-visible device name
- first-paired timestamp
- last-seen timestamp
- optional user-controlled label

Trust is keyed by the public identity key, not by Bluetooth MAC address or device name. Bluetooth addresses and names can change and MUST NOT be used as persistent identity.

## User verification

The first pairing MUST require explicit user confirmation on both sides when the platform permits it.

The UI SHOULD display a short authentication code derived from the handshake transcript. The code is a confirmation aid, not a password and not a replacement for cryptographic verification.

If the displayed codes differ, the user MUST reject the pairing.

## Session framing

After authentication, each application message is encrypted as a binary frame:

```text
version | direction | counter | nonce | ciphertext | tag
```

- `version`: security framing version
- `direction`: initiator-to-responder or responder-to-initiator
- `counter`: monotonically increasing 64-bit sequence number
- `nonce`: unique per frame
- `ciphertext`: encrypted Ecosys envelope
- `tag`: AEAD authentication tag

Counters MUST be checked for replay and MUST NOT move backwards.

Implementations MUST use a fresh nonce for every encrypted frame. A nonce MUST never be reused with the same session key.

## Session lifecycle

```text
transport.connected
        |
        v
pairing.pending
   |            |
reject        approve
   |            |
closed     authenticating
               |
          authenticated
               |
             secure
               |
            closed
```

The session key is ephemeral. Closing the connection discards it.

## Rekeying and limits

The first implementation should create one session key per connection. Before practical production limits are reached, the protocol SHOULD add explicit rekeying based on message count, byte count, or elapsed time.

Implementations MUST enforce maximum handshake/message sizes and abort malformed frames instead of attempting to recover from ambiguous input.

## Implementation status (v0)

The authenticated session described above is **not** implemented yet. Windows, Android and iOS
currently exchange `hello`, `ping` and (Windows ↔ Android) clipboard messages over an
unencrypted RFCOMM/BLE link.

Because that violates the rule that application data must not travel before authentication, the
clients apply a temporary gate built on a local trust list:

- The first `hello` from a peer adds it to a local history keyed by the identity ID in the
  envelope, together with the reported name, the platform and the last-seen time.
- Clipboard sharing is off by default and can only be enabled while a connection exists.
- Enabling it requires the peer to be **confirmed** by the user on this device. That confirmation
  is stored locally, can be revoked at any time, and revoking it stops sharing immediately.
- Confirming is a local decision, not a cryptographic one: it records "I have seen this device ID
  before". The client still warns that the link is unencrypted.

Nothing is shared automatically after connecting, and a peer that was never confirmed cannot
start clipboard traffic from the other side either: the receiving client drops `clipboard.set`
while sharing is off.

This gate is a stopgap, not a substitute for the handshake. Until the session described above
ships, clipboard traffic MUST stay opt-in per connection and MUST NOT be extended to new data
types (files, notifications, history).

## Compatibility

Security framing has its own version. A future incompatible security framing MUST increment that version even if the outer Ecosys protocol remains `ecosys/1`.

A peer that does not support the required security version MUST fail closed and MUST NOT silently fall back to plaintext for sensitive messages.