import { createRouter, createWebHistory } from "vue-router";
import HomeView from "../views/HomeView.vue";
import pinia from "@/stores";
import { useAuthStore } from "@/stores/auth";

const authStore = useAuthStore(pinia);

const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes: [
        {
            path: "/",
            name: "home",
            component: HomeView,
        },
        {
            path: "/about",
            name: "about",
            // route level code-splitting
            // this generates a separate chunk (About.[hash].js) for this route
            // which is lazy-loaded when the route is visited.
            component: () => import("../views/AboutView.vue"),
        },
        {
            path: "/hump",
            name: "hump",
            component: () => import("../hump/HumpMain.vue"),
        },
        {
            path: "/courses",
            name: "courses",
            component: () => import("../course/CourseMain.vue"),
        },
        {
            path: "/login",
            name: "login",
            component: () => import("../views/Login.vue"),
        },
        {
            path: "/userinfo",
            name: "userinfo",
            component: () => import("../views/UserInfo.vue"),
            meta: { requiresAuth: true },
        },
        {
            path: "/usermanager",
            name: "usermanager",
            redirect: "/usermanagement",
        },
        {
            path: "/usermanagement",
            name: "usermanagement",
            component: () => import("../views/UserManagement.vue"),
            meta: { requiresAdmin: true },
        },
        {
            path: "/no-permission",
            name: "no-permission",
            component: () => import("../views/NoPermission.vue"),
        },
        {
            path: "/createuser",
            name: "createuser",
            component: () => import("../views/CreateUser.vue"),
        },
        {
            path: "/capacity",
            name: "capacity",
            component: () => import("../capacity/CapacityMain.vue"),
        },
    ],
});

router.beforeEach((to) => {
    const requiresAuth = to.matched.some(
        (record) => record.meta?.requiresAuth === true,
    );
    const requiresAdmin = to.matched.some(
        (record) => record.meta?.requiresAdmin === true,
    );
    const isLoginRoute = to.path === "/login";
    const isUserInfoRoute = to.path === "/userinfo";

    authStore.hydrateFromStorage();

    if ((requiresAuth || requiresAdmin) && !authStore.isAuthenticated) {
        return {
            path: "/login",
            query: { redirect: to.fullPath },
        };
    }

    if (authStore.isAuthenticated && authStore.needsPasswordChange) {
        if (isLoginRoute) {
            return {
                path: "/userinfo",
                query: { forcePasswordChange: "1" },
            };
        }

        if (!isUserInfoRoute) {
            return {
                path: "/userinfo",
                query: {
                    forcePasswordChange: "1",
                    redirect: to.fullPath,
                },
            };
        }
    }

    if (requiresAdmin && !authStore.isAdmin) {
        return { path: "/no-permission" };
    }

    return true;
});

export default router;
