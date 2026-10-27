/**
 * Generate a UUID-shaped client identifier on both secure and plain-HTTP origins.
 *
 * `crypto.randomUUID()` is unavailable in some browsers when the application is
 * opened through an HTTP server address instead of HTTPS or localhost. Web Crypto's
 * `getRandomValues()` remains available there; the final fallback is sufficient for
 * UI object identifiers, which are not security credentials.
 */
export function createClientId(): string {
    const cryptoApi = globalThis.crypto
    if (typeof cryptoApi?.randomUUID === 'function') return cryptoApi.randomUUID()

    const bytes = new Uint8Array(16)
    if (typeof cryptoApi?.getRandomValues === 'function') {
        cryptoApi.getRandomValues(bytes)
    } else {
        for (let index = 0; index < bytes.length; index++) {
            bytes[index] = Math.floor(Math.random() * 256)
        }
    }

    bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x40
    bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80
    const hex = Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('')
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}
