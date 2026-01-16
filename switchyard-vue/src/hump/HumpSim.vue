<template>
    溜放仿真

</template>
<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import type { SlopeLayout } from './humplayoutctrl';
import axios from '@/utils/axios'
import config from '../config.json'
const slopeLayout = ref<SlopeLayout | null>(null);
</script>

<style scoped lang="css"></style>