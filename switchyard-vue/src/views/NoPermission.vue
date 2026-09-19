<template>
    <div class="no-permission-container">
        <div class="card">
            <h1>403</h1>
            <p>{{ t('noPermission.message') }}</p>
            <div class="actions">
                <ActionButton :label="t('noPermission.home')" :icon="actionIcons.back" @click="goHome" />
                <ActionButton :label="t('noPermission.signIn')" :icon="actionIcons.user" @click="goLogin" />
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import ActionButton from '@/components/ui/ActionButton.vue'
import { actionIcons } from '@/components/ui/actionIcons'
import { useRouter } from 'vue-router'

const { t } = useI18n()
const router = useRouter()

const goHome = () => router.push('/')
const goLogin = () => router.push('/login')
</script>

<style scoped>
.no-permission-container {
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    background: var(--sy-background, #f5f7fa);
}

.card {
    width: 380px;
    text-align: center;
    background: #fff;
    border: 1px solid var(--sy-border, #dfe4ea);
    border-radius: 6px;
    padding: 36px 28px;
}

h1 {
    margin: 0;
    font-size: 56px;
    color: #ef4444;
}

p {
    margin: 12px 0 20px;
    color: #334155;
}

.actions {
    display: flex;
    gap: 10px;
    justify-content: center;
}
</style>
