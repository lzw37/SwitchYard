<script setup lang="js">
import { useRouter } from 'vue-router';
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { ElMessage } from 'element-plus';
import i18n from '../i18n';
import { useAuthStore } from '@/stores/auth';

const router = useRouter();
const { t } = useI18n({ useScope: 'global' });
const authStore = useAuthStore();

authStore.hydrateFromStorage();

const userDisplayName = computed(() => authStore.username.trim() || t('common.userMenu.guest'));
const userDisplayRole = computed(() => {
    const role = authStore.role.trim();
    if (!role) return t('createUser.roles.user');

    const normalizedRole = role.toLowerCase();
    if (normalizedRole === 'admin') return t('createUser.roles.admin');
    if (normalizedRole === 'user') return t('createUser.roles.user');

    return role;
});

const handleUserMenuCommand = (command) => {
    if (command === 'userinfo') {
        router.push('/userinfo');
        return;
    }

    if (command === 'usermanagement') {
        router.push('/usermanagement');
        return;
    }

    if (command === 'logout') {
        authStore.clearAuth();
        ElMessage.success(t('common.userMenu.loggedOut'));
        router.replace('/login');
    }
};

// 语言切换（与 src/i18n.ts 配合）
const locale = ref(i18n.global?.locale?.value ?? i18n.global?.locale ?? 'en');
watch(locale, (v) => {
    try {
        if (i18n.global?.locale?.value !== undefined) i18n.global.locale.value = v;
        else i18n.global.locale = v;
    } catch (e) {
        console.warn('set locale failed', e);
    }
    localStorage.setItem('locale', v);
});

function toggleLocale() {
    locale.value = locale.value === 'en' ? 'zh' : 'en';
}

// 平滑滚动
document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
        e.preventDefault();
        const target = document.querySelector(this.getAttribute('href'));
        if (target) {
            target.scrollIntoView({
                behavior: 'smooth',
                block: 'start'
            });
        }
    });
});

// 导航栏滚动效果
let lastScroll = 0;
const navbar = document.querySelector('.navbar');
let ticking = false;

window.addEventListener('scroll', () => {
    if (!ticking) {
        window.requestAnimationFrame(() => {
            const currentScroll = window.pageYOffset;

            if (currentScroll <= 0) {
                navbar.style.boxShadow = '0 2px 10px rgba(0, 0, 0, 0.1)';
            } else {
                navbar.style.boxShadow = '0 2px 20px rgba(0, 0, 0, 0.2)';
            }

            lastScroll = currentScroll;
            ticking = false;
        });
        ticking = true;
    }
});

// 滚动动画
const observerOptions = {
    threshold: 0.1,
    rootMargin: '0px 0px -100px 0px'
};

const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            entry.target.classList.add('visible');
        }
    });
}, observerOptions);

// 添加淡入动画到所有卡片
document.addEventListener('DOMContentLoaded', () => {
    const cards = document.querySelectorAll('.feature-card, .tech-section, .security-card, .stat-item, .contact-info, .contact-links');
    cards.forEach(card => {
        card.classList.add('fade-in');
        observer.observe(card);
    });
});

// 视差滚动效果已移除以避免跳动

// 添加动态数字计数动画
function animateNumber(element, target, duration = 2000) {
    const start = 0;
    const increment = target / (duration / 16);
    let current = start;

    const timer = setInterval(() => {
        current += increment;
        if (current >= target) {
            element.textContent = target + (element.textContent.includes('+') ? '+' : element.textContent.includes('%') ? '%' : '');
            clearInterval(timer);
        } else {
            element.textContent = Math.floor(current) + (element.textContent.includes('+') ? '+' : element.textContent.includes('%') ? '%' : '');
        }
    }, 16);
}

// 当统计数字进入视口时开始计数
const statObserver = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
        if (entry.isIntersecting && !entry.target.classList.contains('counted')) {
            const number = entry.target.querySelector('.stat-number');
            if (number) {
                const text = number.textContent;
                const value = parseInt(text);
                if (!isNaN(value)) {
                    number.textContent = '0';
                    animateNumber(number, value);
                    entry.target.classList.add('counted');
                }
            }
        }
    });
}, { threshold: 0.5 });

