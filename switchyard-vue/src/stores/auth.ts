import { defineStore } from "pinia";

const STORAGE_KEY_TOKEN = "token";
const STORAGE_KEY_TOKEN_TYPE = "tokenType";
const STORAGE_KEY_USERNAME = "username";
const STORAGE_KEY_ROLE = "role";
const STORAGE_KEY_MUST_CHANGE_PASSWORD = "mustChangePassword";

export interface AuthPayload {
    token: string;
    tokenType?: string;
    username: string;
    role: string;
    mustChangePassword?: boolean;
}

interface AuthState {
    token: string;
    tokenType: string;
    username: string;
    role: string;
    mustChangePassword: boolean;
}

export const useAuthStore = defineStore("auth", {
    state: (): AuthState => ({
        token: "",
        tokenType: "Bearer",
        username: "",
        role: "",
        mustChangePassword: false,
    }),
    getters: {
        isAuthenticated: (state) => !!state.token,
        isAdmin: (state) => state.role.trim().toLowerCase() === "admin",
        needsPasswordChange: (state) => state.mustChangePassword,
    },
    actions: {
        hydrateFromStorage() {
            this.token = localStorage.getItem(STORAGE_KEY_TOKEN) || "";
            this.tokenType =
                localStorage.getItem(STORAGE_KEY_TOKEN_TYPE) || "Bearer";
            this.username = localStorage.getItem(STORAGE_KEY_USERNAME) || "";
            this.role = localStorage.getItem(STORAGE_KEY_ROLE) || "";
            this.mustChangePassword =
                localStorage.getItem(STORAGE_KEY_MUST_CHANGE_PASSWORD) === "1";
        },
        setAuth(payload: AuthPayload) {
            this.token = payload.token;
            this.tokenType = payload.tokenType || "Bearer";
            this.username = payload.username;
            this.role = payload.role;
            this.mustChangePassword = payload.mustChangePassword === true;

            localStorage.setItem(STORAGE_KEY_TOKEN, this.token);
            localStorage.setItem(STORAGE_KEY_TOKEN_TYPE, this.tokenType);
            localStorage.setItem(STORAGE_KEY_USERNAME, this.username);
            localStorage.setItem(STORAGE_KEY_ROLE, this.role);
            localStorage.setItem(
                STORAGE_KEY_MUST_CHANGE_PASSWORD,
                this.mustChangePassword ? "1" : "0",
            );
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
            this.username = "";
            this.role = "";
            this.mustChangePassword = false;

            localStorage.removeItem(STORAGE_KEY_TOKEN);
            localStorage.removeItem(STORAGE_KEY_TOKEN_TYPE);
            localStorage.removeItem(STORAGE_KEY_USERNAME);
            localStorage.removeItem(STORAGE_KEY_ROLE);
            localStorage.removeItem(STORAGE_KEY_MUST_CHANGE_PASSWORD);
        },
    },
});
