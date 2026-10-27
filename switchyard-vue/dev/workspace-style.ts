import { createApp, h } from 'vue'
import { createRouter, createMemoryHistory } from 'vue-router'
import ElementPlus, { ElConfigProvider } from 'element-plus'
import en from 'element-plus/es/locale/lang/en'
import zh from 'element-plus/es/locale/lang/zh-cn'
import 'element-plus/dist/index.css'
import '../src/assets/main.css'
import '../src/assets/workspace.css'
import CapacityMain from '../src/capacity/CapacityMain.vue'
import HumpMain from '../src/hump/HumpMain.vue'
import CourseMain from '../src/course/CourseMain.vue'
import { demoCatalog } from '../src/capacity/operationProcess'
import axios from '../src/utils/axios'
import pinia from '../src/stores'
import i18n from '../src/i18n'
import { createStationPreviewData } from '../tests/fixtures/station-preview-data'

// Preview production components with local sample data; never contact a backend.
const parameters = new URLSearchParams(location.search)
i18n.global.locale.value = parameters.get('lang') === 'en' ? 'en' : 'zh'
const fixture = createStationPreviewData()
axios.defaults.adapter = async config => {
    let data: unknown
    if (/\/(Capacity|Hump)\/GetInstances$/.test(config.url || '')) {
        data = [{ id: 'visual-test', name: 'Demo station', owner: 'Preview', isActive: 1 }]
    } else if (config.url === '/OperationProcess/GetCatalog') {
        data = structuredClone(demoCatalog)
    } else {
        try { data = fixture.respond(config) }
        catch { data = [] }
    }
    return { data, status: 200, statusText: 'OK', headers: {}, config }
}
const page = parameters.get('page')
if (page === 'course') window.fetch = async () => new Response('[]', { headers: { 'Content-Type': 'application/json' } })
const component = page === 'hump' ? HumpMain : page === 'course' ? CourseMain : CapacityMain
const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component }] })
createApp({ render: () => h(ElConfigProvider, { locale: i18n.global.locale.value === 'en' ? en : zh }, () => h(component)) })
    .use(pinia).use(i18n).use(ElementPlus).use(router).mount('#app')