document.addEventListener('DOMContentLoaded', () => {
    const statItems = document.querySelectorAll('.stat-item');
    statItems.forEach(item => statObserver.observe(item));
});

// 移动端菜单切换（预留）
const createMobileMenu = () => {
    const navbar = document.querySelector('.navbar .container');
    const navMenu = document.querySelector('.nav-menu');

    if (window.innerWidth <= 768 && !document.querySelector('.menu-toggle')) {
        const menuToggle = document.createElement('button');
        menuToggle.className = 'menu-toggle';
        menuToggle.innerHTML = '☰';
        menuToggle.style.cssText = `
            background: none;
            border: none;
            color: white;
            font-size: 1.5rem;
            cursor: pointer;
            display: block;
        `;

        navbar.appendChild(menuToggle);

        menuToggle.addEventListener('click', () => {
            navMenu.style.display = navMenu.style.display === 'flex' ? 'none' : 'flex';
            if (navMenu.style.display === 'flex') {
                navMenu.style.cssText = `
                    position: absolute;
                    top: 100%;
                    left: 0;
                    right: 0;
                    background: var(--primary-blue);
                    flex-direction: column;
                    padding: 1rem;
                    gap: 1rem;
                `;
            }
        });
    }
};

window.addEventListener('resize', createMobileMenu);
window.addEventListener('DOMContentLoaded', createMobileMenu);

// 彩蛋：鼠标移动背景效果（使用节流优化性能）
let mouseMoveTicking = false;
document.addEventListener('mousemove', (e) => {
    if (!mouseMoveTicking && window.pageYOffset < window.innerHeight) {
        window.requestAnimationFrame(() => {
            const hero = document.querySelector('.hero');
            if (hero) {
                const x = e.clientX / window.innerWidth;
                const y = e.clientY / window.innerHeight;
                hero.style.backgroundPosition = `${50 + x * 3}% ${50 + y * 3}%`;
            }
            mouseMoveTicking = false;
        });
        mouseMoveTicking = true;
    }
});

// 添加加载动画
window.addEventListener('load', () => {
    document.body.style.opacity = '0';
    setTimeout(() => {
        document.body.style.transition = 'opacity 0.5s ease';
        document.body.style.opacity = '1';
    }, 100);
});

console.log('🚂 SwitchYard - 铁路站场与枢纽教学工具包');
console.log('欢迎访问 SwitchYard 项目主页！');

</script>

