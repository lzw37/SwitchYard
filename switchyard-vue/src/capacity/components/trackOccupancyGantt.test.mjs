import assert from 'node:assert/strict'
import test from 'node:test'
import { fitTrackOccupancyGantt, trackOccupancyGanttMetrics as metrics } from './trackOccupancyGantt.ts'

test('自适应在整数及小数容器边缘留出余量，不因 offset 尺寸取整产生溢出', () => {
    for (const width of [620.25, 620.75, 1244, 1244.5]) {
        for (const height of [293.25, 293.75, 454, 454.5]) {
            // DOM offset dimensions round the allocated CSS box to whole pixels.
            const scales = fitTrackOccupancyGantt(Math.round(width), Math.round(height), 1440, 11400)
            const right = metrics.sidebarWidth + 1440 * scales.scaleX
            const bottom = metrics.axisHeight + 11400 * scales.scaleY
            assert.ok(right <= width - 0.5, `right edge ${right} exceeds ${width}`)
            assert.ok(bottom <= height - 0.5, `bottom edge ${bottom} exceeds ${height}`)
            assert.ok(width - right < 2 && height - bottom < 2, 'fit should still fill the viewport')
        }
    }
})

test('资源行数量及各行高度改变时仍将全部资源放入可用区域', () => {
    for (const heights of [[38], [66, 38, 38], Array(240).fill(38)]) {
        const rowsHeight = heights.reduce((sum, value) => sum + value, 0)
        const { scaleX, scaleY } = fitTrackOccupancyGantt(900, 380, 1560, rowsHeight)
        assert.ok(scaleX > 0 && scaleY > 0)
        assert.ok(metrics.axisHeight + rowsHeight * scaleY < 380)
        assert.ok(metrics.axisHeight + rowsHeight * scaleY >= 378)
    }
})

test('隐藏或无资源的图表不产生无效缩放', () => {
    for (const dimensions of [[0, 0, 1440, 76], [900, 380, 0, 0], [100, 30, 1440, 76]]) {
        const { scaleX, scaleY } = fitTrackOccupancyGantt(...dimensions)
        assert.ok(Number.isFinite(scaleX) && scaleX > 0)
        assert.ok(Number.isFinite(scaleY) && scaleY > 0)
    }
})
