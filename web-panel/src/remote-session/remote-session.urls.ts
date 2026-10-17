import { WEB_CONTRACT } from '../../protocol/contract';
import { loadString, saveString } from '../shared/storage';

export const WS_QUERY_PARAM = 'ws';

const ACCESS_TOKEN_STORAGE_KEY = 'wand-remote.access-token.v1';
const DEV_SERVER_PORTS = new Set(WEB_CONTRACT.devServerPorts.map(String));

function protocolForWebSocket(): 'ws' | 'wss' {
    return window.location.protocol === 'https:' ? 'wss' : 'ws';
}

function isServedByRemoteBridge(): boolean {
    return (
        window.location.pathname.startsWith(WEB_CONTRACT.basePath) &&
        !DEV_SERVER_PORTS.has(window.location.port)
    );
}

/**
 * The QR code carries the bridge access token once; it is remembered so reloads and
 * home-screen launches keep working, and dropped from the address bar so it does not
 * end up in bookmarks, history or screenshots.
 */
function readAccessToken(params: URLSearchParams): string | null {
    const fromUrl = params.get(WEB_CONTRACT.accessTokenParam)?.trim();
    if (!fromUrl) {
        return loadString(ACCESS_TOKEN_STORAGE_KEY);
    }

    saveString(ACCESS_TOKEN_STORAGE_KEY, fromUrl);
    try {
        const cleaned = new URL(window.location.href);
        cleaned.searchParams.delete(WEB_CONTRACT.accessTokenParam);
        window.history.replaceState(window.history.state, '', cleaned.toString());
    } catch {}

    return fromUrl;
}

function withAccessToken(url: string, token: string | null): string {
    if (!token) {
        return url;
    }

    try {
        const parsed = new URL(url);
        if (!parsed.searchParams.has(WEB_CONTRACT.accessTokenParam)) {
            parsed.searchParams.set(WEB_CONTRACT.accessTokenParam, token);
        }
        return parsed.toString();
    } catch {
        return url;
    }
}

export function readInitialWebSocketUrl(): string {
    const params = new URLSearchParams(window.location.search);
    const token = readAccessToken(params);
    const explicitUrl = params.get(WS_QUERY_PARAM)?.trim();
    if (explicitUrl) {
        return withAccessToken(explicitUrl, token);
    }

    if (isServedByRemoteBridge()) {
        return withAccessToken(
            `${protocolForWebSocket()}://${window.location.host}${WEB_CONTRACT.webSocketPath}`,
            token,
        );
    }

    return withAccessToken(
        `ws://127.0.0.1:${WEB_CONTRACT.defaultRemotePort}${WEB_CONTRACT.webSocketPath}`,
        token,
    );
}
