export interface EncryptedPayload {
  v: 1;
  iv: string;
  cipherText: string;
}

function toBase64(bytes: Uint8Array): string {
  let binary = '';
  for (let i = 0; i < bytes.length; i += 1) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

function fromBase64(b64: string): Uint8Array {
  const binary = atob(b64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

function toArrayBuffer(view: Uint8Array): ArrayBuffer {
  const copy = new Uint8Array(view.byteLength);
  copy.set(view);
  return copy.buffer;
}

export async function generateAesGcmKey(): Promise<CryptoKey> {
  return globalThis.crypto.subtle.generateKey(
    { name: 'AES-GCM', length: 256 },
    true,
    ['encrypt', 'decrypt']
  );
}

export async function exportKeyBase64(key: CryptoKey): Promise<string> {
  const raw = await globalThis.crypto.subtle.exportKey('raw', key);
  return toBase64(new Uint8Array(raw));
}

export async function importKeyBase64(b64: string): Promise<CryptoKey> {
  const bytes = fromBase64(b64);
  return globalThis.crypto.subtle.importKey(
    'raw',
    toArrayBuffer(bytes),
    { name: 'AES-GCM', length: 256 },
    true,
    ['encrypt', 'decrypt']
  );
}

export async function encryptSensitiveJson<T>(data: T, key: CryptoKey): Promise<EncryptedPayload> {
  const iv = globalThis.crypto.getRandomValues(new Uint8Array(12));
  const encoded = new TextEncoder().encode(JSON.stringify(data));
  const encrypted = await globalThis.crypto.subtle.encrypt(
    { name: 'AES-GCM', iv: toArrayBuffer(iv) },
    key,
    toArrayBuffer(encoded)
  );
  return {
    v: 1,
    iv: toBase64(iv),
    cipherText: toBase64(new Uint8Array(encrypted)),
  };
}

export async function decryptSensitiveJson<T>(payload: EncryptedPayload, key: CryptoKey): Promise<T> {
  const iv = fromBase64(payload.iv);
  const cipher = fromBase64(payload.cipherText);
  const decrypted = await globalThis.crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: toArrayBuffer(iv) },
    key,
    toArrayBuffer(cipher)
  );
  const decoded = new TextDecoder().decode(new Uint8Array(decrypted));
  return JSON.parse(decoded) as T;
}

