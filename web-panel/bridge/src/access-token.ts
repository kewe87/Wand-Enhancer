const crypto = require('node:crypto');
const fs = require('node:fs');

const { ACCESS_TOKEN_PARAM } = require('./constants');

import type { LogFn } from './types';

const TOKEN_BYTES = 16;
const TOKEN_PATTERN = /^[A-Za-z0-9_-]{22,}$/;

/**
 * The panel is reachable from the whole LAN, so every connection must present this token.
 * It persists across Wand restarts so a paired phone keeps working; deleting the file
 * revokes every paired device on the next start.
 */
function loadOrCreateAccessToken(tokenFile: string, log: LogFn): string {
    try {
        const stored = String(fs.readFileSync(tokenFile, 'utf8')).trim();
        if (TOKEN_PATTERN.test(stored)) {
            return stored;
        }
    } catch {}

    const token = crypto.randomBytes(TOKEN_BYTES).toString('base64url');
    try {
        fs.writeFileSync(tokenFile, token, { mode: 0o600 });
        log('info', `Created a new remote access token in ${tokenFile}.`);
    } catch (error) {
        log(
            'warn',
            'Could not persist the remote access token; phones must rescan after a restart.',
            error,
        );
    }

    return token;
}

function isValidAccessToken(expected: string, candidate: string | null | undefined): boolean {
    if (typeof candidate !== 'string' || !candidate) {
        return false;
    }

    const left = Buffer.from(expected, 'utf8');
    const right = Buffer.from(candidate, 'utf8');
    return left.length === right.length && crypto.timingSafeEqual(left, right);
}

function withAccessToken(url: string, token: string): string {
    const parsed = new URL(url);
    parsed.searchParams.set(ACCESS_TOKEN_PARAM, token);
    return parsed.toString();
}

module.exports = {
    isValidAccessToken,
    loadOrCreateAccessToken,
    withAccessToken,
};
