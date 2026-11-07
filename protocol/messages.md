# Ecosys Message Protocol v1

## Envelope

Every control message uses an envelope:

```json
{
  "protocol": "ecosys/1",
  "type": "hello",
  "messageId": "01J...",
  "sender": "device_01...",
  "timestamp": 1770000000000,
  "payload": {}
}
```

Fields:
- `protocol`: required; current value `ecosys/1`
- `type`: required message type
- `messageId`: required unique message ID
- `sender`: required stable Ecosys device ID
- `timestamp`: required Unix epoch time in milliseconds
- `payload`: required type-specific object

Unknown fields MUST be ignored. Required fields MUST be validated before dispatch.
Implementations MUST drop a message whose `payload` is missing or not an object instead of
guessing the field layout from the envelope.

## Core messages

### hello
Sent after a transport connection is established. Payload contains `deviceName`, `platform`, and `capabilities`. A hello is transport-level metadata and MUST NOT be interpreted as proof of identity.

### hello.ack
Acknowledges hello and communicates the responder's protocol capabilities.

### ping / pong
Keep-alive and connection diagnostics. Payload is an empty object.

### pairing.request
Starts authenticated pairing. Payload contains the security version, identity public key, ephemeral key, device metadata, and a handshake nonce.

### pairing.response
Contains the responder identity, ephemeral key, approval result, handshake nonce, and signature over the canonical handshake transcript.

### pairing.complete
Final authentication message containing the initiator signature over the same transcript. After successful verification both sides may enter the encrypted session state.

### error
Payload contains a stable error code and a human-readable message.

## File transfer

File messages carry all type-specific data in `payload`. They are implemented by the Windows
client, the iOS app and the web bridge (`website/index.html`).

### file.start

```json
{ "transferId": "8f6c...", "fileName": "foto.jpg", "size": 184320, "chunks": 180 }
```

- `transferId`: opaque identifier for this transfer
- `fileName`: display name; receivers MUST sanitise it before using it as a path
- `size`: exact byte length of the file
- `chunks`: number of `file.chunk` messages that follow

### file.chunk

```json
{ "transferId": "8f6c...", "index": 0, "data": "iVBORw0KGgo=" }
```

- `index`: zero-based, strictly sequential
- `data`: base64 of the chunk bytes (1 KiB per chunk in current implementations)

Receivers MUST reject a chunk whose `transferId` or `index` does not match the expected next
chunk, and MUST abort the transfer on a gap rather than buffering ambiguous data.

### file.complete

```json
{ "transferId": "8f6c..." }
```

A receiver MUST verify that all announced chunks arrived and that the reassembled length
equals `size` before accepting the file. Current implementations cap a transfer at 200 MB.

Base64 is used because file messages are ordinary control messages. A future transport layer
may replace it with framed binary streams for large payloads.

## BLE framing (ECS1)

A BLE attribute write is limited by the negotiated ATT MTU, so larger messages are split into
frames. All current BLE implementations (Windows, iOS, web bridge) accept both plain UTF-8 JSON
for messages that fit into a single write and the framed form below.

```text
offset | size | field
-------|------|---------------------------------
0      | 4    | magic "ECS1" (0x45 0x43 0x53 0x31)
4      | 2    | frame id   (uint16, little endian)
6      | 2    | frame index(uint16, little endian)
8      | 2    | frame count(uint16, little endian)
10     | n    | UTF-8 payload fragment
```

- `frame index` starts at 0 and MUST increase by exactly one per frame.
- Frame 0 resets the receiver's assembler; a mismatching id/count/index discards the partial
  message instead of stitching unrelated data together.
- A receiver MUST cap the reassembled message (current limit: 4 MiB).
- Senders SHOULD use the largest frame the peer accepts (170-byte payloads, i.e. 180-byte
  frames, cover the common 185-byte MTU) and MUST fall back to 10-byte payloads (20-byte
  frames, the 23-byte default ATT MTU) when a write is rejected.

## Sensitive data rule

Application data such as clipboard contents and file metadata MUST NOT be sent before the security session is authenticated. Plaintext control traffic is limited to the minimum required to negotiate and authenticate the session.
