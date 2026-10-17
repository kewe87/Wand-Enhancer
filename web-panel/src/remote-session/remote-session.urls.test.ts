import { beforeEach, describe, expect, it } from 'vitest';

import { readInitialWebSocketUrl } from './remote-session.urls';

describe('remote session access token', () => {
    beforeEach(() => {
        localStorage.clear();
        window.history.replaceState(null, '', '/remote/');
    });

    it('takes the token from the QR url, remembers it and drops it from the address bar', () => {
        window.history.replaceState(null, '', '/remote/?token=abc123&lang=de');

        const url = new URL(readInitialWebSocketUrl());

        expect(url.pathname).toBe('/remote/ws');
        expect(url.searchParams.get('token')).toBe('abc123');
        expect(window.location.search).toBe('?lang=de');
        expect(new URL(readInitialWebSocketUrl()).searchParams.get('token')).toBe('abc123');
    });

    it('replaces a remembered token when a new QR code is scanned', () => {
        window.history.replaceState(null, '', '/remote/?token=old');
        readInitialWebSocketUrl();
        window.history.replaceState(null, '', '/remote/?token=new');

        expect(new URL(readInitialWebSocketUrl()).searchParams.get('token')).toBe('new');
    });

    it('keeps a token that an explicit websocket url already carries', () => {
        window.history.replaceState(
            null,
            '',
            `/remote/?token=stored&ws=${encodeURIComponent('ws://10.0.0.2:3223/remote/ws?token=explicit')}`,
        );

        expect(new URL(readInitialWebSocketUrl()).searchParams.get('token')).toBe('explicit');
    });

    it('connects without a token when none was ever provided', () => {
        expect(new URL(readInitialWebSocketUrl()).searchParams.has('token')).toBe(false);
    });
});
