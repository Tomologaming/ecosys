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

## Future application messages
- `clipboard.update`
- `file.offer`
- `file.accept`
- `file.chunk`
- `file.complete`

Large binary data SHOULD NOT be embedded as base64 in ordinary control messages. A future transport layer will define framed binary streams.

## Sensitive data rule

Application data such as clipboard contents and file metadata MUST NOT be sent before the security session is authenticated. Plaintext control traffic is limited to the minimum required to negotiate and authenticate the session.