<template>
    <nav class="navbar">
        <div class="container">
            <div class="logo">
                <span class="logo-icon">🚂</span>
                <span class="logo-text">{{ t('home.brand') }}</span>
            </div>
            <div class="nav-right">
                <ul class="nav-menu">
                    <li><a href="#home">{{ t('home.nav.home') }}</a></li>
                    <li><a href="#features">{{ t('home.nav.features') }}</a></li>
                    <li><a href="#tech">{{ t('home.nav.tech') }}</a></li>
                    <li><a href="#about">{{ t('home.nav.about') }}</a></li>
                    <li><a href="#contact">{{ t('home.nav.contact') }}</a></li>
                </ul>
                <button class="lang-toggle" @click="toggleLocale"
                    :title="locale === 'en' ? t('home.lang.switchToZh') : t('home.lang.switchToEn')">{{ locale === 'en'
                        ? t('home.lang.en') : t('home.lang.zh') }}</button>
                <el-dropdown class="user-dropdown" @command="handleUserMenuCommand">
                    <span class="user-trigger">
                        <span class="user-name">{{ userDisplayName }}</span>
                        <span class="user-role">{{ userDisplayRole }}</span>
                    </span>
                    <template #dropdown>
                        <el-dropdown-menu>
                            <el-dropdown-item command="userinfo">
                                {{ t('userInfo.title') }}
                            </el-dropdown-item>
                            <el-dropdown-item v-if="authStore.isAdmin" command="usermanagement">
                                {{ t('common.userMenu.userManagement') }}
                            </el-dropdown-item>
                            <el-dropdown-item divided command="logout">
                                {{ t('common.userMenu.logout') }}
                            </el-dropdown-item>
                        </el-dropdown-menu>
                    </template>
                </el-dropdown>
            </div>
        </div>
    </nav>

    <!-- 英雄区域 -->
    <section id="home" class="hero">
        <div class="hero-overlay"></div>
        <div class="hero-content">
            <h1 class="hero-title">{{ t('home.brand') }}</h1>
            <p class="hero-subtitle">{{ t('home.hero.subtitle') }}</p>
            <p class="hero-description">{{ t('home.hero.description') }}</p>
            <div class="hero-buttons">
                <a href="/hump" class="btn btn-primary">{{ t('home.hero.btnHump') }}</a>
                <a href="https://gitee.com/lzw37/switchyard" class="btn btn-secondary" target="_blank">{{
                    t('home.hero.btnSource') }}</a>
            </div>
        </div>
        <div class="scroll-indicator">
            <div class="scroll-arrow"></div>
        </div>
    </section>

    <!-- 核心功能 -->
    <section id="features" class="features">
        <div class="container">
            <h2 class="section-title">{{ t('home.features.title') }}</h2>
            <p class="section-subtitle">{{ t('home.features.subtitle') }}</p>

            <div class="features-grid">
                <div class="feature-card">
                    <div class="feature-icon">🏗️</div>
                    <h3>{{ t('home.features.cards.layout.title') }}</h3>
                    <p>{{ t('home.features.cards.layout.desc') }}</p>
                </div>

                <div class="feature-card">
                    <div class="feature-icon">📐</div>
                    <h3>{{ t('home.features.cards.slope.title') }}</h3>
                    <p>{{ t('home.features.cards.slope.desc') }}</p>
                </div>

                <div class="feature-card">
                    <div class="feature-icon">🎬</div>
                    <h3>{{ t('home.features.cards.sim.title') }}</h3>
                    <p>{{ t('home.features.cards.sim.desc') }}</p>
                </div>

                <div class="feature-card">
                    <div class="feature-icon">📊</div>
                    <h3>{{ t('home.features.cards.velocity.title') }}</h3>
                    <p>{{ t('home.features.cards.velocity.desc') }}</p>
                </div>

                <div class="feature-card">
                    <div class="feature-icon">⏱️</div>
                    <h3>{{ t('home.features.cards.time.title') }}</h3>
                    <p>{{ t('home.features.cards.time.desc') }}</p>
                </div>

                <div class="feature-card">
                    <div class="feature-icon">⚡</div>
                    <h3>{{ t('home.features.cards.energy.title') }}</h3>
                    <p>{{ t('home.features.cards.energy.desc') }}</p>
                </div>

                <div class="feature-card">
                    <div class="feature-icon">🚃</div>
                    <h3>{{ t('home.features.cards.wagon.title') }}</h3>
                    <p>{{ t('home.features.cards.wagon.desc') }}</p>
                </div>

                <div class="feature-card">
                    <div class="feature-icon">🔒</div>
                    <h3>{{ t('home.features.cards.safety.title') }}</h3>
                    <p>{{ t('home.features.cards.safety.desc') }}</p>
                </div>
            </div>
        </div>
    </section>

    <!-- 技术栈 -->
    <section id="tech" class="tech-stack">
        <div class="container">
            <h2 class="section-title">{{ t('home.tech.title') }}</h2>
            <p class="section-subtitle">{{ t('home.tech.subtitle') }}</p>

            <div class="tech-grid">
                <div class="tech-section">
                    <h3 class="tech-title">{{ t('home.tech.frontend.title') }}</h3>
                    <div class="tech-items">
                        <div class="tech-item">
                            <span class="tech-label">{{ t('home.tech.frontend.framework') }}</span>
                            <span class="tech-value">Vue 3.5 + TypeScript</span>
                        </div>
                        <div class="tech-item">
                            <span class="tech-label">{{ t('home.tech.frontend.build') }}</span>
                            <span class="tech-value">Vite 7.2</span>
                        </div>
                        <div class="tech-item">
                            <span class="tech-label">{{ t('home.tech.frontend.ui') }}</span>
                            <span class="tech-value">Element Plus 2.13</span>
                        </div>
                        <div class="tech-item">
                            <span class="tech-label">{{ t('home.tech.frontend.router') }}</span>
                            <span class="tech-value">Vue Router 4.6</span>
                        </div>
                        <div class="tech-item">
                            <span class="tech-label">{{ t('home.tech.frontend.http') }}</span>
                            <span class="tech-value">Axios 1.13</span>
                        </div>
                    </div>
                </div>

                <div class="tech-section">
                    <h3 class="tech-title">{{ t('home.tech.backend.title') }}</h3>
                    <div class="tech-items">
                        <div class="tech-item">
                            <span class="tech-label">{{ t('home.tech.backend.framework') }}</span>
                            <span class="tech-value">ASP.NET Core 8.0</span>
                        </div>
                        <div class="tech-item">
                            <span class="tech-label">{{ t('home.tech.backend.language') }}</span>
                            <span class="tech-value">C# (.NET 8.0)</span>
                        </div>
                        <div class="tech-item">
                            <span class="tech-label">{{ t('home.tech.backend.auth') }}</span>
                            <span class="tech-value">JWT Token</span>
                        </div>
                        <div class="tech-item">
                            <span class="tech-label">{{ t('home.tech.backend.database') }}</span>
                            <span class="tech-value">SQLite / MySQL</span>
                        </div>
                        <div class="tech-item">
                            <span class="tech-label">{{ t('home.tech.backend.security') }}</span>
                            <span class="tech-value">{{ t('home.tech.backend.securityValue') }}</span>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </section>

    <!-- 安全特性 -->
    <section class="security">
        <div class="container">
            <h2 class="section-title">{{ t('home.security.title') }}</h2>
            <div class="security-grid">
                <div class="security-card">
                    <div class="security-icon">🔐</div>
                    <h3>{{ t('home.security.cards.password.title') }}</h3>
                    <p>{{ t('home.security.cards.password.p1') }}</p>
                    <p>{{ t('home.security.cards.password.p2') }}</p>
                </div>

                <div class="security-card">
                    <div class="security-icon">🎫</div>
                    <h3>{{ t('home.security.cards.jwt.title') }}</h3>
                    <p>{{ t('home.security.cards.jwt.p1') }}</p>
                    <p>{{ t('home.security.cards.jwt.p2') }}</p>
                </div>

                <div class="security-card">
                    <div class="security-icon">🌐</div>
                    <h3>{{ t('home.security.cards.https.title') }}</h3>
                    <p>{{ t('home.security.cards.https.p1') }}</p>
                    <p>{{ t('home.security.cards.https.p2') }}</p>
                </div>
            </div>
        </div>
    </section>

    <!-- 关于 -->
    <section id="about" class="about">
        <div class="container">
            <h2 class="section-title">{{ t('home.about.title') }}</h2>
            <div class="about-content">
                <div class="about-text">
                    <h3>{{ t('home.about.professional.title') }}</h3>
                    <p>{{ t('home.about.professional.p') }}</p>

                    <h3>{{ t('home.about.modules.title') }}</h3>
                    <p>{{ t('home.about.modules.p') }}</p>

                    <h3>{{ t('home.about.open.title') }}</h3>
                    <p>{{ t('home.about.open.p') }}</p>
                </div>

                <!-- <div class="about-stats">
                    <div class="stat-item">
                        <div class="stat-number">8+</div>
                        <div class="stat-label">核心功能</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-number">2</div>
                        <div class="stat-label">技术栈</div>
                    </div>
                    <div class="stat-item">
                        <div class="stat-number">100%</div>
                        <div class="stat-label">开源</div>
                    </div>
                </div> -->
            </div>
        </div>
    </section>

    <!-- 联系我们 -->
    <section id="contact" class="contact">
        <div class="container">
            <h2 class="section-title">{{ t('home.contact.title') }}</h2>
            <div class="contact-content">
                <div class="contact-info">
                    <h3>{{ t('home.contact.maintainer.title') }}</h3>
                    <p>{{ t('home.contact.maintainer.organization') }}</p>
                    <p>{{ t('home.contact.maintainer.authors') }}</p>
                </div>

                <div class="contact-links">
                    <h3>{{ t('home.contact.links.title') }}</h3>
                    <a href="https://github.com/lzw37/switchyard" class="contact-link" target="_blank"
                        style="margin:5px;">
                        <span class="link-icon"><svg viewBox="0 0 16 16" width="24" height="24" fill="currentColor">
                                <path
                                    d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z">
                                </path>
                            </svg></span>
                        <span>{{ t('home.contact.links.github') }}</span>
                    </a>
                    <a href="https://gitee.com/lzw37/switchyard" class="contact-link" target="_blank"
                        style="margin:5px;">
                        <span class="link-icon"><svg viewBox="0 0 24 24" width="24" height="24" fill="currentColor">
                                <path
                                    d="M11.984 0A12 12 0 0 0 0 12a12 12 0 0 0 12 12 12 12 0 0 0 12-12A12 12 0 0 0 12 0a12 12 0 0 0-.016 0zm6.09 5.333c.328 0 .593.266.592.593v1.482a.594.594 0 0 1-.593.592H9.777c-.982 0-1.778.796-1.778 1.778v5.63c0 .327.266.592.593.592h5.63c.982 0 1.778-.796 1.778-1.778v-.296a.593.593 0 0 0-.592-.593h-4.15a.592.592 0 0 1-.592-.592v-1.482a.593.593 0 0 1 .593-.592h6.815c.327 0 .593.265.593.592v3.408a4 4 0 0 1-4 4H5.926a.593.593 0 0 1-.593-.593V9.778a4.444 4.444 0 0 1 4.445-4.444h8.296Z" />
                            </svg></span>
                        <span>{{ t('home.contact.links.gitee') }}</span>
                    </a>
                </div>

            </div>
        </div>
    </section>

    <!-- 页脚 -->
    <footer class="footer">
        <div class="container">
            <p>{{ t('home.footer.copy') }}</p>
            <p>{{ t('home.footer.tagline') }}</p>
        </div>
    </footer>
