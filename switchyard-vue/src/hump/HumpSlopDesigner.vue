<template>
    <div class="side-menu-top"></div>
    <div class="main-ctrl">
        <HumpSlopCtrl />
        <HumpSlopSketchBlock />
        <!-- <HumpLayoutCtrl /> -->
    </div>
    <div class="side-menu-left"></div>
    <div class="side-menu-right"></div>
    <div class="side-menu-bottom"></div>
</template>
<script setup lang="ts">
import HumpSlopCtrl from './HumpSlopCtrl.vue';
import HumpSlopSketchBlock from './HumpSlopSketchBlock.vue';
import HumpLayoutCtrl from './HumpLayoutCtrl.vue';

</script>
<style scoped lang="css"></style>