<template>
    <div class="no-permission-container">
        <div class="card">
            <h1>403</h1>
            <p>无权限访问该页面</p>
            <div class="actions">
                <el-button type="primary" @click="goHome">返回首页</el-button>
                <el-button @click="goLogin">去登录</el-button>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'

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
    background: linear-gradient(135deg, #f8fafc 0%, #e2e8f0 100%);
}

.card {
    width: 380px;
    text-align: center;
    background: #fff;
    border-radius: 12px;
    box-shadow: 0 10px 30px rgba(15, 23, 42, 0.12);
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