</template>

<style>
/* 全局样式（不加 scoped，确保 CSS 变量和基础重置生效） */
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

:root {
    --primary-blue: #1e3a8a;
    --secondary-blue: #3b82f6;
    --light-blue: #60a5fa;
    --dark-blue: #1e40af;
    --accent-blue: #2563eb;
    --bg-light: #f0f9ff;
    --bg-white: #ffffff;
    --text-dark: #1e293b;
    --text-gray: #64748b;
    --border-color: #cbd5e1;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', sans-serif;
    line-height: 1.6;
    color: var(--text-dark);
    overflow-x: hidden;
}

html {
    scroll-behavior: smooth;
}
</style>

<style scoped>
.container {
    max-width: 1200px;
    margin: 0 auto;
    padding: 0 20px;
}

/* 导航栏 */
.navbar {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    background: rgba(30, 58, 138, 0.95);
    backdrop-filter: blur(10px);
    padding: 1rem 0;
    z-index: 1000;
    box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
}

.navbar .container {
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.logo {
    display: flex;
    align-items: center;
    gap: 10px;
    font-size: 1.5rem;
    font-weight: bold;
    color: white;
}

.logo-icon {
    font-size: 2rem;
}

.nav-menu {
    display: flex;
    list-style: none;
    gap: 2rem;
}

.nav-menu a {
    color: white;
    text-decoration: none;
    font-weight: 500;
    transition: color 0.3s ease;
    position: relative;
}

.nav-menu a:hover {
    color: var(--light-blue);
}

.nav-menu a::after {
    content: '';
    position: absolute;
    bottom: -5px;
    left: 0;
    width: 0;
    height: 2px;
    background: var(--light-blue);
    transition: width 0.3s ease;
}

.nav-menu a:hover::after {
    width: 100%;
}

/* 语言切换布局 */
.nav-right {
    display: flex;
    align-items: center;
    gap: 1rem;
}

.lang-toggle {
    background: rgba(255, 255, 255, 0.12);
    color: white;
    border: 1px solid rgba(255, 255, 255, 0.18);
    padding: 0.35rem 0.6rem;
    border-radius: 8px;
    cursor: pointer;
    font-weight: 600;
    transition: all 0.2s ease;
}

.lang-toggle:hover {
    background: rgba(255, 255, 255, 0.18);
    transform: translateY(-2px);
}

.user-dropdown {
    margin-left: 0.4rem;
}

.user-trigger {
    display: flex;
    align-items: center;
    gap: 0.45rem;
    padding: 0.35rem 0.7rem;
    border-radius: 10px;
    background: rgba(255, 255, 255, 0.14);
    border: 1px solid rgba(255, 255, 255, 0.2);
    color: #ffffff;
    cursor: pointer;
    transition: background 0.2s ease, transform 0.2s ease;
}

.user-trigger:hover {
    background: rgba(255, 255, 255, 0.22);
    transform: translateY(-2px);
}

.user-name {
    font-weight: 700;
    max-width: 120px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.user-role {
    font-size: 0.78rem;
    opacity: 0.88;
    padding: 0.08rem 0.45rem;
    border-radius: 999px;
    border: 1px solid rgba(255, 255, 255, 0.28);
}

/* 英雄区域 */
.hero {
    position: relative;
    height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    text-align: center;
    background: linear-gradient(135deg, var(--primary-blue) 0%, var(--secondary-blue) 100%);
    overflow: hidden;
}

.hero::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: url('data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 600"><defs><linearGradient id="grad" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" style="stop-color:rgb(30,58,138);stop-opacity:0.3" /><stop offset="100%" style="stop-color:rgb(59,130,246);stop-opacity:0.3" /></linearGradient></defs><path d="M0,300 Q150,200 300,300 T600,300 T900,300 T1200,300 L1200,600 L0,600 Z" fill="url(%23grad)" opacity="0.3"/><rect x="100" y="280" width="80" height="20" fill="white" opacity="0.2"/><rect x="200" y="280" width="80" height="20" fill="white" opacity="0.2"/><rect x="300" y="280" width="80" height="20" fill="white" opacity="0.2"/><circle cx="150" cy="310" r="5" fill="white" opacity="0.4"/><circle cx="250" cy="310" r="5" fill="white" opacity="0.4"/><circle cx="350" cy="310" r="5" fill="white" opacity="0.4"/><path d="M50,290 L1150,290" stroke="white" stroke-width="3" opacity="0.2"/><path d="M100,200 L200,250 L300,220 L400,260 L500,230" stroke="white" stroke-width="2" fill="none" opacity="0.3"/></svg>');
    background-size: cover;
    background-position: center;
    opacity: 0.6;
}

.hero-overlay {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: radial-gradient(circle at center, transparent 0%, rgba(0, 0, 0, 0.2) 100%);
}

.hero-content {
    position: relative;
    z-index: 1;
    color: white;
    padding: 0 20px;
    animation: fadeInUp 1s ease;
}

@keyframes fadeInUp {
    from {
        opacity: 0;
        transform: translateY(30px);
    }

    to {
        opacity: 1;
        transform: translateY(0);
    }
}

.hero-title {
    font-size: 4rem;
    font-weight: bold;
    margin-bottom: 1rem;
    text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.3);
    letter-spacing: 2px;
}

