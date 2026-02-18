import { createRouter, createWebHistory } from "vue-router";
import HomeView from "../views/HomeView.vue";

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
        },
        {
            path: "/usermanager",
            name: "usermanager",
            component: () => import("../views/UserManager.vue"),
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

export default router;
