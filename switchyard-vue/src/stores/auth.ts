import { defineStore } from "pinia";

const STORAGE_KEY_TOKEN = "token";
const STORAGE_KEY_TOKEN_TYPE = "tokenType";
const STORAGE_KEY_TOKEN_EXPIRES_AT = "tokenExpiresAt";
const STORAGE_KEY_REFRESH_TOKEN = "refreshToken";
const STORAGE_KEY_REFRESH_TOKEN_EXPIRES_AT = "refreshTokenExpiresAt";
const STORAGE_KEY_USERNAME = "username";
const STORAGE_KEY_ROLE = "role";
const STORAGE_KEY_MUST_CHANGE_PASSWORD = "mustChangePassword";

export interface AuthPayload {
    token: string;
    tokenType?: string;
    expiresIn?: number;
    refreshToken?: string;
    refreshTokenExpiresIn?: number;
    username: string;
    role: string;
    mustChangePassword?: boolean;
}

interface AuthState {
    token: string;
    tokenType: string;
    tokenExpiresAt: number;
    refreshToken: string;
    refreshTokenExpiresAt: number;
    username: string;
    role: string;
    mustChangePassword: boolean;
}

export const useAuthStore = defineStore("auth", {
    state: (): AuthState => ({
        token: "",
        tokenType: "Bearer",
        tokenExpiresAt: 0,
        refreshToken: "",
        refreshTokenExpiresAt: 0,
        username: "",
        role: "",
        mustChangePassword: false,
    }),
    getters: {
        isAuthenticated: (state) => !!state.token,
        isAdmin: (state) => state.role.trim().toLowerCase() === "admin",
        needsPasswordChange: (state) => state.mustChangePassword,
        hasRefreshToken: (state) => !!state.refreshToken,
    },
    actions: {
        hydrateFromStorage() {
            this.token = localStorage.getItem(STORAGE_KEY_TOKEN) || "";
            this.tokenType =
                localStorage.getItem(STORAGE_KEY_TOKEN_TYPE) || "Bearer";
            this.tokenExpiresAt = Number(
                localStorage.getItem(STORAGE_KEY_TOKEN_EXPIRES_AT) || "0",
            );
            this.refreshToken =
                localStorage.getItem(STORAGE_KEY_REFRESH_TOKEN) || "";
            this.refreshTokenExpiresAt = Number(
                localStorage.getItem(STORAGE_KEY_REFRESH_TOKEN_EXPIRES_AT) || "0",
            );
            this.username = localStorage.getItem(STORAGE_KEY_USERNAME) || "";
            this.role = localStorage.getItem(STORAGE_KEY_ROLE) || "";
            this.mustChangePassword =
                localStorage.getItem(STORAGE_KEY_MUST_CHANGE_PASSWORD) === "1";
        },
        setAuth(payload: AuthPayload) {
            const now = Math.floor(Date.now() / 1000);

            this.token = payload.token;
            this.tokenType = payload.tokenType || "Bearer";
            this.tokenExpiresAt =
                typeof payload.expiresIn === "number" && payload.expiresIn > 0
                    ? now + payload.expiresIn
                    : 0;
            this.refreshToken = payload.refreshToken || "";
            this.refreshTokenExpiresAt =
                typeof payload.refreshTokenExpiresIn === "number" &&
                payload.refreshTokenExpiresIn > 0
                    ? now + payload.refreshTokenExpiresIn
                    : 0;
            this.username = payload.username;
            this.role = payload.role;
            this.mustChangePassword = payload.mustChangePassword === true;

            localStorage.setItem(STORAGE_KEY_TOKEN, this.token);
            localStorage.setItem(STORAGE_KEY_TOKEN_TYPE, this.tokenType);
            localStorage.setItem(
                STORAGE_KEY_TOKEN_EXPIRES_AT,
                String(this.tokenExpiresAt),
            );
            localStorage.setItem(STORAGE_KEY_REFRESH_TOKEN, this.refreshToken);
            localStorage.setItem(
                STORAGE_KEY_REFRESH_TOKEN_EXPIRES_AT,
                String(this.refreshTokenExpiresAt),
            );
            localStorage.setItem(STORAGE_KEY_USERNAME, this.username);
            localStorage.setItem(STORAGE_KEY_ROLE, this.role);
            localStorage.setItem(
                STORAGE_KEY_MUST_CHANGE_PASSWORD,
                this.mustChangePassword ? "1" : "0",
            );
        },
        isTokenExpired(bufferSeconds = 0) {
            if (!this.token || this.tokenExpiresAt <= 0) {
                return true;
            }

            const now = Math.floor(Date.now() / 1000);
            return this.tokenExpiresAt - now <= bufferSeconds;
        },
        isRefreshTokenExpired(bufferSeconds = 0) {
            if (!this.refreshToken || this.refreshTokenExpiresAt <= 0) {
                return true;
            }

            const now = Math.floor(Date.now() / 1000);
            return this.refreshTokenExpiresAt - now <= bufferSeconds;
        },
        setMustChangePassword(required: boolean) {
            this.mustChangePassword = required;
            localStorage.setItem(
                STORAGE_KEY_MUST_CHANGE_PASSWORD,
                required ? "1" : "0",
            );
        },
        updateProfile(profile: { username?: string; role?: string }) {
            if (typeof profile.username === "string") {
                this.username = profile.username;
                localStorage.setItem(STORAGE_KEY_USERNAME, this.username);
            }

            if (typeof profile.role === "string") {
                this.role = profile.role;
                localStorage.setItem(STORAGE_KEY_ROLE, this.role);
            }
        },
        clearAuth() {
            this.token = "";
            this.tokenType = "Bearer";
            this.tokenExpiresAt = 0;
            this.refreshToken = "";
            this.refreshTokenExpiresAt = 0;
            this.username = "";
            this.role = "";
            this.mustChangePassword = false;

            localStorage.removeItem(STORAGE_KEY_TOKEN);
            localStorage.removeItem(STORAGE_KEY_TOKEN_TYPE);
            localStorage.removeItem(STORAGE_KEY_TOKEN_EXPIRES_AT);
            localStorage.removeItem(STORAGE_KEY_REFRESH_TOKEN);
            localStorage.removeItem(STORAGE_KEY_REFRESH_TOKEN_EXPIRES_AT);
            localStorage.removeItem(STORAGE_KEY_USERNAME);
            localStorage.removeItem(STORAGE_KEY_ROLE);
            localStorage.removeItem(STORAGE_KEY_MUST_CHANGE_PASSWORD);
        },
    },
});