.hero-subtitle {
    font-size: 1.8rem;
    margin-bottom: 1rem;
    text-shadow: 1px 1px 2px rgba(0, 0, 0, 0.3);
}

.hero-description {
    font-size: 1.2rem;
    margin-bottom: 2rem;
    opacity: 0.9;
}

.hero-buttons {
    display: flex;
    gap: 1rem;
    justify-content: center;
    flex-wrap: wrap;
}

.btn {
    padding: 0.875rem 2rem;
    border-radius: 50px;
    text-decoration: none;
    font-weight: 600;
    transition: all 0.3s ease;
    display: inline-block;
    font-size: 1rem;
}

.btn-primary {
    background: white;
    color: var(--primary-blue);
    box-shadow: 0 4px 15px rgba(255, 255, 255, 0.3);
}

.btn-primary:hover {
    transform: translateY(-3px);
    box-shadow: 0 6px 20px rgba(255, 255, 255, 0.4);
}

.btn-secondary {
    background: transparent;
    color: white;
    border: 2px solid white;
}

.btn-secondary:hover {
    background: white;
    color: var(--primary-blue);
    transform: translateY(-3px);
}

.scroll-indicator {
    position: absolute;
    bottom: 30px;
    left: 50%;
    transform: translateX(-50%);
    animation: bounce 2s infinite;
}

