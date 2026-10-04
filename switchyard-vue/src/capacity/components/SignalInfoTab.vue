<template>
    <div class="demo-collapse">
        <el-collapse>
            <el-collapse-item :title="t('stationLayout.draw.signal')" name="1">
                <el-form label-width="auto" style="margin:10px">
                    <el-form-item label="ID">
                        <el-input v-model="signal.id" />
                    </el-form-item>
                    <el-form-item :label="t('stationLayout.editor.binding.position')">
                        <div style="display: flex;">
                            <el-input disabled v-model="signal.position.x"><template #prepend>X</template></el-input>
                            <el-input disabled v-model="signal.position.y"><template #prepend>Y</template></el-input>
                        </div>
                    </el-form-item>
                    <el-form-item :label="t('stationLayout.equipment.fields.type')">
                        <el-select v-model="signal.type" :placeholder="t('frontend.signalTypePlaceholder')">
                            <el-option :label="t('stationLayout.signal.types.DepartureSignal')" value="departure" />
                            <el-option :label="t('stationLayout.signal.types.HomeSignal')" value="home" />
                            <el-option :label="t('stationLayout.signal.types.ShuntingSignal')" value="shunting" />
                        </el-select>
                    </el-form-item>
                    <el-form-item :label="t('stationLayout.equipment.fields.bindingNodeID')">
                        <el-select v-model="signal.bindingNodeID" filterable :placeholder="t('frontend.nodePlaceholder')" style="width: 240px">
                            <el-option v-for="item in nodeIDOptions" :key="item" :label="item"
                                :value="item" />
                        </el-select>
                    </el-form-item>
                    <el-form-item :label="t('stationLayout.equipment.fields.direction')">
                        <el-radio-group v-model="signal.direction">
                            <el-radio-button value="w">↖</el-radio-button>
                            <el-radio-button value="e">↗</el-radio-button>
                            <el-radio-button value="s">↙</el-radio-button>
                            <el-radio-button value="d">↘</el-radio-button>
                        </el-radio-group>
                    </el-form-item>
                </el-form>
            </el-collapse-item>
        </el-collapse>
    </div>
</template>

<script lang="ts" setup>
import { reactive } from 'vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const signal = reactive({
    id: "243",
    position: { "x": 1220, "y": 360 },
    type: "departure",
    bindingNodeID: "181",
    direction: "e"
})

const nodeIDOptions = [
    "1","2","de"
]
</script>

<style scoped></style>