@keyframes bounce {

    0%,
    20%,
    50%,
    80%,
    100% {
        transform: translateX(-50%) translateY(0);
    }

    40% {
        transform: translateX(-50%) translateY(-10px);
    }

    60% {
        transform: translateX(-50%) translateY(-5px);
    }
}

.scroll-arrow {
    width: 30px;
    height: 30px;
    border-left: 3px solid white;
    border-bottom: 3px solid white;
    transform: rotate(-45deg);
}

/* 通用区域样式 */
section {
    padding: 5rem 0;
}

.section-title {
    font-size: 2.5rem;
    text-align: center;
    margin-bottom: 1rem;
    color: var(--primary-blue);
    font-weight: bold;
}

.section-subtitle {
    text-align: center;
    color: var(--text-gray);
    font-size: 1.1rem;
    margin-bottom: 3rem;
}

/* 核心功能 */
.features {
    background: var(--bg-light);
}

.features-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
    gap: 2rem;
}

.feature-card {
    background: white;
    padding: 2rem;
    border-radius: 15px;
    text-align: center;
    transition: all 0.3s ease;
    box-shadow: 0 2px 10px rgba(0, 0, 0, 0.05);
    border: 2px solid transparent;
}

.feature-card:hover {
    transform: translateY(-10px);
    box-shadow: 0 10px 30px rgba(30, 58, 138, 0.15);
    border-color: var(--light-blue);
}

.feature-icon {
    font-size: 3rem;
    margin-bottom: 1rem;
}

.feature-card h3 {
    color: var(--primary-blue);
    margin-bottom: 0.5rem;
    font-size: 1.3rem;
}

.feature-card p {
    color: var(--text-gray);
    line-height: 1.6;
}

/* 技术栈 */
.tech-stack {
    background: white;
}

.tech-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: 3rem;
}

.tech-section {
    background: var(--bg-light);
    padding: 2rem;
    border-radius: 15px;
    border-left: 5px solid var(--secondary-blue);
}

.tech-title {
    color: var(--primary-blue);
    font-size: 1.5rem;
    margin-bottom: 1.5rem;
    text-align: center;
}

.tech-items {
    display: flex;
    flex-direction: column;
    gap: 1rem;
}

.tech-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 0.75rem;
    background: white;
    border-radius: 8px;
    transition: all 0.3s ease;
}

.tech-item:hover {
    transform: translateX(5px);
    box-shadow: 0 2px 10px rgba(30, 58, 138, 0.1);
}

.tech-label {
    font-weight: 600;
    color: var(--text-dark);
}

.tech-value {
    color: var(--secondary-blue);
    font-weight: 500;
}

/* 安全特性 */
.security {
    background: linear-gradient(135deg, var(--primary-blue) 0%, var(--dark-blue) 100%);
    color: white;
}

.security .section-title {
    color: white;
}

.security-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
    gap: 2rem;
}

.security-card {
    background: rgba(255, 255, 255, 0.1);
    backdrop-filter: blur(10px);
    padding: 2rem;
    border-radius: 15px;
    text-align: center;
    border: 2px solid rgba(255, 255, 255, 0.2);
    transition: all 0.3s ease;
}

.security-card:hover {
    transform: translateY(-10px);
    background: rgba(255, 255, 255, 0.15);
    border-color: rgba(255, 255, 255, 0.4);
}

.security-icon {
    font-size: 3rem;
    margin-bottom: 1rem;
}

.security-card h3 {
    margin-bottom: 1rem;
    font-size: 1.3rem;
}

.security-card p {
    opacity: 0.9;
    margin-bottom: 0.5rem;
}

/* 关于 */
.about {
    background: white;
}

.about-content {
    display: grid;
    grid-template-columns: 2fr 1fr;
    gap: 3rem;
    align-items: center;
}

.about-text h3 {
    color: var(--primary-blue);
    margin-bottom: 1rem;
    font-size: 1.5rem;
}

.about-text p {
    color: var(--text-gray);
    margin-bottom: 2rem;
    line-height: 1.8;
}

.about-stats {
    display: flex;
    flex-direction: column;
    gap: 2rem;
}

.stat-item {
    text-align: center;
    padding: 2rem;
    background: var(--bg-light);
    border-radius: 15px;
    border: 2px solid var(--secondary-blue);
}

.stat-number {
    font-size: 3rem;
    font-weight: bold;
    color: var(--secondary-blue);
    margin-bottom: 0.5rem;
}

.stat-label {
    color: var(--text-gray);
    font-weight: 500;
}

/* 联系我们 */
.contact {
    background: var(--bg-light);
}

.contact-content {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: 3rem;
    text-align: center;
}

.contact-info,
.contact-links {
    background: white;
    padding: 2rem;
    border-radius: 15px;
    box-shadow: 0 2px 10px rgba(0, 0, 0, 0.05);
    margin: 5px;
}

.contact-info h3,
.contact-links h3 {
    color: var(--primary-blue);
    margin-bottom: 1.5rem;
    font-size: 1.3rem;
}

.contact-info p {
    color: var(--text-gray);
    margin-bottom: 0.5rem;
}

.contact-link {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.5rem;
    padding: 1rem;
    background: var(--secondary-blue);
    color: white;
    text-decoration: none;
    border-radius: 10px;
    font-weight: 600;
    transition: all 0.3s ease;
}

.contact-link:hover {
    background: var(--dark-blue);
    transform: translateY(-3px);
    box-shadow: 0 5px 15px rgba(30, 58, 138, 0.3);
}

.link-icon {
    font-size: 1.5rem;
}

/* 页脚 */
.footer {
    background: var(--primary-blue);
    color: white;
    text-align: center;
    padding: 2rem 0;
}

.footer p {
    margin-bottom: 0.5rem;
    opacity: 0.9;
}

/* 响应式设计 */
@media (max-width: 768px) {
    .nav-menu {
        display: none;
    }

    .nav-right {
        gap: 0.5rem;
    }

    .user-trigger {
        padding: 0.3rem 0.55rem;
    }

    .user-name {
        max-width: 78px;
    }

    .user-role {
        display: none;
    }

    .hero-title {
        font-size: 2.5rem;
    }

    .hero-subtitle {
        font-size: 1.3rem;
    }

    .hero-description {
        font-size: 1rem;
    }

    .section-title {
        font-size: 2rem;
    }

    .features-grid,
    .tech-grid,
    .security-grid {
        grid-template-columns: 1fr;
    }

    .about-content {
        grid-template-columns: 1fr;
    }

    .hero-buttons {
        flex-direction: column;
        align-items: center;
    }

    .btn {
        width: 100%;
        max-width: 300px;
    }
}

@media (max-width: 480px) {
    .hero-title {
        font-size: 2rem;
    }

    .section-title {
        font-size: 1.5rem;
    }

    section {
        padding: 3rem 0;
    }
}

/* 滚动动画 */
.fade-in {
    opacity: 0;
    transform: translateY(30px);
    transition: all 0.6s ease;
}

.fade-in.visible {
    opacity: 1;
    transform: translateY(0);
}
</style>
