import { ref as k, computed as A, onMounted as Ws, watch as ma, onBeforeUnmount as Gs, openBlock as w, createElementBlock as N, withModifiers as Qt, normalizeStyle as Fe, createElementVNode as v, Fragment as P, renderList as H, normalizeClass as Oe, withDirectives as Dl, toDisplayString as _, vShow as ya, createCommentVNode as ie, createBlock as ce, resolveDynamicComponent as eo, mergeProps as fi, nextTick as Un, defineComponent as Us, useModel as gu, resolveComponent as ke, renderSlot as di, createVNode as c, unref as J, withCtx as x, createTextVNode as V, mergeModels as pu, resolveDirective as hu } from "vue";
import { ElMessage as X, ElMessageBox as kl } from "element-plus";
import { Fold as xu, Expand as Su, Download as to, Upload as Vs, Aim as $s, Hide as bu, Connection as oa, Scissor as wu, Share as zs, SetUp as aa, Guide as Fs, Minus as Iu, Location as ku, Bell as Nu, ArrowDown as sa, Switch as Du, Filter as Lu, Stopwatch as Cu, Platform as Mu, EditPen as As, CircleClose as _u, Delete as Vu, Magnet as Es, Pointer as $u, RefreshLeft as zu, RefreshRight as Fu, Grid as Au } from "@element-plus/icons-vue";
const Ll = "right", en = "normal", ra = [
  { label: "向左", value: "left" },
  { label: "向右", value: "right" }
], ua = [
  { label: "普通", value: "normal" },
  { label: "延伸式", value: "ext" }
], Eu = {
  l: "left",
  left: "left",
  左: "left",
  向左: "left",
  r: "right",
  right: "right",
  右: "right",
  向右: "right"
}, Bu = {
  n: "normal",
  normal: "normal",
  普通: "normal",
  ext: "ext",
  e: "ext",
  extend: "ext",
  extended: "ext",
  extension: "ext",
  延伸: "ext",
  延申: "ext"
}, va = {
  normal: {
    className: "bufferstop-normal",
    width: 26.7659,
    height: 11.22,
    elements: [
      { tag: "line", attrs: { y1: 5.7064, x2: 16.9215, y2: 5.7064 } },
      { tag: "line", attrs: { x1: 16.9215, y1: 10.72, x2: 16.9215, y2: 0.5 } },
      { tag: "line", attrs: { x1: 16.9215, y1: 0.5, x2: 26.7659, y2: 0.5 } },
      { tag: "line", attrs: { x1: 16.9215, y1: 10.72, x2: 26.7659, y2: 10.72 } }
    ]
  },
  ext: {
    className: "bufferstop-ext",
    width: 40.7659,
    height: 11.22,
    elements: [
      { tag: "line", attrs: { y1: 5.61, x2: 4.617, y2: 5.61 } },
      { tag: "line", attrs: { x1: 30.9215, y1: 10.72, x2: 30.9215, y2: 0.5 } },
      { tag: "line", attrs: { x1: 30.9215, y1: 0.5, x2: 40.7659, y2: 0.5 } },
      { tag: "line", attrs: { x1: 30.9215, y1: 10.72, x2: 40.7659, y2: 10.72 } },
      { tag: "line", attrs: { x1: 4.617, y1: 5.61, x2: 8.4607, y2: 0.4123 } },
      { tag: "line", attrs: { x1: 8.4607, y1: 0.4123, x2: 10.3008, y2: 10.6111 } },
      { tag: "line", attrs: { x1: 10.3008, y1: 10.6111, x2: 13.8158, y2: 5.4508 } },
      { tag: "line", attrs: { x1: 13.8158, y1: 5.4508, x2: 30.9215, y2: 5.4508 } }
    ]
  }
};
function no($) {
  const g = String($ ?? "").trim();
  return g ? Eu[g.toLowerCase()] || g : Ll;
}
function so($) {
  const g = String($ ?? "").trim();
  if (!g) return en;
  const F = Bu[g.toLowerCase()] || g;
  return va[F] ? F : en;
}
function Ru($) {
  const g = so($);
  return va[g] || va[en];
}
const Tu = "data:image/svg+xml,%3c?xml%20version='1.0'%20encoding='utf-8'?%3e%3c!--%20Generator:%20Adobe%20Illustrator%2025.1.0,%20SVG%20Export%20Plug-In%20.%20SVG%20Version:%206.00%20Build%200)%20--%3e%3csvg%20version='1.1'%20id='图层_1'%20xmlns='http://www.w3.org/2000/svg'%20xmlns:xlink='http://www.w3.org/1999/xlink'%20x='0px'%20y='0px'%20viewBox='0%200%2025.8%2094.8'%20style='enable-background:new%200%200%2025.8%2094.8;'%20xml:space='preserve'%3e%3cstyle%20type='text/css'%3e%20.st0{fill:none;stroke:%23231815;stroke-miterlimit:10;}%20.st1{fill:%236FB645;stroke:%23231815;stroke-miterlimit:10;}%20.st2{fill:%23DE2724;stroke:%23231815;stroke-miterlimit:10;}%20.st3{fill:%23FFFFFF;stroke:%23231815;stroke-miterlimit:10;}%20%3c/style%3e%3cline%20class='st0'%20x1='0'%20y1='94.3'%20x2='25.8'%20y2='94.3'/%3e%3ccircle%20class='st1'%20cx='12.6'%20cy='13.1'%20r='12.6'/%3e%3ccircle%20class='st2'%20cx='12.6'%20cy='38.4'%20r='12.6'/%3e%3cline%20class='st0'%20x1='12.6'%20y1='76.3'%20x2='12.6'%20y2='94.3'/%3e%3ccircle%20class='st3'%20cx='12.6'%20cy='64.3'%20r='12.6'/%3e%3ccircle%20class='st3'%20cx='12.6'%20cy='64.3'%20r='6.5'/%3e%3c/svg%3e", Xu = "data:image/svg+xml,%3csvg%20id='图层_1'%20data-name='图层%201'%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2026.2621%2076.7863'%3e%3cdefs%3e%3cstyle%3e.cls-1{fill:%23231815;}.cls-1,.cls-2,.cls-3,.cls-4{stroke:%23231815;stroke-miterlimit:10;}.cls-2{fill:%23fff;}.cls-3{fill:%23de2724;}.cls-4{fill:%236fb645;}%3c/style%3e%3c/defs%3e%3cline%20class='cls-1'%20x1='0.1056'%20y1='76.2863'%20x2='26.1565'%20y2='76.2863'/%3e%3ccircle%20class='cls-2'%20cx='13.131'%20cy='63.6552'%20r='12.631'/%3e%3ccircle%20class='cls-3'%20cx='13.131'%20cy='38.3931'%20r='12.631'/%3e%3ccircle%20class='cls-4'%20cx='13.131'%20cy='13.131'%20r='12.631'/%3e%3ccircle%20class='cls-2'%20cx='13.131'%20cy='63.6552'%20r='6.4698'/%3e%3c/svg%3e", Yu = "data:image/svg+xml,%3c?xml%20version='1.0'%20encoding='utf-8'?%3e%3c!--%20Generator:%20Adobe%20Illustrator%2025.1.0,%20SVG%20Export%20Plug-In%20.%20SVG%20Version:%206.00%20Build%200)%20--%3e%3csvg%20version='1.1'%20id='图层_1'%20xmlns='http://www.w3.org/2000/svg'%20xmlns:xlink='http://www.w3.org/1999/xlink'%20x='0px'%20y='0px'%20viewBox='0%200%2050.9%2076.8'%20style='enable-background:new%200%200%2050.9%2076.8;'%20xml:space='preserve'%3e%3cstyle%20type='text/css'%3e%20.st0{fill:none;stroke:%23231815;stroke-miterlimit:10;}%20.st1{fill:%23FFFFFF;stroke:%23231815;stroke-miterlimit:10;}%20.st2{fill:%23DE2724;stroke:%23231815;stroke-miterlimit:10;}%20.st3{fill:%236FB645;stroke:%23231815;stroke-miterlimit:10;}%20.st4{fill:%23F4E828;stroke:%23231815;stroke-miterlimit:10;}%20%3c/style%3e%3cline%20class='st0'%20x1='0.4'%20y1='76.3'%20x2='50.9'%20y2='76.3'/%3e%3ccircle%20class='st1'%20cx='13'%20cy='38.4'%20r='12.6'/%3e%3ccircle%20class='st1'%20cx='38.3'%20cy='38.4'%20r='12.6'/%3e%3ccircle%20class='st2'%20cx='38.3'%20cy='63.7'%20r='12.6'/%3e%3ccircle%20class='st3'%20cx='13'%20cy='13.1'%20r='12.6'/%3e%3ccircle%20class='st4'%20cx='13'%20cy='63.7'%20r='12.6'/%3e%3cline%20class='st0'%20x1='4.1'%20y1='29.9'%20x2='22'%20y2='47.1'/%3e%3cline%20class='st0'%20x1='21.6'%20y1='29.2'%20x2='4.3'%20y2='47.6'/%3e%3ccircle%20class='st1'%20cx='38.3'%20cy='38.4'%20r='6.5'/%3e%3c/svg%3e", Pu = "data:image/svg+xml,%3csvg%20id='图层_1'%20data-name='图层%201'%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2027.1579%20145.3105'%3e%3cdefs%3e%3cstyle%3e.cls-1{fill:%23231815;}.cls-1,.cls-2,.cls-3,.cls-4,.cls-5,.cls-6{stroke:%23231815;stroke-miterlimit:10;}.cls-2{fill:%23fff;}.cls-3{fill:%23f4e828;}.cls-4{fill:%23de2724;}.cls-5{fill:%236fb645;}.cls-6{fill:none;}%3c/style%3e%3c/defs%3e%3cline%20class='cls-1'%20style='stroke:%23fff'%20x1='0.5'%20y1='144.8105'%20x2='26.8139'%20y2='144.8105'/%3e%3ccircle%20class='cls-2'%20cx='14.0269'%20cy='114.1794'%20r='12.631'/%3e%3ccircle%20class='cls-3'%20cx='13.6569'%20cy='88.9173'%20r='12.631'/%3e%3ccircle%20class='cls-4'%20cx='14.0269'%20cy='63.6552'%20r='12.631'/%3e%3ccircle%20class='cls-5'%20cx='13.6569'%20cy='38.3931'%20r='12.631'/%3e%3ccircle%20class='cls-3'%20cx='13.131'%20cy='13.131'%20r='12.631'/%3e%3cline%20class='cls-6'%20style='stroke:%23fff'%20x1='14.0269'%20y1='126.8105'%20x2='14.0269'%20y2='144.8105'/%3e%3ccircle%20class='cls-2'%20cx='14.1828'%20cy='114.1794'%20r='6.4698'/%3e%3c/svg%3e", Ou = "data:image/svg+xml,%3csvg%20id='图层_1'%20data-name='图层%201'%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2026.3139%20127.3105'%3e%3cdefs%3e%3cstyle%3e.cls-1{fill:%23231815;}.cls-1,.cls-2,.cls-3,.cls-4,.cls-5{stroke:%23231815;stroke-miterlimit:10;}.cls-2{fill:%23fff;}.cls-3{fill:%23f4e828;}.cls-4{fill:%23de2724;}.cls-5{fill:%236fb645;}%3c/style%3e%3c/defs%3e%3cline%20class='cls-1'%20style='stroke:%23fff'%20y1='126.8105'%20x2='26.3139'%20y2='126.8105'/%3e%3ccircle%20class='cls-2'%20cx='13.1569'%20cy='114.1794'%20r='12.631'/%3e%3ccircle%20class='cls-3'%20cx='13.1569'%20cy='88.9173'%20r='12.631'/%3e%3ccircle%20class='cls-4'%20cx='13.1569'%20cy='63.6552'%20r='12.631'/%3e%3ccircle%20class='cls-5'%20cx='13.1569'%20cy='38.3931'%20r='12.631'/%3e%3ccircle%20class='cls-3'%20cx='13.1569'%20cy='13.131'%20r='12.631'/%3e%3ccircle%20class='cls-2'%20cx='13.1569'%20cy='114.1794'%20r='6.4698'/%3e%3c/svg%3e", Wu = "data:image/svg+xml,%3csvg%20id='图层_1'%20data-name='图层%201'%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2055.4246%20102.0484'%3e%3cdefs%3e%3cstyle%3e.cls-1{fill:%23231815;}.cls-1,.cls-2,.cls-3,.cls-4,.cls-5,.cls-6{stroke:%23231815;stroke-miterlimit:10;}.cls-2{fill:%23fff;}.cls-3{fill:%23f4e828;}.cls-4{fill:%23de2724;}.cls-5{fill:%236fb645;}.cls-6{fill:none;}%3c/style%3e%3c/defs%3e%3cline%20class='cls-1'%20style='stroke:%23fff'%20x1='0.5'%20y1='101.5484'%20x2='55.4246'%20y2='101.5484'/%3e%3ccircle%20class='cls-2'%20cx='13.131'%20cy='63.6552'%20r='12.631'/%3e%3ccircle%20class='cls-2'%20cx='38.3931'%20cy='38.3931'%20r='12.631'/%3e%3ccircle%20class='cls-2'%20cx='38.3931'%20cy='63.6552'%20r='12.631'/%3e%3ccircle%20class='cls-3'%20cx='13.131'%20cy='38.3931'%20r='12.631'/%3e%3ccircle%20class='cls-3'%20cx='13.131'%20cy='88.9173'%20r='12.631'/%3e%3ccircle%20class='cls-4'%20cx='38.3931'%20cy='13.131'%20r='12.631'/%3e%3ccircle%20class='cls-5'%20cx='13.131'%20cy='13.131'%20r='12.631'/%3e%3cline%20class='cls-6'%20style='stroke:%23fff'%20x1='38.3931'%20y1='76.2863'%20x2='38.3931'%20y2='101.5484'/%3e%3cline%20class='cls-6'%20style='stroke:%23fff'%20x1='4.3461'%20y1='55.1979'%20x2='22.223'%20y2='72.4226'/%3e%3cline%20class='cls-6'%20style='stroke:%23fff'%20x1='21.8417'%20y1='54.5231'%20x2='4.4989'%20y2='72.8764'/%3e%3cline%20class='cls-6'%20style='stroke:%23fff'%20x1='29.4547'%20y1='29.8913'%20x2='47.3316'%20y2='47.116'/%3e%3cline%20class='cls-6'%20style='stroke:%23fff'%20x1='46.9503'%20y1='29.2165'%20x2='29.6074'%20y2='47.5698'/%3e%3ccircle%20class='cls-2'%20cx='38.3931'%20cy='63.8102'%20r='6.4698'/%3e%3c/svg%3e", Gu = "data:image/svg+xml,%3c?xml%20version='1.0'%20encoding='utf-8'?%3e%3c!--%20Generator:%20Adobe%20Illustrator%2025.1.0,%20SVG%20Export%20Plug-In%20.%20SVG%20Version:%206.00%20Build%200)%20--%3e%3csvg%20version='1.1'%20id='图层_1'%20xmlns='http://www.w3.org/2000/svg'%20xmlns:xlink='http://www.w3.org/1999/xlink'%20x='0px'%20y='0px'%20viewBox='0%200%2026.6%2051.5'%20style='enable-background:new%200%200%2026.6%2051.5;'%20xml:space='preserve'%3e%3cstyle%20type='text/css'%3e%20.st0{fill:none;stroke:%23231815;stroke-miterlimit:10;}%20.st1{fill:%23FFFFFF;stroke:%23231815;stroke-miterlimit:10;}%20.st2{fill:%2316499D;}%20%3c/style%3e%3cline%20class='st0'%20x1='0.3'%20y1='51'%20x2='26'%20y2='51'/%3e%3ccircle%20class='st1'%20cx='13.4'%20cy='13.1'%20r='12.6'/%3e%3ccircle%20class='st1'%20cx='13.4'%20cy='13.1'%20r='6.5'/%3e%3ccircle%20class='st1'%20cx='13.4'%20cy='38.4'%20r='12.6'/%3e%3ccircle%20class='st2'%20cx='13.4'%20cy='38.4'%20r='6.5'/%3e%3cline%20class='st0'%20x1='4'%20y1='29.8'%20x2='0.7'%20y2='27.2'/%3e%3cline%20class='st0'%20x1='22.8'%20y1='30'%20x2='26'%20y2='27.1'/%3e%3cline%20class='st0'%20x1='4.4'%20y1='47.4'%20x2='1.8'%20y2='50'/%3e%3cline%20class='st0'%20x1='22.7'%20y1='46.9'%20x2='26'%20y2='50'/%3e%3c/svg%3e", Uu = `<?xml version="1.0" encoding="utf-8"?>\r
<!-- Generator: Adobe Illustrator 25.1.0, SVG Export Plug-In . SVG Version: 6.00 Build 0)  -->\r
<svg version="1.1" id="图层_1" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" x="0px" y="0px"\r
	 viewBox="0 0 25.8 94.8" style="enable-background:new 0 0 25.8 94.8;" xml:space="preserve">\r
<style type="text/css">\r
	.st0{fill:none;stroke:#231815;stroke-miterlimit:10;}\r
	.st1{fill:#6FB645;stroke:#231815;stroke-miterlimit:10;}\r
	.st2{fill:#DE2724;stroke:#231815;stroke-miterlimit:10;}\r
	.st3{fill:#FFFFFF;stroke:#231815;stroke-miterlimit:10;}\r
</style>\r
<line class="st0" x1="0" y1="94.3" x2="25.8" y2="94.3"/>\r
<circle class="st1" cx="12.6" cy="13.1" r="12.6"/>\r
<circle class="st2" cx="12.6" cy="38.4" r="12.6"/>\r
<line class="st0" x1="12.6" y1="76.3" x2="12.6" y2="94.3"/>\r
<circle class="st3" cx="12.6" cy="64.3" r="12.6"/>\r
<circle class="st3" cx="12.6" cy="64.3" r="6.5"/>\r
</svg>\r
`, Ju = '<svg id="图层_1" data-name="图层 1" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 26.2621 76.7863"><defs><style>.cls-1{fill:#231815;}.cls-1,.cls-2,.cls-3,.cls-4{stroke:#231815;stroke-miterlimit:10;}.cls-2{fill:#fff;}.cls-3{fill:#de2724;}.cls-4{fill:#6fb645;}</style></defs><line class="cls-1" x1="0.1056" y1="76.2863" x2="26.1565" y2="76.2863"/><circle class="cls-2" cx="13.131" cy="63.6552" r="12.631"/><circle class="cls-3" cx="13.131" cy="38.3931" r="12.631"/><circle class="cls-4" cx="13.131" cy="13.131" r="12.631"/><circle class="cls-2" cx="13.131" cy="63.6552" r="6.4698"/></svg>', qu = `<?xml version="1.0" encoding="utf-8"?>\r
<!-- Generator: Adobe Illustrator 25.1.0, SVG Export Plug-In . SVG Version: 6.00 Build 0)  -->\r
<svg version="1.1" id="图层_1" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" x="0px" y="0px"\r
	 viewBox="0 0 50.9 76.8" style="enable-background:new 0 0 50.9 76.8;" xml:space="preserve">\r
<style type="text/css">\r
	.st0{fill:none;stroke:#231815;stroke-miterlimit:10;}\r
	.st1{fill:#FFFFFF;stroke:#231815;stroke-miterlimit:10;}\r
	.st2{fill:#DE2724;stroke:#231815;stroke-miterlimit:10;}\r
	.st3{fill:#6FB645;stroke:#231815;stroke-miterlimit:10;}\r
	.st4{fill:#F4E828;stroke:#231815;stroke-miterlimit:10;}\r
</style>\r
<line class="st0" x1="0.4" y1="76.3" x2="50.9" y2="76.3"/>\r
<circle class="st1" cx="13" cy="38.4" r="12.6"/>\r
<circle class="st1" cx="38.3" cy="38.4" r="12.6"/>\r
<circle class="st2" cx="38.3" cy="63.7" r="12.6"/>\r
<circle class="st3" cx="13" cy="13.1" r="12.6"/>\r
<circle class="st4" cx="13" cy="63.7" r="12.6"/>\r
<line class="st0" x1="4.1" y1="29.9" x2="22" y2="47.1"/>\r
<line class="st0" x1="21.6" y1="29.2" x2="4.3" y2="47.6"/>\r
<circle class="st1" cx="38.3" cy="38.4" r="6.5"/>\r
</svg>\r
`, ju = `<svg id="图层_1" data-name="图层 1" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 27.1579 145.3105"><defs><style>.cls-1{fill:#231815;}.cls-1,.cls-2,.cls-3,.cls-4,.cls-5,.cls-6{stroke:#231815;stroke-miterlimit:10;}.cls-2{fill:#fff;}.cls-3{fill:#f4e828;}.cls-4{fill:#de2724;}.cls-5{fill:#6fb645;}.cls-6{fill:none;}</style></defs><line class="cls-1" style="stroke:#fff" x1="0.5" y1="144.8105" x2="26.8139" y2="144.8105"/><circle class="cls-2" cx="14.0269" cy="114.1794" r="12.631"/><circle class="cls-3" cx="13.6569" cy="88.9173" r="12.631"/><circle class="cls-4" cx="14.0269" cy="63.6552" r="12.631"/><circle class="cls-5" cx="13.6569" cy="38.3931" r="12.631"/><circle class="cls-3" cx="13.131" cy="13.131" r="12.631"/><line class="cls-6" style="stroke:#fff" x1="14.0269" y1="126.8105" x2="14.0269" y2="144.8105"/><circle class="cls-2" cx="14.1828" cy="114.1794" r="6.4698"/></svg>\r
`, Hu = `<svg id="图层_1" data-name="图层 1" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 26.3139 127.3105"><defs><style>.cls-1{fill:#231815;}.cls-1,.cls-2,.cls-3,.cls-4,.cls-5{stroke:#231815;stroke-miterlimit:10;}.cls-2{fill:#fff;}.cls-3{fill:#f4e828;}.cls-4{fill:#de2724;}.cls-5{fill:#6fb645;}</style></defs><line class="cls-1" style="stroke:#fff" y1="126.8105" x2="26.3139" y2="126.8105"/><circle class="cls-2" cx="13.1569" cy="114.1794" r="12.631"/><circle class="cls-3" cx="13.1569" cy="88.9173" r="12.631"/><circle class="cls-4" cx="13.1569" cy="63.6552" r="12.631"/><circle class="cls-5" cx="13.1569" cy="38.3931" r="12.631"/><circle class="cls-3" cx="13.1569" cy="13.131" r="12.631"/><circle class="cls-2" cx="13.1569" cy="114.1794" r="6.4698"/></svg>\r
`, Ku = `<svg id="图层_1" data-name="图层 1" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 55.4246 102.0484"><defs><style>.cls-1{fill:#231815;}.cls-1,.cls-2,.cls-3,.cls-4,.cls-5,.cls-6{stroke:#231815;stroke-miterlimit:10;}.cls-2{fill:#fff;}.cls-3{fill:#f4e828;}.cls-4{fill:#de2724;}.cls-5{fill:#6fb645;}.cls-6{fill:none;}</style></defs><line class="cls-1" style="stroke:#fff" x1="0.5" y1="101.5484" x2="55.4246" y2="101.5484"/><circle class="cls-2" cx="13.131" cy="63.6552" r="12.631"/><circle class="cls-2" cx="38.3931" cy="38.3931" r="12.631"/><circle class="cls-2" cx="38.3931" cy="63.6552" r="12.631"/><circle class="cls-3" cx="13.131" cy="38.3931" r="12.631"/><circle class="cls-3" cx="13.131" cy="88.9173" r="12.631"/><circle class="cls-4" cx="38.3931" cy="13.131" r="12.631"/><circle class="cls-5" cx="13.131" cy="13.131" r="12.631"/><line class="cls-6" style="stroke:#fff" x1="38.3931" y1="76.2863" x2="38.3931" y2="101.5484"/><line class="cls-6" style="stroke:#fff" x1="4.3461" y1="55.1979" x2="22.223" y2="72.4226"/><line class="cls-6" style="stroke:#fff" x1="21.8417" y1="54.5231" x2="4.4989" y2="72.8764"/><line class="cls-6" style="stroke:#fff" x1="29.4547" y1="29.8913" x2="47.3316" y2="47.116"/><line class="cls-6" style="stroke:#fff" x1="46.9503" y1="29.2165" x2="29.6074" y2="47.5698"/><circle class="cls-2" cx="38.3931" cy="63.8102" r="6.4698"/></svg>\r
`, Zu = `<?xml version="1.0" encoding="utf-8"?>\r
<!-- Generator: Adobe Illustrator 25.1.0, SVG Export Plug-In . SVG Version: 6.00 Build 0)  -->\r
<svg version="1.1" id="图层_1" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" x="0px" y="0px"\r
	 viewBox="0 0 26.6 51.5" style="enable-background:new 0 0 26.6 51.5;" xml:space="preserve">\r
<style type="text/css">\r
	.st0{fill:none;stroke:#231815;stroke-miterlimit:10;}\r
	.st1{fill:#FFFFFF;stroke:#231815;stroke-miterlimit:10;}\r
	.st2{fill:#16499D;}\r
</style>\r
<line class="st0" x1="0.3" y1="51" x2="26" y2="51"/>\r
<circle class="st1" cx="13.4" cy="13.1" r="12.6"/>\r
<circle class="st1" cx="13.4" cy="13.1" r="6.5"/>\r
<circle class="st1" cx="13.4" cy="38.4" r="12.6"/>\r
<circle class="st2" cx="13.4" cy="38.4" r="6.5"/>\r
<line class="st0" x1="4" y1="29.8" x2="0.7" y2="27.2"/>\r
<line class="st0" x1="22.8" y1="30" x2="26" y2="27.1"/>\r
<line class="st0" x1="4.4" y1="47.4" x2="1.8" y2="50"/>\r
<line class="st0" x1="22.7" y1="46.9" x2="26" y2="50"/>\r
</svg>\r
`, Qu = /* @__PURE__ */ Object.assign({
  "./signal_svg/departuresignal-3-aspect-high.svg": Tu,
  "./signal_svg/departuresignal-3-aspect-low.svg": Xu,
  "./signal_svg/departuresignal-5-aspect-low.svg": Yu,
  "./signal_svg/homesignal-5-aspect-high.svg": Pu,
  "./signal_svg/homesignal-5-aspect-low.svg": Ou,
  "./signal_svg/homesignal-7-aspect-low.svg": Wu,
  "./signal_svg/shuntingsignal.svg": Gu
}), ec = /* @__PURE__ */ Object.assign({
  "./signal_svg/departuresignal-3-aspect-high.svg": Uu,
  "./signal_svg/departuresignal-3-aspect-low.svg": Ju,
  "./signal_svg/departuresignal-5-aspect-low.svg": qu,
  "./signal_svg/homesignal-5-aspect-high.svg": ju,
  "./signal_svg/homesignal-5-aspect-low.svg": Hu,
  "./signal_svg/homesignal-7-aspect-low.svg": Ku,
  "./signal_svg/shuntingsignal.svg": Zu
}), mi = "DepartureSignal", tc = {
  departuresignal: "出站信号机",
  homesignal: "进站信号机",
  shuntingsignal: "调车信号机",
  humpsignal: "驼峰信号机"
}, nc = {
  departuresignal: "DepartureSignal",
  homesignal: "HomeSignal",
  shuntingsignal: "ShuntingSignal",
  humpsignal: "HumpSignal"
}, ca = ["DepartureSignal", "HomeSignal", "ShuntingSignal", "HumpSignal"], lc = /* @__PURE__ */ new Set(["ShuntingSignal", "HumpSignal"]), Bs = {
  high: "高柱",
  low: "矮柱"
}, Rs = {
  high: "High",
  low: "Low"
}, lo = {
  DepartureSignal: {
    className: "signal-departure",
    width: 119,
    height: 34,
    bounds: { minX: 1, minY: 1, maxX: 119, maxY: 33 },
    elements: [
      { tag: "circle", attrs: { cx: 38, cy: 17, r: 16, fill: "#fff" } },
      { tag: "circle", attrs: { cx: 38, cy: 17, r: 8, fill: "#fff" } },
      { tag: "circle", attrs: { cx: 70, cy: 17, r: 16, fill: "#009a3e" } },
      { tag: "circle", attrs: { cx: 103, cy: 17, r: 16, fill: "#e60012" } },
      { tag: "line", attrs: { x1: 22, y1: 17, x2: 1, y2: 17, fill: "none" } },
      { tag: "line", attrs: { x1: 1, y1: 1, x2: 1, y2: 33, fill: "none" } }
    ]
  },
  HomeSignal: {
    className: "signal-home",
    width: 104,
    height: 42,
    bounds: { minX: 1, minY: 1, maxX: 104, maxY: 41 },
    elements: [
      { tag: "line", attrs: { x1: 1, y1: 1, x2: 1, y2: 41, fill: "none" } },
      { tag: "line", attrs: { x1: 1, y1: 21, x2: 20, y2: 21, fill: "none" } },
      { tag: "rect", attrs: { x: 20, y: 5, width: 84, height: 32, rx: 4, fill: "#111827" } },
      { tag: "circle", attrs: { cx: 38, cy: 21, r: 10, fill: "#fff" } },
      { tag: "circle", attrs: { cx: 64, cy: 21, r: 10, fill: "#009a3e" } },
      { tag: "circle", attrs: { cx: 90, cy: 21, r: 10, fill: "#e60012" } }
    ]
  },
  ShuntingSignal: {
    className: "signal-shunting",
    width: 68,
    height: 34,
    bounds: { minX: 1, minY: 0, maxX: 68, maxY: 33 },
    elements: [
      { tag: "line", attrs: { x1: 1, y1: 12, x2: 20, y2: 12, fill: "none" } },
      { tag: "line", attrs: { x1: 1, y1: 1, x2: 1, y2: 33, fill: "none" } },
      { tag: "polygon", attrs: { points: "20,12 44,0 68,12 44,24", fill: "#1f2937" } },
      { tag: "circle", attrs: { cx: 36, cy: 12, r: 6, fill: "#fff" } },
      { tag: "circle", attrs: { cx: 52, cy: 12, r: 6, fill: "#60a5fa" } }
    ]
  },
  HumpSignal: {
    className: "signal-hump",
    width: 68,
    height: 39,
    bounds: { minX: 1, minY: 1, maxX: 68, maxY: 39 },
    elements: [
      { tag: "line", attrs: { x1: 1, y1: 1, x2: 1, y2: 39, fill: "none" } },
      { tag: "line", attrs: { x1: 1, y1: 20, x2: 20, y2: 20, fill: "none" } },
      { tag: "path", attrs: { d: "M20 36 L44 4 L68 36 Z", fill: "#facc15" } },
      { tag: "circle", attrs: { cx: 44, cy: 23, r: 8, fill: "#e60012" } },
      { tag: "line", attrs: { x1: 28, y1: 36, x2: 60, y2: 36, fill: "none" } }
    ]
  }
};
function ga($) {
  return String($ ?? "").trim().replace(/[\s_-]+/g, "").toLowerCase();
}
function ic($) {
  return String($ ?? "").split(/[\s_-]+/g).filter(Boolean).map((g) => g.charAt(0).toUpperCase() + g.slice(1).toLowerCase()).join("");
}
function oc($) {
  return String($).split("/").pop()?.replace(/\.svg$/i, "") || "";
}
function ac($) {
  const g = String($ || "").match(/viewBox=["']\s*([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s*["']/i);
  if (g)
    return {
      width: Math.max(1, Number(g[3]) || 1),
      height: Math.max(1, Number(g[4]) || 1)
    };
  const F = String($ || "").match(/\bwidth=["']([-\d.]+)/i), y = String($ || "").match(/\bheight=["']([-\d.]+)/i);
  return {
    width: Math.max(1, Number(F?.[1]) || 48),
    height: Math.max(1, Number(y?.[1]) || 48)
  };
}
function sc($) {
  const g = $.toLowerCase().split(/[\s_-]+/g).filter(Boolean), F = g[0] || "", y = nc[F] || ic(F), q = tc[F] || y, Z = [q], j = [y], D = g.findIndex((it, pt) => /^\d+$/.test(it) && g[pt + 1] === "aspect"), M = D >= 0 ? g[D] : "";
  D >= 0 && (Z.push(`${M}显示`), j.push(`${M}Aspect`));
  const de = g.find((it) => Rs[it]);
  return de && (Z.push(Bs[de]), j.push(Rs[de])), {
    type: j.join(""),
    label: Z.join(" "),
    categoryType: y,
    categoryLabel: q,
    poleKey: de || "",
    poleLabel: de ? Bs[de] : "",
    aspectCount: M,
    aspectLabel: M ? `${M}显示` : ""
  };
}
function Js($) {
  return String($ || "").split(";").map((g) => g.trim()).filter(Boolean).reduce((g, F) => {
    const y = F.indexOf(":");
    if (y <= 0) return g;
    const q = F.slice(0, y).trim(), Z = F.slice(y + 1).trim();
    return q && Z && (g[q] = Z), g;
  }, {});
}
function rc($) {
  const g = {}, F = Array.from(String($ || "").matchAll(/<style[^>]*>([\s\S]*?)<\/style>/gi)).map((Z) => Z[1]).join(`
`), y = /([^{}]+)\{([^{}]*)\}/g;
  let q;
  for (; (q = y.exec(F)) !== null; ) {
    const Z = Js(q[2]);
    for (const j of q[1].split(",")) {
      const D = j.trim().match(/^\.([A-Za-z0-9_-]+)$/);
      if (!D) continue;
      const M = D[1];
      g[M] = {
        ...g[M] || {},
        ...Z
      };
    }
  }
  return g;
}
function uc($, g, F) {
  const y = {}, q = {};
  let Z = "";
  const j = /([:@A-Za-z0-9_.-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g;
  let D;
  for (; (D = j.exec($)) !== null; ) {
    const de = D[1], it = D[2] ?? D[3] ?? "";
    if (de === "class") {
      Z = it;
      continue;
    }
    if (de === "style") {
      Object.assign(q, Js(it));
      continue;
    }
    y[de] = it;
  }
  const M = {};
  for (const de of Z.split(/\s+/g).filter(Boolean))
    Object.assign(M, g[de] || {});
  return {
    ...M,
    ...y,
    ...q,
    transform: y.transform ? `${y.transform} ${F}` : F
  };
}
function cc($, g, F) {
  return {
    tag: "image",
    attrs: {
      href: $,
      x: 0,
      y: 0,
      width: g.width,
      height: g.height,
      transform: F
    }
  };
}
function dc($, g, F, y) {
  const q = rc($), Z = /<(line|circle|ellipse|rect|path|polygon|polyline)\b([^>]*)\/?>/gi, j = [];
  let D;
  for (; (D = Z.exec(String($ || ""))) !== null; )
    j.push({
      tag: D[1].toLowerCase(),
      attrs: uc(D[2], q, y)
    });
  return j.length > 0 ? j : [cc(g, F, y)];
}
function fc() {
  return Object.entries(Qu).map(([$, g]) => {
    const F = oc($);
    if (!F || !g) return null;
    const y = sc(F), { type: q, label: Z } = y, j = ec[$], D = ac(j), M = {
      minX: D.width - D.height,
      minY: D.height,
      maxX: D.width,
      maxY: D.height + D.width,
      width: D.height,
      height: D.width
    }, de = `rotate(-90 ${D.width} ${D.height})`;
    return {
      fileName: F,
      type: q,
      ...y,
      option: { label: Z, value: q },
      asset: {
        className: `signal-${F}`,
        placement: "quadrant",
        width: D.width,
        height: D.height,
        bounds: M,
        elements: dc(j, g, D, de)
      }
    };
  }).filter(Boolean).sort(($, g) => $.type.localeCompare(g.type, "en"));
}
function mc($) {
  const g = [$.poleLabel, $.aspectLabel].filter(Boolean);
  return g.length ? g.join(" ") : "通用";
}
function yc($) {
  const g = $.poleKey === "high" ? 0 : $.poleKey === "low" ? 1 : 2, F = Number($.aspectCount || Number.MAX_SAFE_INTEGER);
  return g * 1e3 + (Number.isFinite(F) ? F : Number.MAX_SAFE_INTEGER);
}
function vc($) {
  const g = /* @__PURE__ */ new Map();
  for (const F of $) {
    const y = F.categoryType || F.type;
    g.has(y) || g.set(y, {
      label: F.categoryLabel || F.label,
      value: y,
      order: ca.includes(y) ? ca.indexOf(y) : ca.length,
      children: []
    }), g.get(y).children.push({
      label: mc(F),
      value: F.type,
      sortValue: yc(F)
    });
  }
  return [...g.values()].sort((F, y) => F.order - y.order || F.label.localeCompare(y.label, "zh-Hans-CN")).map((F) => {
    const y = F.children.sort((q, Z) => q.sortValue - Z.sortValue || q.label.localeCompare(Z.label, "zh-Hans-CN"));
    return lc.has(F.value) ? {
      label: F.label,
      value: y[0]?.value || F.value
    } : {
      label: F.label,
      value: F.value,
      children: y.map(({ sortValue: q, ...Z }) => Z)
    };
  });
}
const Cl = fc(), io = Object.fromEntries(Cl.map(($) => [$.type, $.asset])), pa = Cl.find(($) => $.type.startsWith("DepartureSignal"))?.type || "DepartureSignal", ha = Cl.find(($) => $.type.startsWith("HomeSignal"))?.type || "HomeSignal", xa = Cl.find(($) => $.type.startsWith("ShuntingSignal"))?.type || "ShuntingSignal", Ts = {
  ...lo,
  ...io,
  DepartureSignal: io[pa] || lo.DepartureSignal,
  HomeSignal: io[ha] || lo.HomeSignal,
  ShuntingSignal: io[xa] || lo.ShuntingSignal
}, gc = [
  {
    type: "HumpSignal",
    label: "驼峰信号机 通用",
    categoryType: "HumpSignal",
    categoryLabel: "驼峰信号机",
    poleKey: "",
    poleLabel: "",
    aspectCount: "",
    aspectLabel: "",
    option: { label: "驼峰信号机 通用", value: "HumpSignal" }
  }
], qs = [...Cl, ...gc], Km = qs.map(($) => $.option), pc = vc(qs), hc = Object.fromEntries(
  Cl.flatMap(($) => [
    [ga($.fileName), $.type],
    [ga($.type), $.type]
  ])
), xc = {
  departure: pa,
  departuresignal: pa,
  home: ha,
  homesignal: ha,
  shunting: xa,
  shuntingsignal: xa,
  hump: "HumpSignal",
  humpsignal: "HumpSignal",
  ...hc
};
function Sa($) {
  const g = String($ ?? "").trim();
  return g ? xc[ga(g)] || g : mi;
}
function Sc($) {
  const g = Sa($);
  return Ts[g] || Ts[mi];
}
const ba = ($, g) => {
  const F = $.__vccOpts || $;
  for (const [y, q] of g)
    F[y] = q;
  return F;
}, bc = ["width", "height"], wc = { id: "grid" }, Ic = ["cx", "cy"], kc = { id: "linegroup" }, Nc = ["id", "x1", "y1", "x2", "y2", "onMouseenter", "onMouseleave", "onClick"], Dc = ["x", "y", "onMouseenter", "onMouseleave", "onClick"], Lc = ["id", "d"], Cc = ["d"], Mc = ["x1", "y1", "x2", "y2"], _c = ["cx", "cy", "r"], Vc = ["x1", "y1", "x2", "y2"], $c = ["id", "x", "y", "width", "height", "onMousedown"], zc = ["cx", "cy"], Fc = ["cx", "cy"], Ac = {
  key: 0,
  id: "nodegroup"
}, Ec = ["id", "cx", "cy", "r", "onMouseenter", "onMouseleave", "onMousedown"], Bc = ["cx", "cy", "r"], Rc = {
  key: 1,
  id: "route-highlight-layer",
  class: "route-highlight-layer"
}, Tc = ["x1", "y1", "x2", "y2"], Xc = ["d"], Yc = ["cx", "cy", "r"], Pc = ["points"], Oc = {
  key: 2,
  id: "cell-name-layer",
  class: "cell-name-layer"
}, Wc = ["x", "y", "onMouseenter", "onMouseleave", "onClick"], Gc = { id: "signalgroup" }, Uc = ["id", "transform", "onMouseenter", "onMouseleave", "onMousedown"], Jc = ["x", "y", "text-anchor", "onMouseenter", "onMouseleave", "onMousedown"], qc = ["transform"], jc = { id: "insulationjointgroup" }, Hc = ["id", "transform", "onMouseenter", "onMouseleave", "onMousedown"], Kc = {
  key: 0,
  id: "tempinsulationjoint",
  class: "insulationjoint insulationjoint-temp"
}, Zc = ["x1", "x2", "y1", "y2"], Qc = { id: "bufferstopgroup" }, ed = ["id", "transform", "onMouseenter", "onMouseleave", "onMousedown"], td = ["transform"], nd = ["y", "width", "height"], ld = ["transform"], id = ["transform"], od = { id: "switchgroup" }, ad = ["id", "transform", "onMouseenter", "onMouseleave", "onMousedown"], sd = ["x1", "y1", "x2", "y2"], rd = { id: "platformgroup" }, ud = ["id", "onMouseenter", "onMouseleave", "onMousedown"], cd = ["x", "y", "width", "height"], dd = ["x", "y"], fd = ["x", "y", "width", "height"], md = { id: "annotationgroup" }, yd = ["id", "transform", "onMouseenter", "onMouseleave", "onMousedown"], vd = ["font-family", "font-size", "font-weight", "font-style", "fill"], gd = ["onMousedown"], pd = { id: "cursor" }, hd = ["x", "y", "width", "height"], xd = {
  key: 1,
  class: "drawing-hint signal-direction-hint"
}, Sd = ["x", "y", "width", "height"], bd = ["x1", "x2", "y1", "y2"], wd = ["x1", "x2", "y1", "y2"], Id = ["transform"], kd = ["x", "y"], Nl = 100, Nd = 90, Dd = 175, Ld = 0.98, Cd = 0.62, Md = 4, Xs = 4, _d = "#9ec7e8", Ys = "#e2bf6a", Vd = "#6f9fc5", $d = "#b8924d", zd = "#ffffff", Fd = "#808080", da = 240, oo = 80, ao = 80, Ps = 4, Ad = 5, Ed = {
  __name: "StationLayoutEditor",
  props: {
    width: { type: Number, default: 1920 },
    height: { type: Number, default: 1080 },
    displayScaleX: { type: Number, default: 1 },
    displayScaleY: { type: Number, default: 1 },
    showCurveArc: { type: Boolean, default: !0 },
    showNodes: { type: Boolean, default: !0 },
    showGrid: { type: Boolean, default: !0 },
    gridSpacing: { type: Number, default: 20 },
    objectSnapDistance: { type: Number, default: 10 },
    displayStyles: { type: Object, default: () => ({}) },
    editorState: { type: String, default: "" },
    cellLinkMembershipCounts: { type: Object, default: () => ({}) },
    cells: { type: Array, default: () => [] },
    showCellNames: { type: Boolean, default: !1 },
    routePickTarget: { type: String, default: "" },
    highlightedRouteLinkIds: { type: Array, default: () => [] },
    highlightedRouteNodeIds: { type: Array, default: () => [] },
    highlightedRouteArrowNodeIds: { type: Array, default: () => [] },
    highlightedRouteColor: { type: String, default: "#ffd600" },
    highlightedRouteArrowVisible: { type: Boolean, default: !0 },
    autoGenerateTopology: { type: Boolean, default: !0 },
    readonly: { type: Boolean, default: !1 }
  },
  emits: [
    "selected-annotation-change",
    "selected-equipment-change",
    "route-node-pick",
    "cell-name-click",
    "delete-selection-request",
    "topology-rebuilt"
  ],
  setup($, { expose: g, emit: F }) {
    const y = $, q = F, Z = k(null), j = {
      switchName: { fontSize: 8, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
      platformName: { fontSize: 10, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
      signalName: { fontSize: 8, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
      lineName: { fontSize: 10, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
      track: { strokeWidth: 2, color: "#fefded" },
      curve: { strokeWidth: 4, color: "#ffb347" },
      platform: { strokeWidth: 1, color: "#87ceeb" },
      signal: { scale: 0.5 },
      switch: { strokeWidth: 5, color: "#00ffff" },
      node: { radius: 5, color: "#ffffff" }
    }, D = k(0), M = k("l"), de = A(() => D.value === 1 && !y.readonly), it = k(1), pt = k(1), Ml = A(() => Math.max(0, m(y.objectSnapDistance))), at = k(10), Pt = k(10), be = {
      length: 12,
      gap: 4,
      namePadding: 6,
      halfWidth: 5,
      minLength: 4,
      tailLineSpacing: 4,
      tailCircleRadius: 4,
      tailCircleGap: 1,
      tailGap: 3
    };
    let tn = null;
    const st = { originX: 0, originY: 0 }, ee = k({ size: 10, barVisible: !1, barLength: 100, x: 200, y: 200 }), _l = [
      { key: "w", label: "w", x: -22, y: -18 },
      { key: "e", label: "e", x: 22, y: -18 },
      { key: "s", label: "s", x: -22, y: 22 },
      { key: "d", label: "d", x: 22, y: 22 }
    ], ne = { size: 10 }, tt = A(() => el(y.displayScaleX)), Ne = A(() => el(y.displayScaleY)), Cn = () => ({
      minX: 0,
      minY: 0,
      maxX: Math.max(1, m(y.width)),
      maxY: Math.max(1, m(y.height))
    }), we = k(Cn()), Jn = A(() => Math.max(1, we.value.maxX - we.value.minX)), Mn = A(() => Math.max(1, we.value.maxY - we.value.minY)), Ot = A(() => Jn.value * tt.value), Wt = A(() => Mn.value * Ne.value), _n = A(() => ({
      width: `${Ot.value}px`,
      height: `${Wt.value}px`
    })), Me = A(() => co(y.displayStyles)), Vl = A(() => y.editorState === "cell_editing"), qn = A(() => new Set(
      (Array.isArray(y.highlightedRouteLinkIds) ? y.highlightedRouteLinkIds : []).map((e) => String(e)).filter((e) => e !== "")
    )), nn = A(() => new Set(
      (Array.isArray(y.highlightedRouteNodeIds) ? y.highlightedRouteNodeIds : []).map((e) => String(e)).filter((e) => e !== "")
    )), ln = A(() => {
      const e = (Array.isArray(y.highlightedRouteLinkIds) ? y.highlightedRouteLinkIds : []).map((n) => String(n)).filter((n) => n !== ""), t = /* @__PURE__ */ new Set();
      for (let n = 0; n < e.length - 1; n++)
        t.add(ss(e[n], e[n + 1]));
      return t;
    }), on = A(() => qn.value.size === 0 ? [] : gn.value.filter((e) => qn.value.has(String(e.line?.id ?? "")))), Gt = A(() => ln.value.size === 0 ? [] : Gl.value.filter((e) => Br(e))), jn = A(() => nn.value.size === 0 ? [] : rs(y.highlightedRouteNodeIds)), Hn = A(() => {
      const e = Array.isArray(y.highlightedRouteArrowNodeIds) ? y.highlightedRouteArrowNodeIds : [], t = e.length > 0 ? e : y.highlightedRouteNodeIds;
      return rs(t);
    }), an = A(
      () => on.value.length > 0 || Gt.value.length > 0 || jn.value.length > 0
    ), kt = A(() => String(y.highlightedRouteColor || "#ffd600").trim() || "#ffd600"), $l = A(() => Math.max(
      Me.value.track.strokeWidth + 4,
      Me.value.curve.strokeWidth + 2,
      7
    )), yi = A(() => ({
      stroke: kt.value,
      strokeWidth: $l.value
    })), ro = A(() => ({
      fill: kt.value
    })), uo = A(() => ({
      fill: kt.value,
      stroke: "#111827"
    })), me = A(() => Math.max(Me.value.node.radius + 2, 6)), je = A(() => {
      if (!y.highlightedRouteArrowVisible) return null;
      const e = Hn.value.filter((dt) => dt != null);
      if (e.length < 2) return null;
      const t = e[e.length - 1], n = U(t.x), i = G(t.y);
      let a = null, s = 0;
      for (let dt = e.length - 2; dt >= 0; dt--) {
        const ft = e[dt], wl = n - U(ft.x), Il = i - G(ft.y), Zt = Math.hypot(wl, Il);
        if (!(Zt <= 1e-3)) {
          a = ft, s = Zt;
          break;
        }
      }
      if (!a || s <= 1e-3) return null;
      const d = U(a.x), f = G(a.y), p = (n - d) / s, I = (i - f) / s, b = Math.max(16, $l.value * 2.4), C = Math.max(13, $l.value * 1.8), B = n - p * b, T = i - I * b, ge = -I, L = p, R = B + ge * C / 2, he = T + L * C / 2, Ze = B - ge * C / 2, Gn = T - L * C / 2;
      return {
        points: `${n},${i} ${R},${he} ${Ze},${Gn}`
      };
    }), rt = k(0), z = k({}), Y = k([]), Ae = k([]), O = k([]), xe = k([]), le = k([]), fe = k([]), Be = k([]), te = k([]), Ee = k([]), ye = k(/* @__PURE__ */ new Set()), De = k(/* @__PURE__ */ new Set()), We = k(/* @__PURE__ */ new Set()), Ge = k(/* @__PURE__ */ new Set()), _e = k(/* @__PURE__ */ new Set()), W = k(/* @__PURE__ */ new Set()), se = k(/* @__PURE__ */ new Set()), oe = k(/* @__PURE__ */ new Set()), Le = k(null), mt = k(null), pe = ["link", "signal", "insulationJoint", "bufferStop", "switch", "platform"], sn = k([]), Nt = k(null), Re = k(null), Ue = k({ visible: !1, direction: "w", x: 0, y: 0, type: mi }), yt = k({ visible: !1, x: 0, y: 0 }), Ve = k({ visible: !1, direction: Ll, type: en, x: 0, y: 0 }), ht = k({ visible: !1, x: 0, y: 0 }), Te = k(null), He = k(null), Kn = k(null), Zn = k(null), Qn = k(null), Qe = k([]), Dt = k([]);
    let Ft = 0;
    const rn = A(() => {
      if (!He.value)
        return { visible: !1, x: 0, y: 0, width: 0, height: 0 };
      const e = Yl(He.value);
      return {
        visible: ki(He.value),
        x: U(e.minX),
        y: G(e.minY),
        width: $n(e.maxX - e.minX),
        height: un(e.maxY - e.minY)
      };
    });
    function el(e) {
      const t = Number(e);
      return !Number.isFinite(t) || t <= 0 ? 1 : t;
    }
    function Ut(e, t, n, i) {
      const a = Number(e);
      return Number.isFinite(a) ? Math.min(i, Math.max(n, a)) : t;
    }
    function Vn(e, t) {
      const n = e && typeof e == "object" ? e : {};
      return {
        fontSize: Ut(n.fontSize, t.fontSize, 6, 96),
        fontFamily: String(n.fontFamily || t.fontFamily),
        fontWeight: String(n.fontWeight || t.fontWeight),
        fontStyle: String(n.fontStyle || t.fontStyle),
        color: String(n.color || t.color)
      };
    }
    function co(e) {
      const t = e && typeof e == "object" ? e : {};
      return {
        switchName: Vn(t.switchName, j.switchName),
        platformName: Vn(t.platformName, j.platformName),
        signalName: Vn(t.signalName, j.signalName),
        lineName: Vn(t.lineName, j.lineName),
        track: {
          strokeWidth: Ut(t.track?.strokeWidth, j.track.strokeWidth, 0.5, 24),
          color: String(t.track?.color || j.track.color)
        },
        curve: {
          strokeWidth: Ut(t.curve?.strokeWidth, j.curve.strokeWidth, 0.5, 24),
          color: String(t.curve?.color || j.curve.color)
        },
        platform: {
          strokeWidth: Ut(t.platform?.strokeWidth, j.platform.strokeWidth, 0.5, 24),
          color: String(t.platform?.color || j.platform.color)
        },
        signal: {
          scale: Ut(t.signal?.scale, j.signal.scale, 0.2, 3)
        },
        switch: {
          strokeWidth: Ut(t.switch?.strokeWidth, j.switch.strokeWidth, 1, 32),
          color: String(t.switch?.color || j.switch.color)
        },
        node: {
          radius: Ut(t.node?.radius, j.node.radius, 1, 48),
          color: String(t.node?.color || j.node.color)
        }
      };
    }
    function m(e) {
      const t = Number(e);
      return Number.isFinite(t) ? t : 0;
    }
    function U(e) {
      return (m(e) - we.value.minX) * tt.value;
    }
    function G(e) {
      return (m(e) - we.value.minY) * Ne.value;
    }
    function $n(e) {
      return m(e) * tt.value;
    }
    function un(e) {
      return m(e) * Ne.value;
    }
    function Lt(e, t) {
      return U(m(e) + m(t) / 2);
    }
    function vi(e, t) {
      return G(m(e) + m(t) / 2);
    }
    function gi(e) {
      return m(e) / tt.value + we.value.minX;
    }
    function pi(e) {
      return m(e) / Ne.value + we.value.minY;
    }
    function zl() {
      const e = Cn(), t = dn(), n = zn();
      we.value = {
        minX: cn(e.minX, t, Ct()),
        minY: cn(e.minY, n, At()),
        maxX: K(e.maxX, t, Ct()),
        maxY: K(e.maxY, n, At())
      };
    }
    function cn(e, t, n = 0) {
      const i = Math.max(1, m(t)), a = m(n);
      return Math.floor((m(e) - a) / i) * i + a;
    }
    function K(e, t, n = 0) {
      const i = Math.max(1, m(t)), a = m(n);
      return Math.ceil((m(e) - a) / i) * i + a;
    }
    function dn() {
      return Math.max(1, m(y.gridSpacing) || 1);
    }
    function zn() {
      return Math.max(1, m(y.gridSpacing) || 1);
    }
    function Ct() {
      return m(st.originX);
    }
    function At() {
      return m(st.originY);
    }
    function Et(e, t) {
      const n = Math.max(1, m(t)), i = (m(e) % n + n) % n;
      return Je(i);
    }
    function Fl() {
      st.originX = 0, st.originY = 0;
    }
    function fo() {
      return {
        showGrid: y.showGrid !== !1,
        spacing: dn(),
        originX: Je(Ct()),
        originY: Je(At())
      };
    }
    function mo(e) {
      if (!e || typeof e != "object" || Array.isArray(e)) return !1;
      const t = Number(e.originX ?? e.OriginX ?? 0), n = Number(e.originY ?? e.OriginY ?? 0);
      return st.originX = Number.isFinite(t) ? Je(t) : 0, st.originY = Number.isFinite(n) ? Je(n) : 0, !0;
    }
    function hi() {
      return Z.value?.parentElement || null;
    }
    function yo(e, t) {
      if (e <= 0 && t <= 0) return;
      const n = hi();
      n && Un(() => {
        e > 0 && (n.scrollLeft += e), t > 0 && (n.scrollTop += t);
      });
    }
    function xi(e, t = {}) {
      if (!e) return !1;
      const n = Math.min(m(e.minX), m(e.maxX)), i = Math.min(m(e.minY), m(e.maxY)), a = Math.max(m(e.minX), m(e.maxX)), s = Math.max(m(e.minY), m(e.maxY));
      return [n, i, a, s].every(Number.isFinite) ? (tl({ minX: n, minY: i, maxX: a, maxY: s }, {
        padding: Math.max(0, m(t.padding ?? da))
      }), Un(() => {
        const d = hi();
        if (!d) return;
        const f = Math.max(0, m(t.screenMargin ?? 48)), p = U(n) - f, I = U(a) + f, b = G(i) - f, C = G(s) + f, B = (p + I - d.clientWidth) / 2, T = (b + C - d.clientHeight) / 2;
        d.scrollLeft = Math.max(0, B), d.scrollTop = Math.max(0, T);
      }), !0) : !1;
    }
    function Al(e = {}) {
      const t = Math.max(0, m(e.screenMargin ?? 0)), n = Ol(t);
      return Pl(n) ? null : { ...n };
    }
    function tl(e, t = {}) {
      if (!e) return !1;
      const n = Math.min(m(e.minX), m(e.maxX)), i = Math.min(m(e.minY), m(e.maxY)), a = Math.max(m(e.minX), m(e.maxX)), s = Math.max(m(e.minY), m(e.maxY)), d = Math.max(0, m(t.triggerMargin)), f = Math.max(0, m(t.padding ?? da)), p = we.value, I = dn(), b = zn();
      let C = p.minX, B = p.minY, T = p.maxX, ge = p.maxY;
      if (n < p.minX + d && (C = cn(n - f, I, Ct())), i < p.minY + d && (B = cn(i - f, b, At())), a > p.maxX - d && (T = K(a + f, I, Ct())), s > p.maxY - d && (ge = K(s + f, b, At())), C === p.minX && B === p.minY && T === p.maxX && ge === p.maxY)
        return !1;
      const L = Math.max(0, (p.minX - C) * tt.value), R = Math.max(0, (p.minY - B) * Ne.value);
      return we.value = {
        minX: C,
        minY: B,
        maxX: T,
        maxY: ge
      }, yo(L, R), !0;
    }
    function Fn(e, t = {}) {
      if (!e) return !1;
      const n = m(e.x), i = m(e.y);
      return tl({ minX: n, minY: i, maxX: n, maxY: i }, t);
    }
    function Si(e) {
      return U(m(e.x) + ne.size / 2) - ne.size / 2;
    }
    function vo(e) {
      return G(m(e.y) + ne.size / 2) - ne.size / 2;
    }
    function nt(e) {
      return {
        x: m(e?.x),
        y: m(e?.y)
      };
    }
    function El(e) {
      return {
        id: e?.id ?? ut(),
        text: e?.text ?? "Annotation",
        position: nt(e?.position),
        fontFamily: e?.fontFamily || "Arial",
        fontSize: m(e?.fontSize) || 16,
        fontWeight: e?.fontWeight || "normal",
        fontStyle: e?.fontStyle || "normal",
        angle: m(e?.angle),
        textColor: e?.textColor || "#ffffff"
      };
    }
    function fn(e, t) {
      return El({
        id: ut(),
        text: "Annotation",
        position: { x: e, y: t }
      });
    }
    function go() {
      return {
        minX: we.value.minX,
        minY: we.value.minY,
        maxX: we.value.maxX,
        maxY: we.value.maxY,
        width: Ot.value,
        height: Wt.value,
        scaleX: tt.value,
        scaleY: Ne.value
      };
    }
    function Bl(e) {
      return {
        id: e?.id ?? ut(),
        nodeID: e?.nodeID ?? e?.vertexNodeID ?? "",
        tangentLinkID1: e?.tangentLinkID1 ?? e?.linkID1 ?? "",
        tangentLinkID2: e?.tangentLinkID2 ?? e?.linkID2 ?? "",
        radius: m(e?.radius) || Nl,
        angle: m(e?.angle),
        tangentDistance: m(e?.tangentDistance),
        start: nt(e?.start ?? { x: e?.startX, y: e?.startY }),
        end: nt(e?.end ?? { x: e?.endX, y: e?.endY }),
        center: nt(e?.center ?? { x: e?.centerX, y: e?.centerY }),
        largeArcFlag: Number(e?.largeArcFlag) === 1 ? 1 : 0,
        sweepFlag: Number(e?.sweepFlag) === 1 ? 1 : 0
      };
    }
    function An() {
      if (oe.value.size !== 1) return null;
      const [e] = [...oe.value];
      return Ee.value.find((t) => t.id === e) || null;
    }
    function En(e) {
      return e ? JSON.parse(JSON.stringify(e)) : null;
    }
    function Xe() {
      q("selected-annotation-change", En(An()));
    }
    function mn(e) {
      return e === "link" ? Y.value : e === "signal" ? xe.value : e === "insulationJoint" ? le.value : e === "bufferStop" ? fe.value : e === "switch" ? te.value : e === "platform" ? Be.value : [];
    }
    function Bn(e) {
      return e === "link" ? ye.value : e === "signal" ? We.value : e === "insulationJoint" ? Ge.value : e === "bufferStop" ? _e.value : e === "switch" ? W.value : e === "platform" ? se.value : /* @__PURE__ */ new Set();
    }
    function Rl(e, t) {
      const n = t instanceof Set ? t : new Set(t);
      e === "link" && (ye.value = n), e === "signal" && (We.value = n), e === "insulationJoint" && (Ge.value = n), e === "bufferStop" && (_e.value = n), e === "switch" && (W.value = n), e === "platform" && (se.value = n);
    }
    function Bt(e, t, n) {
      const i = new Set(Bn(e));
      i.delete(t), i.add(n), Rl(e, i), Le.value?.kind === e && Le.value?.id === t && (Le.value = { kind: e, id: n });
    }
    function Rn(e, t) {
      return t ? {
        kind: e,
        id: t.id,
        data: JSON.parse(JSON.stringify(t))
      } : null;
    }
    function Tl(e, t) {
      return mn(e).find((n) => n.id === t) || null;
    }
    function Xl(e, t) {
      Le.value = { kind: e, id: t };
    }
    function po(e, t) {
      return e === "node" ? Dn(t) : e === "annotation" ? jo(t) : e === "cellName" ? t == null ? null : { id: t } : Tl(e, t);
    }
    function bi(e, t) {
      const n = po(e, t);
      return n ? e === "cellName" ? !0 : e === "node" ? !!y.routePickTarget || D.value === 0 || D.value === 1 && M.value === "w" : y.routePickTarget ? !!qe(n) : D.value === 0 : !1;
    }
    function ot(e, t) {
      bi(e, t) && (mt.value = { kind: e, id: t });
    }
    function et(e, t) {
      if (!e) {
        mt.value = null;
        return;
      }
      $e(e, t) && (mt.value = null);
    }
    function $e(e, t) {
      const n = mt.value;
      return n?.kind === e && String(n.id) === String(t);
    }
    function wi(e, t) {
      return e === "link" ? Zo(t) : e === "node" ? os(t) : e === "signal" ? is(t) : e === "insulationJoint" ? us(t) : e === "bufferStop" ? cs(t) : e === "switch" ? fs(t) : e === "platform" ? ea(t) : e === "annotation" ? Ui(t) : !1;
    }
    function xt(e, t) {
      return wi(e, t) ? Ys : $e(e, t) ? _d : "";
    }
    function ho(e, t) {
      return wi(e, t) ? $d : $e(e, t) ? Vd : "";
    }
    function St(e, t) {
      const n = xt(e, t);
      return n ? { "--equipment-highlight-color": n } : {};
    }
    function yn(e) {
      Le.value && (!e || e(Le.value)) && (Le.value = null);
    }
    function xo() {
      const e = [];
      for (const s of pe) {
        const d = [...Bn(s)];
        if (d.length === 0)
          continue;
        const f = d.map((p) => mn(s).find((I) => I.id === p)).filter((p) => p != null);
        f.length > 0 && e.push({ kind: s, ids: f.map((p) => p.id), items: f });
      }
      if (e.length !== 1) return null;
      const t = e[0];
      if (t.items.length === 1)
        return Rn(t.kind, t.items[0]);
      const n = Le.value, i = n?.kind === t.kind && t.ids.some((s) => String(s) === String(n.id)) ? n.id : t.ids[0], a = t.items.find((s) => String(s.id) === String(i)) || t.items[0];
      return {
        ...Rn(t.kind, a),
        batch: !0,
        ids: t.ids,
        items: t.items.map((s) => Rn(t.kind, s)),
        count: t.items.length
      };
    }
    function lt() {
      q("selected-equipment-change", xo());
    }
    function Tn() {
      We.value = /* @__PURE__ */ new Set(), Ge.value = /* @__PURE__ */ new Set(), _e.value = /* @__PURE__ */ new Set(), W.value = /* @__PURE__ */ new Set(), se.value = /* @__PURE__ */ new Set();
    }
    function Ii(e) {
      oe.value = new Set(e), Xe();
    }
    function So(e) {
      if (y.readonly) return;
      const t = An();
      t && (Ce(() => {
        const n = Ee.value.find((i) => i.id === t.id);
        n && (e.position && (n.position = {
          ...n.position,
          ...e.position
        }), Object.assign(n, { ...e, position: n.position }), Ee.value = Ee.value.map((i) => i.id === n.id ? El(n) : i));
      }), Xe());
    }
    function bo(e) {
      return `translate(${U(e.position?.x)},${G(e.position?.y)}) rotate(${m(e.angle)})`;
    }
    function Je(e) {
      return Math.round(m(e) * 1e3) / 1e3;
    }
    function wo(e) {
      return oe.value.size === 1 && Ui(e.id);
    }
    function Yl(e) {
      return {
        minX: Math.min(m(e.startX), m(e.endX)),
        minY: Math.min(m(e.startY), m(e.endY)),
        maxX: Math.max(m(e.startX), m(e.endX)),
        maxY: Math.max(m(e.startY), m(e.endY))
      };
    }
    function ki(e) {
      return Math.abs($n(m(e.endX) - m(e.startX))) >= Ps || Math.abs(un(m(e.endY) - m(e.startY))) >= Ps;
    }
    const Io = A(() => {
      if (y.readonly) return [];
      const e = [];
      for (const t of Y.value) {
        if (!ye.value.has(t.id)) continue;
        const n = ne.size / 2, i = { id: `sp${t.id}`, lineId: t.id, type: "sp", x: Number(t.x1) - n, y: Number(t.y1) - n }, a = { id: `ep${t.id}`, lineId: t.id, type: "ep", x: Number(t.x2) - n, y: Number(t.y2) - n };
        e.push(i, a);
      }
      return e;
    }), vn = A(() => {
      const e = [];
      if (!y.showGrid) return e;
      const t = dn(), n = zn(), i = we.value, a = cn(i.minX, t, Ct()), s = cn(i.minY, n, At()), d = K(i.maxX, t, Ct()), f = K(i.maxY, n, At());
      for (let p = a; p <= d; p += t)
        for (let I = s; I <= f; I += n)
          e.push({ x: p, y: I });
      return e;
    });
    function Mt() {
      return {
        minX: 1 / 0,
        minY: 1 / 0,
        maxX: -1 / 0,
        maxY: -1 / 0
      };
    }
    function Pl(e) {
      return !Number.isFinite(e.minX) || !Number.isFinite(e.minY) || !Number.isFinite(e.maxX) || !Number.isFinite(e.maxY);
    }
    function nl(e, t, n, i, a, s = 0) {
      const d = Number(t), f = Number(n), p = Number(i), I = Number(a);
      if (![d, f, p, I].every(Number.isFinite)) return;
      const b = Math.max(0, s) / tt.value, C = Math.max(0, s) / Ne.value;
      e.minX = Math.min(e.minX, Math.min(d, p) - b), e.minY = Math.min(e.minY, Math.min(f, I) - C), e.maxX = Math.max(e.maxX, Math.max(d, p) + b), e.maxY = Math.max(e.maxY, Math.max(f, I) + C);
    }
    function ll(e, t, n = ao) {
      t && nl(e, t.x, t.y, t.x, t.y, n);
    }
    function Xn(e, t, n = ao) {
      ll(e, t?.position, n);
    }
    function Ol(e = ao) {
      const t = Mt();
      for (const n of Y.value)
        nl(t, n.x1, n.y1, n.x2, n.y2, e);
      for (const n of Ae.value)
        if (ll(t, n.start, e), ll(t, n.end, e), n.center) {
          const i = Math.max(0, m(n.radius));
          nl(
            t,
            m(n.center.x) - i,
            m(n.center.y) - i,
            m(n.center.x) + i,
            m(n.center.y) + i,
            e
          );
        }
      for (const n of O.value)
        ll(t, n, e);
      for (const n of xe.value)
        Xn(t, n, e);
      for (const n of le.value)
        Xn(t, n, e);
      for (const n of fe.value)
        Xn(t, n, e);
      for (const n of Be.value)
        nl(
          t,
          n.x,
          n.y,
          m(n.x) + m(n.width),
          m(n.y) + m(n.height),
          e
        );
      for (const n of te.value)
        Xn(t, n, e);
      for (const n of Ee.value)
        Xn(t, n, e);
      return t;
    }
    function ko() {
      const e = Ol(0);
      return Pl(e) ? (Fl(), !1) : (st.originX = Et(e.minX, dn()), st.originY = Et(e.minY, zn()), !0);
    }
    function Wl() {
      const e = Ol(ao);
      return Pl(e) ? !1 : tl(e, { padding: da });
    }
    function il(e, t) {
      const n = m(e.x1), i = m(e.y1), a = m(e.x2), s = m(e.y2);
      return {
        x: n + (a - n) * t,
        y: i + (s - i) * t
      };
    }
    function Ni(e, t) {
      const n = m(e.x1), i = m(e.y1), a = m(e.x2) - n, s = m(e.y2) - i, d = a * a + s * s;
      if (d <= 0) return null;
      const f = ((m(t?.x) - n) * a + (m(t?.y) - i) * s) / d;
      return Number.isFinite(f) ? Math.max(0, Math.min(1, f)) : null;
    }
    function No(e) {
      const t = e.map((i) => ({
        start: Math.max(0, Math.min(1, Math.min(i.start, i.end))),
        end: Math.max(0, Math.min(1, Math.max(i.start, i.end)))
      })).filter((i) => i.end - i.start > 1e-6).sort((i, a) => i.start - a.start), n = [];
      for (const i of t) {
        const a = n[n.length - 1];
        a && i.start <= a.end + 1e-6 ? a.end = Math.max(a.end, i.end) : n.push({ ...i });
      }
      return n;
    }
    function Do(e) {
      const t = No(e), n = [];
      let i = 0;
      for (const a of t)
        a.start > i + 1e-6 && n.push({ start: i, end: a.start }), i = Math.max(i, a.end);
      return i < 1 - 1e-6 && n.push({ start: i, end: 1 }), n;
    }
    function Di(e, t, n, i, a, s) {
      const d = i?.[a], f = t.get(d), p = n.get(i?.nodeID);
      if (!f || !p) return;
      const I = Ni(f, p), b = Ni(f, i?.[s]);
      I == null || b == null || (e.has(f.id) || e.set(f.id, []), e.get(f.id).push({ start: I, end: b }));
    }
    const gn = A(() => {
      const e = new Map(Y.value.map((a) => [a.id, a])), t = new Map(O.value.map((a) => [a.id, a])), n = /* @__PURE__ */ new Map();
      if (y.showCurveArc)
        for (const a of Ae.value)
          Di(n, e, t, a, "tangentLinkID1", "start"), Di(n, e, t, a, "tangentLinkID2", "end");
      const i = [];
      for (const a of Y.value)
        Do(n.get(a.id) || []).forEach((d, f) => {
          const p = il(a, d.start), I = il(a, d.end);
          i.push({
            id: `${a.id}-visible-${f}`,
            line: a,
            x1: p.x,
            y1: p.y,
            x2: I.x,
            y2: I.y,
            rateStart: d.start,
            rateEnd: d.end
          });
        });
      return i;
    }), Gl = A(() => y.showCurveArc ? Ae.value : []), Li = A(() => Y.value.flatMap((e) => du(e))), Ci = A(() => Y.value.filter((e) => Zi(e)).map((e) => {
      const n = gn.value.filter((i) => i.line.id === e.id).map((i) => ({
        segment: i,
        length: Math.hypot(
          m(i.x2) - m(i.x1),
          m(i.y2) - m(i.y1)
        )
      })).sort((i, a) => a.length - i.length)[0]?.segment;
      return n ? {
        id: e.id,
        line: e,
        x: (m(n.x1) + m(n.x2)) / 2,
        y: (m(n.y1) + m(n.y2)) / 2
      } : null;
    }).filter((e) => e != null));
    function Mi(e) {
      return String(e ?? "").split(/[\s,，;；]+/).map((t) => t.trim()).filter((t) => t !== "");
    }
    function Ul(e) {
      return String(e?.id ?? e?.ID ?? "").trim();
    }
    function Lo(e) {
      return String(e?.name ?? e?.Name ?? Ul(e)).trim();
    }
    function ol(e) {
      return Mi(e?.linkIDList ?? e?.LinkIDList);
    }
    function Co(e) {
      return Math.hypot(
        m(e.x2) - m(e.x1),
        m(e.y2) - m(e.y1)
      );
    }
    function _i(e, t, n) {
      const i = Lo(e);
      if (!i) return null;
      let a = 0, s = 0, d = 0;
      for (const p of new Set(ol(e))) {
        const I = n.get(p) || [];
        for (const b of I) {
          const C = Co(b);
          !Number.isFinite(C) || C <= 0 || (a += (m(b.x1) + m(b.x2)) / 2 * C, s += (m(b.y1) + m(b.y2)) / 2 * C, d += C);
        }
      }
      if (d <= 0) return null;
      const f = Ul(e) || String(t);
      return {
        id: f,
        key: `${f || "cell"}-${t}`,
        name: i,
        x: a / d,
        y: s / d
      };
    }
    const Jl = A(() => {
      if (!y.showCellNames || !Array.isArray(y.cells) || y.cells.length === 0) return [];
      const e = /* @__PURE__ */ new Map();
      for (const t of gn.value) {
        const n = String(t.line?.id ?? "").trim();
        n && (e.has(n) || e.set(n, []), e.get(n).push(t));
      }
      return y.cells.map((t, n) => _i(t, n, e)).filter((t) => t != null);
    });
    function Mo(e) {
      q("cell-name-click", {
        id: e.id,
        name: e.name
      });
    }
    function pn() {
      return JSON.parse(
        JSON.stringify({
          latestElementID: rt.value,
          tracks: Y.value,
          curves: Ae.value,
          nodes: O.value,
          signals: xe.value,
          insulationJoints: le.value,
          bufferStops: fe.value,
          platforms: Be.value,
          switches: te.value,
          annotations: Ee.value,
          selectedLineIds: [...ye.value],
          selectedNodeIds: [...De.value],
          selectedSignalIds: [...We.value],
          selectedInsulationJointIds: [...Ge.value],
          selectedBufferStopIds: [..._e.value],
          selectedSwitchIds: [...W.value],
          selectedPlatformIds: [...se.value],
          selectedAnnotationIds: [...oe.value],
          lastSelectedEquipment: Le.value
        })
      );
    }
    function ql(e) {
      rt.value = e.latestElementID, Y.value = e.tracks || [], Ae.value = (e.curves || []).map((t) => Bl(t)), O.value = e.nodes || [], xe.value = (e.signals || []).map((t) => sl(t)), le.value = (e.insulationJoints || []).map((t) => hn(t)), fe.value = (e.bufferStops || []).map((t) => Hl(t)), Be.value = (e.platforms || []).map((t) => al(t)), te.value = (e.switches || []).map((t) => sl(t)), Ee.value = e.annotations || [], Kl(), ye.value = new Set(e.selectedLineIds || []), De.value = new Set(e.selectedNodeIds || []), We.value = new Set(e.selectedSignalIds || []), Ge.value = new Set(e.selectedInsulationJointIds || []), _e.value = new Set(e.selectedBufferStopIds || []), W.value = new Set(e.selectedSwitchIds || []), se.value = new Set(e.selectedPlatformIds || []), oe.value = new Set(e.selectedAnnotationIds || []), Le.value = e.lastSelectedEquipment || null, Wl(), Xe(), lt();
    }
    function Ce(e, t = {}) {
      if (y.readonly && t?.allowReadonly !== !0) return;
      const n = Ft === 0;
      n && (Qe.value.push(pn()), Qe.value.length > 30 && Qe.value.shift(), Dt.value = []), Ft += 1;
      try {
        return e();
      } finally {
        Ft -= 1, n && Wl();
      }
    }
    function Vi() {
      if (y.readonly || Qe.value.length === 0) return;
      Dt.value.push(pn());
      const e = Qe.value.pop();
      ql(e);
    }
    function jl() {
      if (y.readonly || Dt.value.length === 0) return;
      Qe.value.push(pn());
      const e = Dt.value.pop();
      ql(e);
    }
    function ut() {
      const e = String(rt.value);
      return rt.value += 1, e;
    }
    function al(e) {
      const t = { ...e || {} }, n = t.id == null ? "" : String(t.id).trim(), i = t.name == null ? "" : String(t.name).trim();
      return t.name = i || n, t;
    }
    function qe(e) {
      return String(
        e?.bindingNodeID ?? e?.BindingNodeID ?? e?.bindingNodeId ?? e?.BindingNodeId ?? ""
      ).trim();
    }
    function _t(e, t) {
      if (!e) return "";
      const n = String(t ?? "").trim();
      return e.bindingNodeID = n, delete e.BindingNodeID, delete e.bindingNodeId, delete e.BindingNodeId, n;
    }
    function hn(e) {
      const t = { ...e || {} };
      return _t(t, qe(t)), t;
    }
    function sl(e) {
      return hn(al(e));
    }
    function Hl(e) {
      const t = hn(e);
      return t.direction = no(t.direction ?? t.Direction), t.type = so(t.type ?? t.Type ?? t.style ?? t.Style), t;
    }
    function xn(e) {
      const t = Dn(qe(e));
      return t ? (_t(e, t.id), e.position = {
        ...e.position || {},
        x: m(t.x),
        y: m(t.y)
      }, t) : null;
    }
    function Kl() {
      for (const e of xe.value)
        xn(e);
      for (const e of le.value)
        xn(e);
      for (const e of fe.value)
        xn(e);
      for (const e of te.value) {
        const t = xn(e);
        if (t) {
          const n = re(t);
          (n.length > 0 || !Array.isArray(e.branchVectorList)) && (e.branchVectorList = n);
        }
      }
    }
    function Sn(e, t) {
      const n = e?.name == null ? "" : String(e.name).trim();
      return n || (e?.id == null ? "" : String(e.id).trim()) || t;
    }
    function Zl() {
      ye.value = /* @__PURE__ */ new Set(), yn((e) => e.kind === "link"), Ln(), lt();
    }
    function Jt() {
      De.value = /* @__PURE__ */ new Set(), Sl();
    }
    function Yn() {
      Tn(), yn((e) => e.kind !== "link"), oe.value = /* @__PURE__ */ new Set(), bl(), Xe(), lt();
    }
    function Rt(e = {}) {
      const t = e.notify !== !1;
      ye.value = /* @__PURE__ */ new Set(), De.value = /* @__PURE__ */ new Set(), Tn(), oe.value = /* @__PURE__ */ new Set(), Le.value = null, Ln(), Sl(), bl(), t && (Xe(), lt());
    }
    function Ql() {
      Re.value = null, Ue.value = { ...Ue.value, visible: !1 }, yt.value = { ...yt.value, visible: !1 }, Ve.value = { ...Ve.value, visible: !1 }, ht.value = { ...ht.value, visible: !1 }, Te.value = null, Nt.value = null;
    }
    function bn(e) {
      et();
      const t = y.readonly ? 0 : Number(e);
      (t !== 1 || D.value !== 1) && Ql(), D.value = t;
    }
    function _o(e) {
      Ue.value.type = Sa(e);
    }
    function Vo(e) {
      Ve.value.direction = no(e);
    }
    function $o(e) {
      Ve.value.type = so(e);
    }
    function zo(e) {
      if (y.readonly) return;
      const t = String(e || "");
      M.value !== t && Ql(), M.value = t, de.value && (t === "s" ? Q() : t === "i" ? ze() : t === "e" ? jt() : t === "n" ? Ke() : t === "p" ? _a() : t === "a" && In());
    }
    function Fo(e) {
      it.value = Number(e);
    }
    function Pn(e) {
      pt.value = Number(e);
    }
    function Vt(e, t) {
      if (!Z.value) return;
      const n = Z.value.getScreenCTM();
      if (!n) return;
      const i = Z.value.createSVGPoint();
      return i.x = e, i.y = t, i.matrixTransform(n.inverse());
    }
    function $t(e, t) {
      const n = Vt(e, t);
      return n ? {
        x: gi(n.x),
        y: pi(n.y)
      } : null;
    }
    function ei(e) {
      const t = dn(), n = zn(), i = Ct(), a = At();
      return {
        x: i + Math.round((m(e?.x) - i) / t) * t,
        y: a + Math.round((m(e?.y) - a) / n) * n
      };
    }
    function $i(e) {
      return it.value !== 1 ? nt(e) : ei(e);
    }
    function Ao() {
      return O.value.map((e) => ({
        x: m(e.x),
        y: m(e.y),
        id: e.id,
        kind: "node"
      })).filter((e) => Number.isFinite(e.x) && Number.isFinite(e.y));
    }
    function zi(e, t, n) {
      let i = null;
      const a = nt(e);
      for (const s of t) {
        const d = nt(s), f = Math.hypot(d.x - a.x, d.y - a.y);
        f > n || (!i || f < i.dist) && (i = {
          ...s,
          x: d.x,
          y: d.y,
          dist: f
        });
      }
      return i;
    }
    function Eo(e, t) {
      return zi(e, Ao(), t);
    }
    function Bo(e, t) {
      let n = null;
      const i = nt(e);
      for (const a of Y.value) {
        const s = li(a, i);
        !s || !Number.isFinite(s.dist) || s.dist > t || (!n || s.dist < n.dist) && (n = {
          x: Je(s.x),
          y: Je(s.y),
          dist: s.dist,
          id: a.id,
          kind: "edge"
        });
      }
      return n;
    }
    function Fi(e) {
      if (pt.value !== 1) return null;
      const t = Ml.value, n = Eo(e, t);
      return n || Bo(e, t);
    }
    function Ro(e) {
      const t = nt(e);
      if (it.value === 1)
        return {
          ...ei(t),
          kind: "grid"
        };
      const n = Fi(t);
      return n || {
        ...nt(t),
        kind: "free"
      };
    }
    function rl(e, t) {
      const n = $t(e, t);
      if (!n) return;
      const i = nt(n), a = Ro(i), s = a.x, d = a.y;
      ee.value.x = s, ee.value.y = d, D.value !== 0 && Fn({ x: s, y: d }, { triggerMargin: oo }), D.value === 1 ? a.kind === "edge" ? Nt.value = { x: s, y: d } : kn(i.x, i.y) : Nt.value = null;
    }
    function zt(e, t) {
      const n = m(e.x), i = m(e.y);
      return n >= t.minX && n <= t.maxX && i >= t.minY && i <= t.maxY;
    }
    function On(e, t, n) {
      return (m(t.x) - m(e.x)) * (m(n.y) - m(e.y)) - (m(t.y) - m(e.y)) * (m(n.x) - m(e.x));
    }
    function ul(e, t, n) {
      return Math.abs(On(t, n, e)) > 1e-6 ? !1 : m(e.x) >= Math.min(m(t.x), m(n.x)) - 1e-6 && m(e.x) <= Math.max(m(t.x), m(n.x)) + 1e-6 && m(e.y) >= Math.min(m(t.y), m(n.y)) - 1e-6 && m(e.y) <= Math.max(m(t.y), m(n.y)) + 1e-6;
    }
    function cl(e, t, n, i) {
      const a = On(e, t, n), s = On(e, t, i), d = On(n, i, e), f = On(n, i, t), p = 1e-6;
      return Math.abs(a) <= p && ul(n, e, t) || Math.abs(s) <= p && ul(i, e, t) || Math.abs(d) <= p && ul(e, n, i) || Math.abs(f) <= p && ul(t, n, i) ? !0 : (a > 0 && s < 0 || a < 0 && s > 0) && (d > 0 && f < 0 || d < 0 && f > 0);
    }
    function bt(e, t) {
      const n = { x: m(e.x1), y: m(e.y1) }, i = { x: m(e.x2), y: m(e.y2) };
      if (zt(n, t) || zt(i, t)) return !0;
      if (Math.max(n.x, i.x) < t.minX || Math.min(n.x, i.x) > t.maxX || Math.max(n.y, i.y) < t.minY || Math.min(n.y, i.y) > t.maxY) return !1;
      const a = { x: t.minX, y: t.minY }, s = { x: t.maxX, y: t.minY }, d = { x: t.maxX, y: t.maxY }, f = { x: t.minX, y: t.maxY };
      return cl(n, i, a, s) || cl(n, i, s, d) || cl(n, i, d, f) || cl(n, i, f, a);
    }
    function Ye(e, t, n, i) {
      const a = m(e), s = m(t), d = a + m(n), f = s + m(i);
      return {
        minX: Math.min(a, d),
        minY: Math.min(s, f),
        maxX: Math.max(a, d),
        maxY: Math.max(s, f)
      };
    }
    function Ai(e, t) {
      return e.minX <= t.maxX && e.maxX >= t.minX && e.minY <= t.maxY && e.maxY >= t.minY;
    }
    function Ei(e) {
      const t = Yl(e), n = new Set(ye.value), i = new Set(De.value), a = new Set(We.value), s = new Set(Ge.value), d = new Set(_e.value), f = new Set(W.value), p = new Set(se.value), I = new Set(oe.value);
      for (const b of Y.value)
        bt(b, t) && n.add(b.id);
      for (const b of O.value)
        zt(b, t) && i.add(b.id);
      for (const b of xe.value)
        zt(b.position || {}, t) && a.add(b.id);
      for (const b of le.value)
        zt(b.position || {}, t) && s.add(b.id);
      for (const b of fe.value)
        zt(b.position || {}, t) && d.add(b.id);
      for (const b of te.value)
        zt(b.position || {}, t) && f.add(b.id);
      for (const b of Be.value)
        Ai(Ye(b.x, b.y, b.width, b.height), t) && p.add(b.id);
      for (const b of Ee.value)
        zt(b.position || {}, t) && I.add(b.id);
      ye.value = n, De.value = i, We.value = a, Ge.value = s, _e.value = d, W.value = f, se.value = p, oe.value = I, Ln(), Xe(), lt();
    }
    function ve() {
      window.addEventListener("mousemove", Bi), window.addEventListener("mouseup", ti);
    }
    function wn() {
      window.removeEventListener("mousemove", Bi), window.removeEventListener("mouseup", ti);
    }
    function To(e) {
      const t = $t(e.clientX, e.clientY);
      t && (He.value = {
        startX: t.x,
        startY: t.y,
        endX: t.x,
        endY: t.y
      }, ve());
    }
    function dl(e, t) {
      if (!He.value) return;
      const n = $t(e, t);
      n && (He.value.endX = n.x, He.value.endY = n.y);
    }
    function Tt() {
      if (!He.value) return;
      const e = He.value;
      wn(), ki(e) && Ei(e), He.value = null;
    }
    function In() {
      He.value = null, wn();
    }
    function Bi(e) {
      dl(e.clientX, e.clientY);
    }
    function ti(e) {
      dl(e.clientX, e.clientY), Tt();
    }
    function Xo(e, t) {
      Re.value = { x1: e, y1: t, x2: e, y2: t };
    }
    function Ri(e, t) {
      Re.value && (Re.value.x2 = e, Re.value.y2 = t);
    }
    function Yo() {
      if (!Re.value) return;
      const e = {
        id: ut(),
        name: "",
        x1: Re.value.x1,
        y1: Re.value.y1,
        x2: Re.value.x2,
        y2: Re.value.y2,
        fromNodeID: "",
        toNodeID: ""
      }, t = y.autoGenerateTopology ? ml() : null;
      let n = null;
      Ce(() => {
        Y.value.push(e), y.autoGenerateTopology && (E(), hl(), h(), Kl(), xl({
          lineIds: new Set(Y.value.map((i) => i.id)),
          nodeIds: new Set(O.value.map((i) => i.id))
        }), n = yl(t));
      }), Re.value = null, vl(n);
    }
    function Xt(e, t, n) {
      if (n?.ctrlKey || Rt({ notify: !1 }), e === "node")
        De.value = /* @__PURE__ */ new Set([...De.value, t]);
      else if (e === "annotation")
        oe.value = /* @__PURE__ */ new Set([...oe.value, t]);
      else {
        const i = new Set(Bn(e));
        i.add(t), Rl(e, i), Xl(e, t);
      }
      Xe(), lt();
    }
    function Po() {
      y.readonly || ye.value.size !== 0 && (Ce(() => {
        Y.value = Y.value.filter((e) => !ye.value.has(e.id)), ye.value = /* @__PURE__ */ new Set(), yn((e) => e.kind === "link"), Ln();
      }), lt());
    }
    function Ti(e) {
      const t = new Set([...e].map((i) => String(i ?? ""))), n = (i, a) => a.filter((s) => t.has(qe(s))).map((s) => ({
        kind: i,
        id: s.id,
        name: s.name || s.id || "",
        bindingNodeID: qe(s)
      }));
      return [
        ...n("signal", xe.value),
        ...n("insulationJoint", le.value),
        ...n("switch", te.value),
        ...n("bufferStop", fe.value)
      ];
    }
    function ni(e) {
      return e.reduce((t, n) => (t[n.kind] = (t[n.kind] || 0) + 1, t), {});
    }
    function vt() {
      const e = [...De.value], t = Ti(new Set(e));
      return {
        nodeIds: e,
        boundEquipment: t,
        counts: ni(t),
        requiresConfirmation: t.length > 0
      };
    }
    function Oo(e) {
      const t = e.reduce((n, i) => (n[i.kind] || (n[i.kind] = /* @__PURE__ */ new Set()), n[i.kind].add(i.id), n), {});
      t.signal && (xe.value = xe.value.filter((n) => !t.signal.has(n.id)), We.value = new Set([...We.value].filter((n) => !t.signal.has(n)))), t.insulationJoint && (le.value = le.value.filter((n) => !t.insulationJoint.has(n.id)), Ge.value = new Set([...Ge.value].filter((n) => !t.insulationJoint.has(n)))), t.switch && (te.value = te.value.filter((n) => !t.switch.has(n.id)), W.value = new Set([...W.value].filter((n) => !t.switch.has(n)))), t.bufferStop && (fe.value = fe.value.filter((n) => !t.bufferStop.has(n.id)), _e.value = new Set([..._e.value].filter((n) => !t.bufferStop.has(n)))), yn((n) => !!t[n.kind]?.has(n.id));
    }
    function Wo(e = {}) {
      if (y.readonly || De.value.size === 0) return;
      const t = new Set(De.value), n = Ti(t);
      return n.length > 0 && e?.deleteBoundEquipment !== !0 ? {
        requiresConfirmation: !0,
        nodeIds: [...t],
        boundEquipment: n,
        counts: ni(n)
      } : (Ce(() => {
        O.value = O.value.filter((i) => !t.has(i.id)), De.value = /* @__PURE__ */ new Set(), e?.deleteBoundEquipment === !0 && Oo(n);
      }), lt(), {
        deleted: !0,
        nodeIds: [...t],
        boundEquipment: n,
        counts: ni(n)
      });
    }
    function Go() {
      y.readonly || Ce(() => {
        xe.value = xe.value.filter((e) => !We.value.has(e.id)), le.value = le.value.filter((e) => !Ge.value.has(e.id)), fe.value = fe.value.filter((e) => !_e.value.has(e.id)), te.value = te.value.filter((e) => !W.value.has(e.id)), Be.value = Be.value.filter((e) => !se.value.has(e.id)), Ee.value = Ee.value.filter((e) => !oe.value.has(e.id)), Yn();
      });
    }
    function fl() {
      return Y.value.map((e) => ({ ...e }));
    }
    function Uo() {
      return O.value.map((e) => ({ ...e }));
    }
    function Xi() {
      return [
        ...xe.value.map((e) => ({ kind: "signal", equipment: e })),
        ...le.value.map((e) => ({ kind: "insulationJoint", equipment: e })),
        ...fe.value.map((e) => ({ kind: "bufferStop", equipment: e })),
        ...te.value.map((e) => ({ kind: "switch", equipment: e }))
      ];
    }
    function ml() {
      const e = /* @__PURE__ */ new Map();
      for (const { kind: t, equipment: n } of Xi()) {
        const i = String(n?.id ?? "").trim();
        i && e.set(`${t}:${i}`, qe(n));
      }
      return {
        lineIds: new Set(Y.value.map((t) => String(t?.id ?? "")).filter((t) => t !== "")),
        nodeIds: new Set(O.value.map((t) => String(t?.id ?? "")).filter((t) => t !== "")),
        equipmentBindings: e,
        equipmentCount: e.size,
        curveCount: Ae.value.length,
        switchCount: te.value.length,
        hasCells: Array.isArray(y.cells) && y.cells.length > 0
      };
    }
    function yl(e) {
      if (!e) return null;
      const t = new Set(Y.value.map((R) => String(R?.id ?? "")).filter((R) => R !== "")), n = new Set(O.value.map((R) => String(R?.id ?? "")).filter((R) => R !== "")), i = [...e.lineIds].filter((R) => !t.has(R)), a = [...e.nodeIds].filter((R) => !n.has(R)), s = [...t].filter((R) => !e.lineIds.has(R)).length, d = [...n].filter((R) => !e.nodeIds.has(R)).length, f = [], p = [];
      for (const { kind: R, equipment: he } of Xi()) {
        const Ze = String(he?.id ?? "").trim();
        if (!Ze) continue;
        const Gn = `${R}:${Ze}`, dt = qe(he), ft = e.equipmentBindings.get(Gn);
        ft != null && ft !== dt && f.push({ kind: R, id: Ze, previousBindingNodeId: ft, currentBindingNodeId: dt }), dt && !n.has(dt) && p.push({ kind: R, id: Ze, bindingNodeId: dt });
      }
      const I = i.length > 0 || a.length > 0, b = I || s > 0 || d > 0, C = e.hasCells && b, B = f.length > 0 || p.length > 0 || I && e.equipmentCount > 0, T = I && e.curveCount > 0, ge = I && e.switchCount > 0, L = I;
      return {
        topologyChanged: b,
        rewiredExistingTopology: I,
        removedLineIds: i,
        removedNodeIds: a,
        changedEquipmentBindings: f,
        missingEquipmentBindings: p,
        cellsMayNeedRebuild: C,
        equipmentMayNeedRepair: B,
        curvesMayNeedRepair: T,
        switchesMayNeedRepair: ge,
        routesMayNeedReview: L,
        requiresRepair: C || B || T || ge || L
      };
    }
    function vl(e) {
      e?.requiresRepair && q("topology-rebuilt", e);
    }
    function gt(e, t) {
      return Math.trunc(Math.hypot(Number(t.x) - Number(e.x), Number(t.y) - Number(e.y)));
    }
    function li(e, t) {
      const n = Number(e.x1), i = Number(e.y1), a = Number(e.x2), s = Number(e.y2), d = Number(t.x), f = Number(t.y), p = a - n, I = s - i, b = p * p + I * I;
      if (b === 0)
        return { x: n, y: i, dist: Math.hypot(d - n, f - i) };
      const C = Math.max(0, Math.min(1, ((d - n) * p + (f - i) * I) / b)), B = n + C * p, T = i + C * I;
      return { x: B, y: T, dist: Math.hypot(d - B, f - T) };
    }
    function Yi(e, t) {
      const n = (t.x1 - t.x2) * (e.y1 - e.y2) - (e.x1 - e.x2) * (t.y1 - t.y2), i = (t.y1 - t.y2) * (e.x1 - e.x2) - (e.y1 - e.y2) * (t.x1 - t.x2);
      if (n === 0 || i === 0) return null;
      const a = Math.round(((t.x1 - t.x2) * (e.x2 * e.y1 - e.x1 * e.y2) - (e.x1 - e.x2) * (t.x2 * t.y1 - t.x1 * t.y2)) / n), s = Math.round(((t.y1 - t.y2) * (e.y2 * e.x1 - e.y1 * e.x2) - (e.y1 - e.y2) * (t.y2 * t.x1 - t.y1 * t.x2)) / i);
      return { x: a, y: s };
    }
    function ct(e, t) {
      return Math.hypot(Number(t.x) - Number(e.x), Number(t.y) - Number(e.y)) < 1;
    }
    function qt(e, t) {
      return O.value.find((n) => ct(n, { x: e, y: t })) || null;
    }
    function Pi(e, t) {
      return ct(t, { x: e.x1, y: e.y1 }) || ct(t, { x: e.x2, y: e.y2 });
    }
    function gl(e, t) {
      const n = Number(e.x1), i = Number(e.y1), a = Number(e.x2), s = Number(e.y2), d = Number(t.x), f = Number(t.y), p = a - n, I = s - i, b = Math.hypot(p, I);
      return b === 0 ? ct(t, { x: n, y: i }) : d < Math.min(n, a) - 1 || d > Math.max(n, a) + 1 || f < Math.min(i, s) - 1 || f > Math.max(i, s) + 1 ? !1 : Math.abs(I * d - p * f + a * i - s * n) / b < 1;
    }
    function Jo(e, t) {
      const n = Number(e.x2) - Number(e.x1), i = Number(e.y2) - Number(e.y1), a = n * n + i * i;
      return a === 0 ? 0 : ((Number(t.x) - Number(e.x1)) * n + (Number(t.y) - Number(e.y1)) * i) / a;
    }
    function o(e, t, n) {
      let i = !1, a = !1;
      const s = gt(n, { x: e.x1, y: e.y1 }) === 0, d = gt(n, { x: e.x2, y: e.y2 }) === 0, f = gt(n, { x: t.x1, y: t.y1 }) === 0, p = gt(n, { x: t.x2, y: t.y2 }) === 0, I = gt(n, { x: t.x1, y: t.y1 }), b = gt(n, { x: t.x2, y: t.y2 }), C = gt(n, { x: e.x1, y: e.y1 }), B = gt(n, { x: e.x2, y: e.y2 }), T = [];
      if (C < at.value && T.push({ line: e, point: 1 }), B < at.value && T.push({ line: e, point: 2 }), I < at.value && T.push({ line: t, point: 1 }), b < at.value && T.push({ line: t, point: 2 }), Math.min(e.x1, e.x2) <= n.x && n.x <= Math.max(e.x1, e.x2) && Math.min(e.y1, e.y2) <= n.y && n.y <= Math.max(e.y1, e.y2) && (i = !0), Math.min(t.x1, t.x2) <= n.x && n.x <= Math.max(t.x1, t.x2) && Math.min(t.y1, t.y2) <= n.y && n.y <= Math.max(t.y1, t.y2) && (a = !0), i && a) {
        if ((s || d) && (f || p))
          return { code: 0 };
        if (s || d || f || p)
          return { code: 1, breakingLineList: [!s && !d ? e : t] };
        if (!s && !d && !f && !p)
          return Math.min(C, B, I, b) < at.value ? { code: 3, snapPointList: T } : { code: 2, breakingLineList: [e, t] };
      }
      return !a && !i && Math.min(C, B) <= at.value && Math.min(I, b) <= at.value ? { code: 4, snapPointList: T } : { code: 6 };
    }
    function l() {
      const e = fl(), t = [];
      for (let n = 0; n < e.length; n += 1)
        for (let i = n + 1; i < e.length; i += 1) {
          const a = e[n], s = e[i], d = Yi(a, s);
          if (!d) continue;
          const f = o(a, s, d);
          d.relation = f, f.code <= 4 && t.push({ ...d, code: f.code, relation: f });
        }
      return sn.value = t, t;
    }
    function r() {
      sn.value = [];
    }
    function S() {
      Ce(() => {
        const e = fl(), t = [];
        for (const n of e) {
          const i = [
            { pointid: 1, x: Number(n.x1), y: Number(n.y1) },
            { pointid: 2, x: Number(n.x2), y: Number(n.y2) }
          ];
          for (const a of i) {
            let s = null;
            for (const d of e) {
              if (d.id === n.id) continue;
              const f = li(d, a);
              f.dist > at.value || (!s || f.dist < s.dist) && (s = f);
            }
            s && t.push({
              lineId: n.id,
              pointid: a.pointid,
              x: Math.round(s.x),
              y: Math.round(s.y)
            });
          }
        }
        for (const n of t) {
          const i = Y.value.find((a) => a.id === n.lineId);
          i && (i[`x${n.pointid}`] = n.x, i[`y${n.pointid}`] = n.y);
        }
        xl({
          lineIds: new Set(t.map((n) => n.lineId))
        }), l();
      });
    }
    function h() {
      const e = Ft === 0, t = e ? ml() : null, n = Number(Pt.value);
      if (!Number.isFinite(n) || n < 0 || O.value.length < 2) return;
      const i = Uo(), a = /* @__PURE__ */ new Set(), s = [];
      for (const f of i) {
        if (a.has(f.id)) continue;
        const p = [], I = [f];
        for (a.add(f.id); I.length > 0; ) {
          const b = I.pop();
          p.push(b);
          for (const C of i) {
            if (a.has(C.id)) continue;
            Math.hypot(Number(C.x) - Number(b.x), Number(C.y) - Number(b.y)) <= n && (a.add(C.id), I.push(C));
          }
        }
        p.length > 1 && s.push(p);
      }
      if (s.length === 0) return;
      Ce(() => {
        const f = /* @__PURE__ */ new Map(), p = /* @__PURE__ */ new Set();
        for (const L of s) {
          const R = L[0].id;
          for (const he of L)
            f.set(he.id, R), he.id !== R && p.add(he.id);
        }
        const I = (L) => f.get(L) || L;
        O.value = O.value.filter((L) => !p.has(L.id));
        const b = new Map(O.value.map((L) => [L.id, L]));
        for (const L of Y.value) {
          L.fromNodeID = I(L.fromNodeID), L.toNodeID = I(L.toNodeID);
          const R = b.get(L.fromNodeID), he = b.get(L.toNodeID);
          R && (L.x1 = R.x, L.y1 = R.y), he && (L.x2 = he.x, L.y2 = he.y);
        }
        const C = /* @__PURE__ */ new Set();
        Y.value = Y.value.filter((L) => L.fromNodeID && L.fromNodeID === L.toNodeID && ct({ x: L.x1, y: L.y1 }, { x: L.x2, y: L.y2 }) ? (C.add(L.id), !1) : !0);
        const B = new Map(O.value.map((L) => [L.id, /* @__PURE__ */ new Set()])), T = (L, R) => {
          const he = B.get(L);
          he && he.add(R);
        };
        for (const L of Y.value)
          T(L.fromNodeID, L.id), T(L.toNodeID, L.id);
        for (const L of O.value)
          L.adjacentLineIDList = [...B.get(L.id) || []];
        const ge = (L, R) => {
          const he = I(qe(L)), Ze = b.get(he);
          Ze && (_t(L, he), L.position = {
            ...L.position || {},
            x: Ze.x,
            y: Ze.y
          }, R && R(L, Ze));
        };
        for (const L of xe.value)
          ge(L);
        for (const L of le.value)
          ge(L);
        for (const L of fe.value)
          ge(L);
        for (const L of te.value)
          ge(L, (R, he) => {
            R.branchVectorList = re(he);
          });
        De.value = new Set([...De.value].map((L) => I(L)).filter((L) => b.has(L))), ye.value = new Set([...ye.value].filter((L) => !C.has(L))), yn((L) => L.kind === "link" && C.has(L.id)), Ln();
        for (const L of Ae.value)
          L.nodeID = I(L.nodeID);
        xl({
          lineIds: new Set(Y.value.map((L) => L.id)),
          nodeIds: new Set(O.value.map((L) => L.id))
        }), l();
      });
      const d = e ? yl(t) : null;
      return vl(d), d;
    }
    function E() {
      const e = Ft === 0, t = e ? ml() : null;
      Ce(() => {
        const i = fl(), a = {}, s = (p, I) => {
          !Number.isFinite(Number(I.x)) || !Number.isFinite(Number(I.y)) || gl(p, I) && (Pi(p, I) || (a[p.id] || (a[p.id] = { line: p, pointList: [] }), a[p.id].pointList.some((b) => ct(b, I)) || a[p.id].pointList.push({ x: I.x, y: I.y })));
        }, d = (p, I) => {
          if (gl(I, p)) {
            s(I, p);
            return;
          }
          const b = li(I, p);
          b.dist < at.value && s(I, { x: b.x, y: b.y });
        };
        for (let p = 0; p < i.length; p += 1)
          for (let I = p + 1; I < i.length; I += 1) {
            const b = i[p], C = i[I], B = Yi(b, C);
            B && gl(b, B) && gl(C, B) && (s(b, B), s(C, B)), d({ x: Number(b.x1), y: Number(b.y1) }, C), d({ x: Number(b.x2), y: Number(b.y2) }, C), d({ x: Number(C.x1), y: Number(C.y1) }, b), d({ x: Number(C.x2), y: Number(C.y2) }, b);
          }
        const f = [...Y.value.filter((p) => !a[p.id])];
        for (const p of Object.keys(a)) {
          const I = a[p].line, b = a[p].pointList;
          b.push({ x: I.x1, y: I.y1 }), b.push({ x: I.x2, y: I.y2 });
          for (const C of b)
            C.positionRate = Jo(I, C);
          b.sort((C, B) => C.positionRate < B.positionRate ? -1 : 1);
          for (let C = 0; C < b.length - 1; C += 1) {
            const B = b[C], T = b[C + 1];
            ct(B, T) || f.push({
              ...I,
              id: `${I.id}s${C}`,
              x1: B.x,
              y1: B.y,
              x2: T.x,
              y2: T.y,
              fromNodeID: "",
              toNodeID: ""
            });
          }
        }
        Y.value = f, l();
      });
      const n = e ? yl(t) : null;
      return vl(n), n;
    }
    function Q() {
      Ue.value.visible = !0;
    }
    function Se(e, t) {
      Ue.value.visible && (Ue.value.x = e, Ue.value.y = t);
    }
    function ae(e, t) {
      const n = qt(e, t);
      n && Ce(() => {
        const i = ut();
        xe.value.push({
          id: i,
          name: i,
          type: Sa(Ue.value.type),
          position: { x: m(n.x), y: m(n.y) },
          direction: Ue.value.direction,
          bindingNodeID: n.id
        });
      });
    }
    function ze() {
      yt.value.visible = !0;
    }
    function wt(e, t) {
      yt.value.visible && (yt.value.x = e, yt.value.y = t);
    }
    function ii(e, t) {
      const n = qt(e, t);
      n && Ce(() => {
        le.value.push({
          id: ut(),
          type: "normal",
          position: { x: e, y: t },
          bindingNodeID: n.id
        });
      });
    }
    function jt() {
      Ve.value.visible = !0;
    }
    function oi(e, t) {
      Ve.value.visible && (Ve.value.x = e, Ve.value.y = t);
    }
    function Ie(e, t) {
      const n = qt(e, t);
      n && Ce(() => {
        fe.value.push({
          id: ut(),
          direction: no(Ve.value.direction),
          type: so(Ve.value.type),
          position: { x: e, y: t },
          bindingNodeID: n.id
        });
      });
    }
    function Pe(e, t) {
      const n = qt(e, t);
      n && Da(n);
    }
    function Ke() {
      ht.value.visible = !0;
    }
    function pl(e, t, n, i, a, s) {
      if (t === i) return { x: a, y: t };
      if (e === n) return { x: e, y: s };
      const d = t - i, f = -(e - n), p = -n * (t - i) + i * (e - n), I = (f * f * a - d * f * s - d * p) / (d * d + f * f), b = (d * d * s - d * f * a - f * p) / (d * d + f * f);
      return { x: I, y: b };
    }
    function Wn(e, t, n, i, a, s) {
      const d = pl(e, t, n, i, a, s);
      return d.x < Math.min(e, n) || d.x > Math.max(e, n) || d.y < Math.min(t, i) || d.y > Math.max(t, i) || gt(d, { x: a, y: s }) > 30 ? null : d;
    }
    function kn(e, t) {
      let n = null;
      for (const i of Y.value) {
        const a = Wn(Number(i.x1), Number(i.y1), Number(i.x2), Number(i.y2), e, t);
        if (a) {
          n = a;
          break;
        }
      }
      return Nt.value = n, n;
    }
    function ai(e, t) {
      ht.value.visible && (ht.value.x = e, ht.value.y = t);
    }
    function Oi(e, t) {
      if (qt(e, t)) return;
      const n = fl();
      let i = null, a = null, s = Number.MAX_SAFE_INTEGER;
      for (const d of n) {
        const f = Wn(Number(d.x1), Number(d.y1), Number(d.x2), Number(d.y2), e, t);
        if (!f) continue;
        const p = gt(f, { x: e, y: t });
        p < s && (s = p, i = d, a = f);
      }
      !a || !i || Ce(() => {
        const d = Je(a.x), f = Je(a.y), p = { id: ut(), x: d, y: f, adjacentLineIDList: [`${i.id}s1`, `${i.id}s2`] }, I = {
          id: `${i.id}s1`,
          x1: i.x1,
          y1: i.y1,
          x2: d,
          y2: f,
          fromNodeID: i.fromNodeID,
          toNodeID: p.id
        }, b = {
          id: `${i.id}s2`,
          x1: d,
          y1: f,
          x2: i.x2,
          y2: i.y2,
          fromNodeID: p.id,
          toNodeID: i.toNodeID
        };
        Y.value = Y.value.filter((C) => C.id !== i.id), Y.value.push(I, b), O.value.push(p);
      });
    }
    function hl() {
      const e = Ft === 0, t = e ? ml() : null;
      Ce(() => {
        const i = new Map(O.value.map((p) => [p.id, { ...p }])), a = O.value.map((p) => ({ ...p }));
        O.value = [];
        const s = [], d = (p, I) => {
          const b = { x: Number(p), y: Number(I) };
          return a.find((C) => ct(C, b)) || null;
        }, f = (p, I) => {
          const b = s.find((T) => ct(T, { x: p, y: I }));
          if (b) return b;
          const C = d(p, I), B = {
            ...C || {},
            id: String(C?.id ?? "").trim() || ut(),
            x: Number(p),
            y: Number(I),
            adjacentLineIDList: []
          };
          return s.push(B), B;
        };
        for (const p of Y.value) {
          const I = f(p.x1, p.y1), b = f(p.x2, p.y2);
          p.fromNodeID = I.id, p.toNodeID = b.id, I.adjacentLineIDList.push(p.id), b.adjacentLineIDList.push(p.id);
        }
        O.value = s;
        for (const p of Ae.value) {
          const I = i.get(p.nodeID);
          if (!I) continue;
          const b = s.find((C) => ct(C, I));
          b && (p.nodeID = b.id);
        }
        xl({
          lineIds: new Set(Y.value.map((p) => p.id)),
          nodeIds: new Set(O.value.map((p) => p.id))
        });
      });
      const n = e ? yl(t) : null;
      return vl(n), n;
    }
    function re(e) {
      const t = new Set(Nn(e)), n = Y.value.filter((s) => t.has(String(s.id))), i = [], a = String(e?.id ?? "");
      for (const s of n)
        String(s.fromNodeID ?? "") === a ? i.push({ x: Number(s.x2) - Number(s.x1), y: Number(s.y2) - Number(s.y1), lineID: s.id }) : String(s.toNodeID ?? "") === a && i.push({ x: Number(s.x1) - Number(s.x2), y: Number(s.y1) - Number(s.y2), lineID: s.id });
      return i;
    }
    function Nn(e) {
      return Array.isArray(e?.adjacentLineIDList) ? e.adjacentLineIDList.map((t) => String(t)).filter((t) => t !== "") : [];
    }
    function qo(e) {
      const t = Nn(e).length;
      return t === 3 || t === 4;
    }
    function si(e) {
      let t = 0, n = 0, i = !1;
      for (let s = 0; s < e.length; s += 1)
        for (let d = s + 1; d < e.length; d += 1) {
          const f = e[s].x * e[d].x + e[s].y * e[d].y, p = Math.hypot(e[s].x, e[s].y), I = Math.hypot(e[d].x, e[d].y);
          if (p === 0 || I === 0) continue;
          const b = La(f / (p * I));
          Math.abs(b + 1) < 0.01 && (i = !0), b > 0 && b < 1 ? t += 1 : b < 0 && b >= -1 && (n += 1);
        }
      let a = "unknown";
      return t === 1 && n === 2 ? a = i ? "single" : "symmetrical" : t === 2 && (a = "slip"), a;
    }
    function u(e) {
      if (!qo(e)) return null;
      const t = re(e);
      return {
        type: si(t),
        branchVectorList: t
      };
    }
    function ue(e, t = u(e), n = {}) {
      if (!t) return null;
      const i = String(n.id ?? "").trim() || ut(), a = n.name == null ? i : String(n.name).trim();
      return {
        id: i,
        name: a || i,
        type: t.type,
        position: { x: e.x, y: e.y },
        bindingNodeID: e.id,
        branchVectorList: t.branchVectorList
      };
    }
    function Yt(e) {
      return String(e ?? "").trim().toLowerCase();
    }
    function ri(e, t) {
      return qe(e) === String(t?.id ?? "");
    }
    function wa(e, t) {
      const n = Nn(t), i = new Set(n), s = (Array.isArray(e?.branchVectorList) ? e.branchVectorList : []).map((f) => String(f?.lineID ?? "")).filter((f) => f !== ""), d = new Set(s);
      if (i.size !== n.length || d.size !== i.size || s.length !== n.length) return !1;
      for (const f of i)
        if (!d.has(f)) return !1;
      return !0;
    }
    function Hs(e, t, n) {
      return n ? ri(e, t) && Yt(e?.type) === Yt(n.type) && wa(e, t) : !1;
    }
    function Ks(e, t) {
      return te.value.find((n) => Hs(n, e, t)) || null;
    }
    function Ia() {
      const e = new Set(te.value.map((t) => t.id));
      W.value = new Set([...W.value].filter((t) => e.has(t))), yn((t) => t.kind === "switch" && !e.has(t.id));
    }
    function ka(e, t, n = []) {
      const i = n[0] || null, a = i ? (Array.isArray(i.branchVectorList) ? i.branchVectorList : []).map((s) => String(s?.lineID ?? "")).filter((s) => s !== "") : [];
      return {
        nodeId: String(e.id),
        nodeName: String(e.name || e.id || ""),
        switchId: String(i?.id || ""),
        switchName: String(i?.name || i?.id || ""),
        existingSwitchIds: n.map((s) => String(s.id || "")).filter((s) => s !== ""),
        previousLineIds: a,
        nextLineIds: Nn(e),
        candidate: t
      };
    }
    function Na() {
      const e = [], t = [];
      for (const n of O.value) {
        const i = u(n);
        if (!i) continue;
        const a = te.value.filter((d) => ri(d, n));
        if (a.length === 0) {
          e.push(ka(n, i));
          continue;
        }
        const s = a[0];
        wa(s, n) || t.push(ka(n, i, a));
      }
      return {
        createItems: e,
        reconstructItems: t,
        createCount: e.length,
        reconstructCount: t.length,
        requiresConfirmation: t.length > 0
      };
    }
    function Zs(e) {
      const t = new Set((e?.reconstructItems || []).map((i) => String(i.nodeId))), n = te.value.filter((i) => !t.has(qe(i)));
      for (const i of e?.reconstructItems || []) {
        const a = Dn(i.nodeId);
        if (!a) continue;
        const s = u(a);
        if (!s) continue;
        const d = ue(a, s, {
          id: i.switchId,
          name: i.switchName
        });
        d && n.push(d);
      }
      for (const i of e?.createItems || []) {
        const a = Dn(i.nodeId);
        if (!a || te.value.some((f) => ri(f, a))) continue;
        const s = u(a), d = ue(a, s);
        d && n.push(d);
      }
      te.value = n;
    }
    function Da(e) {
      const t = u(e);
      if (!t) return;
      const n = Ks(e, t), i = te.value.filter((s) => ri(s, e)), a = i[0] || null;
      n && i.length === 1 || (Ce(() => {
        te.value = te.value.filter((s) => !ri(s, e)), te.value.push(n || ue(e, t, {
          id: a?.id,
          name: a?.name
        })), Ia();
      }), lt());
    }
    function Qs(e = {}) {
      const t = e.plan || Na();
      return t.requiresConfirmation && e.confirmed !== !0 || t.createCount === 0 && t.reconstructCount === 0 ? t : (Ce(() => {
        Zs(t), Ia();
      }), lt(), {
        ...t,
        applied: !0
      });
    }
    function La(e) {
      return Math.max(-1, Math.min(1, e));
    }
    function Ht(e) {
      return Math.round(m(e) * 1e3) / 1e3;
    }
    function Ca(e, t) {
      return e.fromNodeID === t.id ? {
        x: m(e.x2) - m(e.x1),
        y: m(e.y2) - m(e.y1)
      } : e.toNodeID === t.id ? {
        x: m(e.x1) - m(e.x2),
        y: m(e.y1) - m(e.y2)
      } : ct(t, { x: e.x1, y: e.y1 }) ? {
        x: m(e.x2) - m(e.x1),
        y: m(e.y2) - m(e.y1)
      } : ct(t, { x: e.x2, y: e.y2 }) ? {
        x: m(e.x1) - m(e.x2),
        y: m(e.y1) - m(e.y2)
      } : null;
    }
    function Ma(e, t, n, i = Nl, a = ut()) {
      const s = Ca(t, e), d = Ca(n, e);
      if (!s || !d) return null;
      const f = Math.hypot(s.x, s.y), p = Math.hypot(d.x, d.y);
      if (f <= 0 || p <= 0) return null;
      const I = { x: s.x / f, y: s.y / f }, b = { x: d.x / p, y: d.y / p }, C = La(I.x * b.x + I.y * b.y), B = Math.acos(C), T = B * 180 / Math.PI;
      if (T <= Nd || T >= Dd) return null;
      const ge = Math.tan(B / 2);
      if (!Number.isFinite(ge) || ge <= 0) return null;
      let L = m(i) || Nl;
      if (L <= 0) return null;
      let R = L / ge;
      if (!Number.isFinite(R) || R <= 0) return null;
      const he = Math.min(f, p);
      if (R > he && (L = he * ge * Ld, R = L / ge, !Number.isFinite(L) || L <= 0 || !Number.isFinite(R) || R <= 0) || R > f || R > p) return null;
      const Ze = {
        x: I.x + b.x,
        y: I.y + b.y
      }, Gn = Math.hypot(Ze.x, Ze.y);
      if (Gn <= 0) return null;
      const dt = L / Math.sin(B / 2), ft = { x: m(e.x), y: m(e.y) }, wl = {
        x: ft.x + I.x * R,
        y: ft.y + I.y * R
      }, Il = {
        x: ft.x + b.x * R,
        y: ft.y + b.y * R
      }, Zt = {
        x: ft.x + Ze.x / Gn * dt,
        y: ft.y + Ze.y / Gn * dt
      }, Ms = { x: wl.x - Zt.x, y: wl.y - Zt.y }, _s = { x: Il.x - Zt.x, y: Il.y - Zt.y }, vu = Ms.x * _s.y - Ms.y * _s.x >= 0 ? 1 : 0;
      return Bl({
        id: a,
        nodeID: e.id,
        tangentLinkID1: t.id,
        tangentLinkID2: n.id,
        radius: Ht(L),
        angle: Ht(T),
        tangentDistance: Ht(R),
        start: {
          x: Ht(wl.x),
          y: Ht(wl.y)
        },
        end: {
          x: Ht(Il.x),
          y: Ht(Il.y)
        },
        center: {
          x: Ht(Zt.x),
          y: Ht(Zt.y)
        },
        largeArcFlag: 0,
        sweepFlag: vu
      });
    }
    function er() {
      return Ce(() => {
        Ho();
        const e = new Map(Y.value.map((n) => [n.id, n])), t = [];
        for (const n of O.value) {
          const i = Array.isArray(n.adjacentLineIDList) ? n.adjacentLineIDList : [];
          if (i.length !== 2) continue;
          const a = e.get(i[0]), s = e.get(i[1]);
          if (!a || !s) continue;
          const d = Ma(n, a, s, Nl);
          d && t.push(d);
        }
        Ae.value = t;
      }), Ae.value.length;
    }
    function _a() {
      Te.value = null;
    }
    function tr(e, t) {
      Te.value && (Te.value.endX = e, Te.value.endY = t);
    }
    function nr(e, t) {
      if (!Te.value) {
        Te.value = { startX: e, startY: t, endX: e, endY: t };
        return;
      }
      Ce(() => {
        const n = ut(), i = Te.value.startX, a = Te.value.endX, s = Te.value.startY, d = Te.value.endY;
        Be.value.push({
          id: n,
          name: n,
          x: Math.min(i, a),
          y: Math.min(s, d),
          width: Math.abs(a - i),
          height: Math.abs(d - s)
        });
      }), Te.value = null;
    }
    function lr(e, t) {
      Ce(() => {
        const n = fn(e, t);
        Ee.value.push(n), Ii([...oe.value, n.id]);
      });
    }
    const Wi = A(() => {
      if (!Te.value)
        return { x: 0, y: 0, width: 0, height: 0 };
      const e = Te.value.startX, t = Te.value.endX, n = Te.value.startY, i = Te.value.endY;
      return {
        x: Math.min(e, t),
        y: Math.min(n, i),
        width: Math.abs(t - e),
        height: Math.abs(i - n)
      };
    });
    function ir() {
      return JSON.stringify({
        metadata: {
          ...z.value,
          latestElementID: rt.value,
          gridSettings: fo()
        },
        tracks: Y.value,
        curves: Ae.value,
        nodes: O.value,
        signals: xe.value,
        insulationJoints: le.value,
        bufferStops: fe.value,
        platforms: Be.value,
        switches: te.value,
        annotations: Ee.value
      });
    }
    function Va() {
      z.value = {}, Y.value = [], Ae.value = [], O.value = [], xe.value = [], le.value = [], fe.value = [], Be.value = [], te.value = [], Ee.value = [], Zl(), Jt(), Yn(), Fl(), zl();
    }
    function or(e) {
      Ce(() => {
        Va(), z.value = { ...e?.metadata || {} }, rt.value = Number(e?.metadata?.latestElementID || 0), Y.value = (e?.tracks || []).map((t) => ({ name: "", ...t })), Ae.value = (e?.curves || []).map((t) => Bl(t)), O.value = (e?.nodes || []).map((t) => ({ ...t })), xe.value = (e?.signals || []).map((t) => sl(t)), le.value = (e?.insulationJoints || []).map((t) => hn(t)), fe.value = (e?.bufferStops || []).map((t) => Hl(t)), Be.value = (e?.platforms || []).map((t) => al(t)), te.value = (e?.switches || []).map((t) => sl(t)), Ee.value = (e?.annotations || []).map((t) => El(t)), Ho(), Kl(), mo(e?.metadata?.gridSettings) || ko(), zl(), Wl();
      }, { allowReadonly: !0 }), Xe();
    }
    function $a(e, t) {
      D.value === 0 && Xt("link", t, e);
    }
    function Kt(e) {
      return D.value !== 0 ? !1 : (e?.stopPropagation(), !0);
    }
    function ar(e, t) {
      if (y.routePickTarget) {
        e.preventDefault(), e.stopPropagation(), za(t);
        return;
      }
      if (D.value === 1 && M.value === "w") {
        e.preventDefault(), e.stopPropagation();
        const n = Dn(t);
        n && Da(n);
        return;
      }
      Kt(e) && (e.preventDefault(), In(), Xt("node", t, e), y.readonly || wr(e, t));
    }
    function za(e) {
      const t = String(e ?? "").trim();
      return t ? (q("route-node-pick", {
        target: y.routePickTarget,
        nodeId: t
      }), !0) : !1;
    }
    function ui(e, t) {
      if (!y.routePickTarget) return !1;
      const n = qe(t);
      return n ? (e.preventDefault(), e.stopPropagation(), za(n)) : !1;
    }
    function Fa(e, t) {
      ui(e, xe.value.find((n) => n.id === t)) || Kt(e) && Xt("signal", t, e);
    }
    function sr(e, t) {
      ui(e, le.value.find((n) => n.id === t)) || Kt(e) && Xt("insulationJoint", t, e);
    }
    function rr(e, t) {
      ui(e, fe.value.find((n) => n.id === t)) || Kt(e) && Xt("bufferStop", t, e);
    }
    function ur(e, t) {
      ui(e, te.value.find((n) => n.id === t)) || Kt(e) && Xt("switch", t, e);
    }
    function cr(e, t) {
      ui(e, Be.value.find((n) => n.id === t)) || Kt(e) && Xt("platform", t, e);
    }
    function dr(e, t) {
      Kt(e) && Xt("annotation", t, e);
    }
    function jo(e) {
      return Ee.value.find((t) => t.id === e) || null;
    }
    function fr(e, t, n) {
      if (y.readonly) return;
      const a = mn(e).find((s) => s.id === t);
      a && (Ce(() => {
        Aa(e, a, n);
      }), lt());
    }
    function mr(e, t, n) {
      if (y.readonly) return;
      const i = new Set((Array.isArray(t) ? t : []).map((f) => String(f ?? "")));
      if (i.size === 0) return;
      const a = { ...n };
      delete a.id;
      const d = mn(e).filter((f) => i.has(String(f.id)));
      d.length !== 0 && (Ce(() => {
        for (const f of d)
          Aa(e, f, a, { allowIdUpdate: !1 });
      }), lt());
    }
    function Aa(e, t, n, i = {}) {
      const a = i.allowIdUpdate !== !1, s = t.id, d = e === "link" ? {
        fromNodeID: t.fromNodeID,
        toNodeID: t.toNodeID,
        x1: t.x1,
        y1: t.y1,
        x2: t.x2,
        y2: t.y2
      } : null, f = { ...n };
      if (a || delete f.id, f.position && (t.position = {
        ...t.position || {},
        ...f.position
      }, f.position = t.position), Object.assign(t, f), (e === "signal" || e === "switch" || e === "platform") && Object.assign(t, al(t)), e === "bufferStop" && Object.assign(t, Hl(t)), ["signal", "insulationJoint", "bufferStop", "switch"].includes(e)) {
        _t(t, qe(t));
        const p = xn(t);
        e === "switch" && p && (t.branchVectorList = re(p));
      }
      a && e === "link" && f.id != null && f.id !== s ? (vr(s, f.id), Bt(e, s, f.id)) : a && f.id != null && f.id !== s && Bt(e, s, f.id), e === "link" && d && es(t, d);
    }
    function yr(e, t) {
      return Object.prototype.hasOwnProperty.call(e, t);
    }
    function vr(e, t) {
      for (const n of O.value)
        Array.isArray(n.adjacentLineIDList) && (n.adjacentLineIDList = n.adjacentLineIDList.map((i) => i === e ? t : i));
      for (const n of te.value)
        for (const i of n.branchVectorList || [])
          i.lineID === e && (i.lineID = t);
      for (const n of Ae.value)
        n.tangentLinkID1 === e && (n.tangentLinkID1 = t), n.tangentLinkID2 === e && (n.tangentLinkID2 = t);
    }
    function Ea(e, t, n) {
      return yr(e, t) ? Math.abs(m(e[t]) - m(n)) > 1e-6 : !1;
    }
    function Ba(e) {
      for (const t of Y.value)
        t.fromNodeID === e.id && (t.x1 = e.x, t.y1 = e.y), t.toNodeID === e.id && (t.x2 = e.x, t.y2 = e.y);
    }
    function Ra(e) {
      const t = (i) => qe(i) === String(e.id), n = (i) => {
        _t(i, e.id), i.position = {
          ...i.position || {},
          x: m(e.x),
          y: m(e.y)
        };
      };
      for (const i of xe.value)
        t(i) && n(i);
      for (const i of le.value)
        t(i) && n(i);
      for (const i of fe.value)
        t(i) && n(i);
      for (const i of te.value)
        t(i) && (n(i), i.branchVectorList = re(e));
    }
    function Ta(e) {
      if (!e?.position) return null;
      const t = Number(e.position.x), n = Number(e.position.y);
      return !Number.isFinite(t) || !Number.isFinite(n) ? null : { x: t, y: n };
    }
    function gr(e) {
      return e === "signal" ? xe.value : e === "insulationJoint" ? le.value : e === "bufferStop" ? fe.value : e === "switch" ? te.value : [];
    }
    function pr(e) {
      return e === "signal" ? "信号机" : e === "insulationJoint" ? "钢轨绝缘" : e === "bufferStop" ? "车挡" : e === "switch" ? "道岔" : "设备";
    }
    function Xa() {
      if (y.readonly)
        return { totalCount: 0, items: [], alreadyCorrectCount: 0, unmatchedCount: 0 };
      const e = [];
      let t = 0, n = 0, i = 0;
      const a = (s, d) => {
        t += 1;
        const f = Ta(d);
        if (!f) {
          i += 1;
          return;
        }
        const p = qt(f.x, f.y);
        if (!p) {
          i += 1;
          return;
        }
        const I = qe(d), b = String(p.id ?? "").trim();
        if (I === b) {
          n += 1;
          return;
        }
        const C = pr(s), B = String(d?.id ?? ""), T = Sn(d, C);
        e.push({
          key: `${s}:${B}:${I}:${b}`,
          kind: s,
          kindLabel: C,
          equipmentId: B,
          equipmentName: T,
          previousBindingNodeID: I,
          nextBindingNodeID: b,
          nodeName: String(p.name || p.id || ""),
          position: {
            x: Je(f.x),
            y: Je(f.y)
          }
        });
      };
      return xe.value.forEach((s) => a("signal", s)), le.value.forEach((s) => a("insulationJoint", s)), fe.value.forEach((s) => a("bufferStop", s)), te.value.forEach((s) => a("switch", s)), {
        totalCount: t,
        items: e,
        alreadyCorrectCount: n,
        unmatchedCount: i
      };
    }
    function Ya(e = []) {
      if (y.readonly)
        return { requestedCount: 0, fixedCount: 0, alreadyCorrectCount: 0, unmatchedCount: 0 };
      const t = Array.isArray(e) ? e : [], n = [];
      let i = 0, a = 0;
      for (const s of t) {
        const d = String(s?.kind || ""), f = String(s?.equipmentId ?? ""), p = gr(d).find((ge) => String(ge?.id ?? "") === f);
        if (!p) {
          a += 1;
          continue;
        }
        const I = Ta(p);
        if (!I) {
          a += 1;
          continue;
        }
        const b = qt(I.x, I.y);
        if (!b) {
          a += 1;
          continue;
        }
        const C = qe(p), B = String(s?.nextBindingNodeID ?? "").trim(), T = String(b.id ?? "").trim();
        if (B && B !== T) {
          a += 1;
          continue;
        }
        if (C === T) {
          i += 1;
          continue;
        }
        n.push({
          kind: d,
          equipment: p,
          node: b,
          previousBindingNodeID: C,
          nextBindingNodeID: T
        });
      }
      return n.length > 0 && (Ce(() => {
        for (const s of n)
          _t(s.equipment, s.nextBindingNodeID), s.kind === "switch" && (s.equipment.branchVectorList = re(s.node));
      }), lt()), {
        requestedCount: t.length,
        fixedCount: n.length,
        alreadyCorrectCount: i,
        unmatchedCount: a,
        fixedEquipment: n.map((s) => ({
          kind: s.kind,
          id: s.equipment.id,
          previousBindingNodeID: s.previousBindingNodeID,
          nextBindingNodeID: s.nextBindingNodeID
        }))
      };
    }
    function hr() {
      const e = Xa();
      return {
        ...e,
        ...Ya(e.items)
      };
    }
    function xl({ lineIds: e = /* @__PURE__ */ new Set(), nodeIds: t = /* @__PURE__ */ new Set() } = {}) {
      if (Ae.value.length === 0 || e.size === 0 && t.size === 0) return;
      const n = new Map(O.value.map((a) => [a.id, a])), i = new Map(Y.value.map((a) => [a.id, a]));
      Ae.value = Ae.value.map((a) => {
        if (!(t.has(a.nodeID) || e.has(a.tangentLinkID1) || e.has(a.tangentLinkID2))) return a;
        const d = n.get(a.nodeID), f = i.get(a.tangentLinkID1), p = i.get(a.tangentLinkID2);
        return !d || !f || !p ? a : Ma(
          d,
          f,
          p,
          m(a.radius) || Nl,
          a.id
        ) || a;
      });
    }
    function xr(e) {
      const t = /* @__PURE__ */ new Set();
      for (const n of Y.value)
        (n.fromNodeID === e.id || n.toNodeID === e.id) && t.add(n.id);
      xl({
        lineIds: t,
        nodeIds: /* @__PURE__ */ new Set([e.id])
      });
    }
    function Dn(e) {
      const t = String(e ?? "");
      return O.value.find((n) => String(n.id ?? "") === t) || null;
    }
    function Sr() {
      const e = Zn.value;
      !e || e.undoCaptured || (Qe.value.push(pn()), Qe.value.length > 30 && Qe.value.shift(), Dt.value = [], e.undoCaptured = !0);
    }
    function br() {
      window.addEventListener("mousemove", Wa), window.addEventListener("mouseup", Ga);
    }
    function Pa() {
      window.removeEventListener("mousemove", Wa), window.removeEventListener("mouseup", Ga);
    }
    function wr(e, t) {
      if (y.readonly) return;
      Sl();
      const n = Dn(t);
      if (!n) return;
      const i = $t(e.clientX, e.clientY);
      i && (Zn.value = {
        nodeId: t,
        startNode: {
          x: m(n.x),
          y: m(n.y)
        },
        startPointer: i,
        undoCaptured: !1
      }, br());
    }
    function Oa(e) {
      const t = Zn.value;
      if (!t) return;
      const n = Dn(t.nodeId);
      if (!n) {
        Sl();
        return;
      }
      const i = $t(e.clientX, e.clientY);
      if (!i) return;
      const a = i.x - t.startPointer.x, s = i.y - t.startPointer.y, d = $i({
        x: t.startNode.x + a,
        y: t.startNode.y + s
      }), f = Je(d.x), p = Je(d.y);
      f === m(n.x) && p === m(n.y) || (Sr(), n.x = f, n.y = p, Ba(n), Ra(n), xr(n), Fn(n, { triggerMargin: oo }));
    }
    function Sl() {
      Zn.value = null, Pa();
    }
    function Wa(e) {
      e.preventDefault(), Oa(e);
    }
    function Ga(e) {
      e.preventDefault(), Oa(e), Sl();
    }
    function Ho() {
      const e = new Map(O.value.map((t) => [t.id, /* @__PURE__ */ new Set()]));
      for (const t of Y.value) {
        const n = e.get(t.fromNodeID), i = e.get(t.toNodeID);
        n && n.add(t.id), i && i.add(t.id);
      }
      for (const t of O.value)
        t.adjacentLineIDList = [...e.get(t.id) || []];
    }
    function Ua(e, t, n) {
      const { nodeIDKey: i, xKey: a, yKey: s } = n, d = O.value.find((I) => I.id === e[i]);
      if (!d) return;
      const f = e[i] !== t[i];
      if (Ea(e, a, t[a]) || Ea(e, s, t[s])) {
        d.x = m(e[a]), d.y = m(e[s]), Ba(d);
        return;
      }
      f && (e[a] = m(d.x), e[s] = m(d.y));
    }
    function Ir(e, t) {
      const n = e.fromNodeID !== t.fromNodeID, i = e.toNodeID !== t.toNodeID;
      Ua(e, t, {
        nodeIDKey: "fromNodeID",
        xKey: "x1",
        yKey: "y1"
      }), Ua(e, t, {
        nodeIDKey: "toNodeID",
        xKey: "x2",
        yKey: "y2"
      }), (n || i) && Ho();
    }
    function kr(e) {
      return {
        position: {
          x: m(e.position?.x),
          y: m(e.position?.y)
        }
      };
    }
    function Nr() {
      const e = Qn.value;
      !e || e.undoCaptured || (Qe.value.push(pn()), Qe.value.length > 30 && Qe.value.shift(), Dt.value = [], e.undoCaptured = !0);
    }
    function Dr() {
      window.addEventListener("mousemove", ja), window.addEventListener("mouseup", Ha);
    }
    function Ja() {
      window.removeEventListener("mousemove", ja), window.removeEventListener("mouseup", Ha);
    }
    function Lr(e, t) {
      if (y.readonly || !Kt(e)) return;
      e.preventDefault(), In();
      const n = jo(t);
      if (!n) return;
      Ui(t) || Ii([...oe.value, t]);
      const i = kr(n), a = $t(e.clientX, e.clientY);
      a && (Qn.value = {
        annotationId: t,
        startState: i,
        startPointer: a,
        undoCaptured: !1
      }, Dr());
    }
    function Cr(e, t, n) {
      const i = n.x - t.startPointer.x, a = n.y - t.startPointer.y;
      Nr(), e.position = {
        x: Je(t.startState.position.x + i),
        y: Je(t.startState.position.y + a)
      }, Fn(e.position, { triggerMargin: oo }), Xe();
    }
    function qa(e) {
      const t = Qn.value;
      if (!t) return;
      const n = jo(t.annotationId);
      if (!n) {
        bl();
        return;
      }
      const i = $t(e.clientX, e.clientY);
      i && Cr(n, t, i);
    }
    function bl() {
      Qn.value = null, Ja();
    }
    function ja(e) {
      e.preventDefault(), qa(e);
    }
    function Ha(e) {
      e.preventDefault(), qa(e), bl();
    }
    function Ka(e) {
      return {
        fromNodeID: e.fromNodeID,
        toNodeID: e.toNodeID,
        x1: e.x1,
        y1: e.y1,
        x2: e.x2,
        y2: e.y2
      };
    }
    function Za(e) {
      return Y.value.find((t) => t.id === e) || null;
    }
    function Mr() {
      const e = Kn.value;
      !e || e.undoCaptured || (Qe.value.push(pn()), Qe.value.length > 30 && Qe.value.shift(), Dt.value = [], e.undoCaptured = !0);
    }
    function _r() {
      window.addEventListener("mousemove", ns), window.addEventListener("mouseup", ls);
    }
    function Qa() {
      window.removeEventListener("mousemove", ns), window.removeEventListener("mouseup", ls);
    }
    function Vr(e, t) {
      if (y.readonly || !Kt(e)) return;
      e.preventDefault(), Ln(), In(), bl();
      const n = Za(t.lineId), i = $t(e.clientX, e.clientY);
      !n || !i || (Kn.value = {
        lineId: t.lineId,
        type: t.type,
        startPointer: i,
        startLine: Ka(n),
        undoCaptured: !1
      }, _r());
    }
    function $r(e, t) {
      const n = t.x - e.startPointer.x, i = t.y - e.startPointer.y, a = e.type === "sp" ? e.startLine.x1 : e.startLine.x2, s = e.type === "sp" ? e.startLine.y1 : e.startLine.y2, d = $i({
        x: m(a) + n,
        y: m(s) + i
      });
      return {
        x: Je(d.x),
        y: Je(d.y)
      };
    }
    function es(e, t) {
      Ir(e, t);
      const n = new Set([
        t.fromNodeID,
        t.toNodeID,
        e.fromNodeID,
        e.toNodeID
      ].filter((i) => i != null && i !== ""));
      for (const i of n) {
        const a = Dn(i);
        a && Ra(a);
      }
      xl({
        lineIds: /* @__PURE__ */ new Set([e.id]),
        nodeIds: n
      });
    }
    function ts(e) {
      const t = Kn.value;
      if (!t) return;
      const n = Za(t.lineId);
      if (!n) {
        Ln();
        return;
      }
      const i = $t(e.clientX, e.clientY);
      if (!i) return;
      const a = $r(t, i), s = t.type === "sp" ? "x1" : "x2", d = t.type === "sp" ? "y1" : "y2";
      if (a.x === m(n[s]) && a.y === m(n[d])) return;
      Mr();
      const f = Ka(n);
      n[s] = a.x, n[d] = a.y, es(n, f), Fn(a, { triggerMargin: oo }), lt();
    }
    function Ln() {
      Kn.value = null, Qa();
    }
    function ns(e) {
      e.preventDefault(), ts(e);
    }
    function ls(e) {
      e.preventDefault(), ts(e), Ln();
    }
    function zr(e) {
      rl(e.clientX, e.clientY), Z.value?.focus({ preventScroll: !0 });
      const t = ee.value.x, n = ee.value.y;
      if (He.value) {
        dl(e.clientX, e.clientY);
        return;
      }
      D.value !== 0 && (M.value === "l" ? Ri(t, n) : M.value === "s" ? Se(t, n) : M.value === "i" ? wt(t, n) : M.value === "e" ? oi(t, n) : M.value === "n" ? ai(t, n) : M.value === "p" && tr(t, n));
    }
    function Fr(e) {
      rl(e.clientX, e.clientY);
      const t = ee.value.x, n = ee.value.y;
      if (D.value === 0) {
        e.button === 0 && (e.preventDefault(), Rt(), To(e));
        return;
      }
      M.value === "l" ? Re.value ? Yo() : Xo(t, n) : M.value === "s" ? ae(t, n) : M.value === "i" ? ii(t, n) : M.value === "e" ? Ie(t, n) : M.value === "w" ? Pe(t, n) : M.value === "n" ? Oi(t, n) : M.value === "p" ? nr(t, n) : M.value === "a" && lr(t, n);
    }
    function Ar(e) {
      He.value && (dl(e.clientX, e.clientY), Tt());
    }
    function Er(e) {
      if (e.key === "Delete" && (e.preventDefault(), q("delete-selection-request")), e.key === "Escape" && (In(), Sl(), bl(), Yn(), Zl(), Jt()), e.ctrlKey && e.key === "z" && (e.preventDefault(), Vi()), e.ctrlKey && e.key === "y" && (e.preventDefault(), jl()), D.value === 1 && M.value === "s" && ["w", "e", "s", "d"].includes(e.key) && (Ue.value.direction = e.key, Se(ee.value.x, ee.value.y)), D.value === 1 && M.value === "e") {
        const n = {
          ArrowLeft: "left",
          ArrowRight: "right",
          l: "left",
          r: "right"
        }[e.key];
        n && (e.preventDefault(), Ve.value.direction = n, oi(ee.value.x, ee.value.y));
      }
    }
    function is(e) {
      return We.value.has(e);
    }
    function Ko(e) {
      return is(e) || $e("signal", e);
    }
    function Zo(e) {
      return ye.value.has(e);
    }
    function Gi(e) {
      return Zo(e) || $e("link", e);
    }
    function os(e) {
      return De.value.has(e);
    }
    function as(e) {
      return os(e) || $e("node", e);
    }
    function ss(e, t) {
      const n = String(e ?? ""), i = String(t ?? "");
      return n < i ? `${n}|${i}` : `${i}|${n}`;
    }
    function rs(e) {
      const t = new Map(O.value.map((n) => [String(n.id ?? ""), n]));
      return (Array.isArray(e) ? e : []).map((n) => t.get(String(n))).filter((n) => n != null);
    }
    function Br(e) {
      const t = String(e?.tangentLinkID1 ?? ""), n = String(e?.tangentLinkID2 ?? "");
      if (!t || !n) return !1;
      const i = String(e?.nodeID ?? "");
      return i && !nn.value.has(i) ? !1 : ln.value.has(ss(t, n));
    }
    function us(e) {
      return Ge.value.has(e);
    }
    function Rr(e) {
      return us(e) || $e("insulationJoint", e);
    }
    function cs(e) {
      return _e.value.has(e);
    }
    function ds(e) {
      return cs(e) || $e("bufferStop", e);
    }
    function fs(e) {
      return W.value.has(e);
    }
    function Qo(e) {
      return fs(e) || $e("switch", e);
    }
    function ea(e) {
      return se.value.has(e);
    }
    function ta(e) {
      return ea(e) || $e("platform", e);
    }
    function Ui(e) {
      return oe.value.has(e);
    }
    function ms(e) {
      return Ui(e) || $e("annotation", e);
    }
    function Ji(e) {
      const t = {
        e: { coefScaleX: 1, coefShiftY: 1, horizontalSide: "right", verticalSide: "top" },
        w: { coefScaleX: -1, coefShiftY: 1, horizontalSide: "left", verticalSide: "top" },
        s: { coefScaleX: -1, coefShiftY: 0, horizontalSide: "left", verticalSide: "bottom" },
        d: { coefScaleX: 1, coefShiftY: 0, horizontalSide: "right", verticalSide: "bottom" }
      };
      return t[e.direction || "e"] || t.e;
    }
    function na(e, t) {
      const n = Number(e?.[t]);
      return Number.isFinite(n) && n > 0 ? n : 0;
    }
    function ys(e) {
      const t = na(e, "width"), n = na(e, "height"), i = e?.bounds || {}, a = Number(i.minX), s = Number(i.minY), d = Number(i.maxX), f = Number(i.maxY);
      return [a, s, d, f].every(Number.isFinite) ? {
        minX: a,
        minY: s,
        maxX: d,
        maxY: f,
        width: Math.abs(d - a),
        height: Math.abs(f - s)
      } : {
        minX: 0,
        minY: 0,
        maxX: t,
        maxY: n,
        width: t,
        height: n
      };
    }
    function vs() {
      return Me.value.node.radius + Md;
    }
    function qi() {
      return 0;
    }
    function ci(e) {
      return e.verticalSide === "top" ? -vs() : vs();
    }
    function gs(e) {
      const t = Ji(e), n = Me.value.signal.scale, i = ji(e);
      if (i.placement === "quadrant") {
        const s = ys(i), d = na(i, "width"), f = t.horizontalSide === "right" ? -1 : 1, p = U(e.position.x) - f * d * n + qi(), I = t.verticalSide === "top" ? G(e.position.y) - s.maxY * n + ci(t) : G(e.position.y) - s.minY * n + ci(t);
        return `translate(${p},${I})scale(${n * f},${n})`;
      }
      return `translate(${U(e.position.x) - n * t.coefScaleX + qi()},${G(e.position.y) - 40 * n * t.coefShiftY + ci(t)})scale(${n * t.coefScaleX},${n})`;
    }
    function Tr(e) {
      return e?.type ?? e?.SignalType ?? e?.signalType ?? mi;
    }
    function ji(e) {
      return Sc(Tr(e));
    }
    function ps(e) {
      return ji(e).className;
    }
    function hs(e) {
      return ji(e).elements;
    }
    function xs(e) {
      const t = Ji(e), n = Me.value.signal.scale, i = ji(e), a = ys(i);
      if (i.placement === "quadrant") {
        const C = a.width * n, B = a.height * n, T = U(e.position.x) + qi(), ge = G(e.position.y) + ci(t), L = t.horizontalSide === "right" ? T : T - C, R = t.horizontalSide === "right" ? T + C : T, he = t.verticalSide === "top" ? ge - B : ge, Ze = t.verticalSide === "top" ? ge : ge + B;
        return { left: L, right: R, top: he, bottom: Ze };
      }
      const s = U(e.position.x) - n * t.coefScaleX + qi(), d = G(e.position.y) - 40 * n * t.coefShiftY + ci(t), f = s + a.minX * n * t.coefScaleX, p = s + a.maxX * n * t.coefScaleX, I = d + a.minY * n, b = d + a.maxY * n;
      return {
        left: Math.min(f, p),
        right: Math.max(f, p),
        top: Math.min(I, b),
        bottom: Math.max(I, b)
      };
    }
    function Xr(e) {
      const t = Ji(e), n = xs(e);
      return t.horizontalSide === "right" ? n.left - Xs : n.right + Xs;
    }
    function Yr(e) {
      const t = xs(e);
      return (t.top + t.bottom) / 2;
    }
    function Pr(e) {
      return Ji(e).horizontalSide === "right" ? "end" : "start";
    }
    function Or(e) {
      return e?.direction ?? e?.Direction ?? Ll;
    }
    function Wr(e) {
      return e?.type ?? e?.Type ?? e?.style ?? e?.Style ?? en;
    }
    function Hi(e) {
      return Ru(Wr(e));
    }
    function Ss(e) {
      return Hi(e).className;
    }
    function bs(e) {
      return Hi(e).elements;
    }
    function Gr(e) {
      return Hi(e).width;
    }
    function ws(e) {
      return Hi(e).height;
    }
    function Is(e) {
      return -ws(e) / 2;
    }
    function ks(e) {
      return `translate(0,${Is(e)})`;
    }
    function Ns() {
      const e = Me.value.track;
      return {
        fill: "none",
        stroke: e.color,
        strokeWidth: e.strokeWidth
      };
    }
    function Ds(e) {
      const n = no(Or(e)) === "left" ? -1 : 1;
      return `translate(${U(e.position.x)},${G(e.position.y)})scale(${n},1)`;
    }
    function Ki(e, t = !1, n = Ys) {
      const i = Me.value[e];
      return {
        fill: t ? n : i.color,
        fontFamily: i.fontFamily,
        fontSize: `${i.fontSize}px`,
        fontWeight: t ? "700" : i.fontWeight,
        fontStyle: i.fontStyle
      };
    }
    function Ur(e) {
      const t = String(e ?? "").trim();
      if (!t || !y.cellLinkMembershipCounts) return 0;
      const n = y.cellLinkMembershipCounts[t], i = Number(n);
      return Number.isFinite(i) && i > 0 ? i : 0;
    }
    function Jr(e) {
      return [e?.fromNodeID, e?.toNodeID].every((t) => String(t ?? "").trim() !== "");
    }
    function qr(e) {
      const t = Me.value.track, n = e?.id, i = Zo(n), a = $e("link", n), s = i || a, d = xt("link", n), f = Jr(e) ? zd : Fd, p = s ? Math.max(t.strokeWidth + 2, t.strokeWidth * 2) : t.strokeWidth;
      if (Vl.value) {
        const I = Ur(n);
        return I > 1 ? {
          stroke: "#f56c6c",
          strokeWidth: Math.max(p, t.strokeWidth + 2),
          strokeDasharray: "none"
        } : I === 1 ? {
          stroke: d || f,
          strokeWidth: p,
          strokeDasharray: "none"
        } : {
          stroke: d || f,
          strokeWidth: p,
          strokeDasharray: "10 7"
        };
      }
      return {
        stroke: d || f,
        strokeWidth: p
      };
    }
    function jr() {
      const e = Me.value.curve;
      return {
        stroke: e.color,
        strokeWidth: e.strokeWidth
      };
    }
    function Hr(e) {
      return e != null && as(e) ? {
        fill: xt("node", e),
        stroke: ho("node", e),
        strokeWidth: 2
      } : {
        fill: Me.value.node.color
      };
    }
    function Ls(e) {
      const t = Me.value.platform, n = e != null && ea(e), i = e != null && $e("platform", e);
      return {
        stroke: (e != null ? xt("platform", e) : "") || t.color,
        strokeWidth: n || i ? t.strokeWidth + 2 : t.strokeWidth
      };
    }
    function Kr(e) {
      const t = Me.value.switch, n = xt("switch", e);
      return n ? {
        stroke: n,
        strokeWidth: t.strokeWidth
      } : {
        stroke: t.color,
        strokeWidth: t.strokeWidth
      };
    }
    function Zi(e) {
      return String(e?.name || "").trim();
    }
    function Zr() {
      return tn || (typeof document > "u" ? null : (tn = document.createElement("canvas").getContext("2d"), tn));
    }
    function Qr(e) {
      return String(e || "Arial").split(",").map((t) => t.trim()).filter((t) => t !== "").map((t) => /^["'].*["']$/.test(t) || /^[\w-]+$/.test(t) ? t : `"${t.replace(/"/g, '\\"')}"`).join(", ") || "Arial";
    }
    function eu(e, t) {
      return Array.from(String(e || "")).reduce((n, i) => n + (/^[\x00-\x7F]$/.test(i) ? Cd : 1), 0) * t;
    }
    function tu(e) {
      const t = Zi(e);
      if (!t) return 0;
      const n = Me.value.lineName, i = Math.max(0, m(n.fontSize)), a = Zr();
      if (a) {
        a.font = `${n.fontStyle || "normal"} ${n.fontWeight || "normal"} ${i}px ${Qr(n.fontFamily)}`;
        const s = a.measureText(t);
        if (Number.isFinite(s.width)) return s.width;
      }
      return eu(t, i);
    }
    function nu(e, t) {
      const n = tu(e);
      if (n <= 0) return be.gap;
      const i = Math.max(0, m(Me.value.lineName.fontSize)), a = (n * Math.abs(t.ux) + i * Math.abs(t.uy)) / 2;
      return Math.max(be.gap, a + be.namePadding);
    }
    function lu(e) {
      return String(e?.arrowDirection ?? e?.ArrowDirection ?? "").trim().toUpperCase();
    }
    function la(e) {
      return String(e?.arrowType ?? e?.ArrowType ?? "").trim().toUpperCase();
    }
    function iu(e) {
      const t = la(e);
      return t === "F" ? 1 : t === "P" ? 2 : t === "PF" ? 3 : ["LO", "LI", "LIRO", "LORI", "OF"].includes(t) ? 1 : 0;
    }
    function ou(e) {
      const t = lu(e), n = la(e);
      return (n === "LIRO" || n === "LORI") && t ? ["L", "R"] : t === "L" ? ["L"] : t === "R" ? ["R"] : t === "LR" ? ["L", "R"] : [];
    }
    function It(e) {
      return Number.isFinite(e) ? Number(e.toFixed(3)) : 0;
    }
    function au(e, t) {
      return e === "LO" ? "out" : e === "LI" ? "in" : e === "LIRO" ? t === "L" ? "in" : "out" : e === "LORI" ? t === "L" ? "out" : "in" : e === "OF" ? "oversize" : "";
    }
    function su(e) {
      const t = be.tailGap;
      return e === "LI" || e === "LIRO" || e === "LORI" ? t + be.tailLineSpacing : e === "OF" ? t + be.tailCircleRadius * 2 + be.tailCircleGap : e === "LO" ? t : 0;
    }
    function ru(e, t, n, i) {
      const a = {
        x: e.x - t.x * n,
        y: e.y - t.y * n
      }, s = { x: -t.y, y: t.x }, d = {
        x: a.x + s.x * i,
        y: a.y + s.y * i
      }, f = {
        x: a.x - s.x * i,
        y: a.y - s.y * i
      };
      return {
        base: a,
        perpendicular: s,
        path: [
          "M",
          It(e.x),
          It(e.y),
          "L",
          It(d.x),
          It(d.y),
          "L",
          It(f.x),
          It(f.y),
          "Z"
        ].join(" ")
      };
    }
    function ia(e, t, n) {
      const i = n / 2;
      return {
        x1: It(e.x + t.x * i),
        y1: It(e.y + t.y * i),
        x2: It(e.x - t.x * i),
        y2: It(e.y - t.y * i)
      };
    }
    function uu(e, t, n, i, a) {
      if (!e)
        return { lines: [], circles: [] };
      const s = {
        x: t.x - n.x * be.tailGap,
        y: t.y - n.y * be.tailGap
      };
      if (e === "out")
        return {
          lines: [ia(s, i, a)],
          circles: []
        };
      if (e === "in") {
        const d = {
          x: s.x - n.x * be.tailLineSpacing,
          y: s.y - n.y * be.tailLineSpacing
        };
        return {
          lines: [
            ia(s, i, a),
            ia(d, i, a)
          ],
          circles: []
        };
      }
      if (e === "oversize") {
        const d = be.tailCircleRadius;
        return {
          lines: [],
          circles: [{
            cx: It(t.x - n.x * (d + be.tailCircleGap + be.tailGap)),
            cy: It(t.y - n.y * (d + be.tailCircleGap + be.tailGap)),
            r: d
          }]
        };
      }
      return { lines: [], circles: [] };
    }
    function cu(e) {
      const t = U(e?.x1), n = G(e?.y1), i = U(e?.x2), a = G(e?.y2), s = i - t, d = a - n, f = Math.hypot(s, d);
      if (!Number.isFinite(f) || f <= 0) return null;
      const p = t < i || t === i && n <= a ? 1 : -1;
      return {
        center: {
          x: (t + i) / 2,
          y: (n + a) / 2
        },
        ux: s / f * p,
        uy: d / f * p,
        length: f
      };
    }
    function du(e) {
      const t = ou(e), n = iu(e);
      if (t.length === 0 || n === 0) return [];
      const i = la(e), a = cu(e);
      if (!a) return [];
      const s = nu(e, a), d = a.length / 2 - s - su(i);
      if (d < be.minLength) return [];
      const f = Math.min(be.length, d / n);
      if (f < be.minLength) return [];
      const p = Math.min(be.halfWidth, f * 0.45), I = [];
      for (const b of t) {
        const C = b === "L" ? { x: a.ux, y: a.uy } : { x: -a.ux, y: -a.uy }, B = {
          x: a.center.x - C.x * s,
          y: a.center.y - C.y * s
        };
        for (let T = 0; T < n; T++) {
          const ge = {
            x: B.x - C.x * f * T,
            y: B.y - C.y * f * T
          }, L = ru(ge, C, f, p), R = T === n - 1 ? au(i, b) : "", he = uu(
            R,
            L.base,
            C,
            L.perpendicular,
            p * 2
          );
          I.push({
            id: `${e.id}-arrow-${b}-${T}`,
            lineId: e.id,
            path: L.path,
            tailLines: he.lines,
            tailCircles: he.circles
          });
        }
      }
      return I;
    }
    function Cs(e) {
      const t = nt(e?.start), n = nt(e?.end), i = m(e?.radius) || Nl, a = Math.max(1e-3, $n(i)), s = Math.max(1e-3, un(i)), d = Number(e?.largeArcFlag) === 1 ? 1 : 0, f = Number(e?.sweepFlag) === 1 ? 1 : 0;
      return `M ${U(t.x)} ${G(t.y)} A ${a} ${s} 0 ${d} ${f} ${U(n.x)} ${G(n.y)}`;
    }
    function fu() {
      return gs({
        position: { x: Ue.value.x, y: Ue.value.y },
        direction: Ue.value.direction,
        type: Ue.value.type
      });
    }
    function mu() {
      return Ds({
        position: { x: Ve.value.x, y: Ve.value.y },
        direction: Ve.value.direction,
        type: Ve.value.type
      });
    }
    function yu(e) {
      return `translate(${U(e.position.x)},${G(e.position.y)})`;
    }
    function Qi(e, t) {
      const i = {
        x: m(t.x) * tt.value,
        y: m(t.y) * Ne.value
      };
      if (i.x === 0)
        return { x1: 0, y1: 0, x2: 0, y2: i.y > 0 ? 10 : -10 };
      const a = i.y / i.x, s = Math.sqrt(100 / (1 + a * a)), d = s * a, f = -Math.sqrt(100 / (1 + a * a)), p = f * a, I = s * i.x + d * i.y, b = Math.hypot(i.x, i.y), C = Math.hypot(s, d), B = I / (b * C);
      return Math.abs(B - 1) < 0.01 ? { x1: 0, y1: 0, x2: s, y2: d } : { x1: 0, y1: 0, x2: f, y2: p };
    }
    return Ws(() => {
      Z.value?.focus({ preventScroll: !0 });
    }), ma(() => y.readonly, (e) => {
      e && bn(0);
    }), Gs(() => {
      wn(), Qa(), Pa(), Ja();
    }), g({
      editModeCode: D,
      drawingObject: M,
      mouseGridSnapModeCode: it,
      mouseObjectSnapModeCode: pt,
      setEditMode: bn,
      setDrawingObject: zo,
      setDrawingSignalType: _o,
      setDrawingBufferStopDirection: Vo,
      setDrawingBufferStopType: $o,
      setMouseGridSnapModeCode: Fo,
      setMouseObjectSnapModeCode: Pn,
      clearSelectedLines: Zl,
      clearSelectedNodes: Jt,
      clearSelectedEquipment: Yn,
      deleteLine: Po,
      getSelectedNodeDeletePlan: vt,
      deleteNode: Wo,
      deleteEquipment: Go,
      revoke: Vi,
      redo: jl,
      buildJsonData: ir,
      loadDataFromJson: or,
      autoSeparateLine: E,
      markCrossPoint: l,
      removeCrossPoint: r,
      snapLine: S,
      autoMergeNode: h,
      autoGenerateNodes: hl,
      getEquipmentBindingNodeCorrectionPlan: Xa,
      applyEquipmentBindingNodeCorrections: Ya,
      correctEquipmentBindingNodesByPosition: hr,
      getAutoGenerateSwitchPlan: Na,
      autoGenerateSwitches: Qs,
      autoGenerateCurves: er,
      startDrawingSignal: Q,
      startDrawingInsulationJoint: ze,
      startDrawingBufferStop: jt,
      startDrawingNode: Ke,
      startDrawingPlatform: _a,
      updateSelectedAnnotation: So,
      updateSelectedEquipment: fr,
      updateSelectedEquipmentBatch: mr,
      clearElements: Va,
      getFullViewRect: Al,
      scrollDataRectIntoView: xi,
      getCanvasViewportState: go
    }), (e, t) => (w(), N("svg", {
      id: "layout-editor-svg",
      ref_key: "svgRef",
      ref: Z,
      tabindex: "0",
      width: Ot.value,
      height: Wt.value,
      style: Fe(_n.value),
      onMousemove: zr,
      onMousedown: Fr,
      onMouseup: Ar,
      onMouseleave: t[3] || (t[3] = (n) => et()),
      onKeydown: Er,
      onSelectstart: t[4] || (t[4] = Qt(() => {
      }, ["prevent"])),
      onDragstart: t[5] || (t[5] = Qt(() => {
      }, ["prevent"]))
    }, [
      v("g", wc, [
        (w(!0), N(P, null, H(vn.value, (n, i) => (w(), N("circle", {
          key: `g-${i}`,
          class: "griddot",
          cx: U(n.x),
          cy: G(n.y),
          r: "0.7"
        }, null, 8, Ic))), 128))
      ]),
      v("g", kc, [
        (w(!0), N(P, null, H(gn.value, (n) => (w(), N("line", {
          id: n.id,
          key: `line-${n.id}`,
          class: Oe(["track", { "track-selected": Gi(n.line.id) }]),
          x1: U(n.x1),
          y1: G(n.y1),
          x2: U(n.x2),
          y2: G(n.y2),
          style: Fe(qr(n.line)),
          onMouseenter: (i) => ot("link", n.line.id),
          onMouseleave: (i) => et("link", n.line.id),
          onMousedown: t[0] || (t[0] = Qt(() => {
          }, ["stop"])),
          onClick: Qt((i) => $a(i, n.line.id), ["stop"])
        }, null, 46, Nc))), 128)),
        (w(!0), N(P, null, H(Ci.value, (n) => Dl((w(), N("text", {
          key: `line-name-${n.id}`,
          class: Oe(["trackname", { "name-selected": Gi(n.line.id) }]),
          style: Fe(Ki("lineName", Gi(n.line.id), xt("link", n.line.id))),
          x: U(n.x),
          y: G(n.y),
          onMouseenter: (i) => ot("link", n.line.id),
          onMouseleave: (i) => et("link", n.line.id),
          onMousedown: t[1] || (t[1] = Qt(() => {
          }, ["stop"])),
          onClick: Qt((i) => $a(i, n.line.id), ["stop"])
        }, _(Zi(n.line)), 47, Dc)), [
          [ya, Zi(n.line)]
        ])), 128)),
        (w(!0), N(P, null, H(Gl.value, (n) => (w(), N("path", {
          id: String(n.id),
          key: `curve-${n.id}`,
          class: "curve",
          d: Cs(n),
          style: Fe(jr())
        }, null, 12, Lc))), 128)),
        (w(!0), N(P, null, H(Li.value, (n) => (w(), N("g", {
          key: n.id,
          class: Oe(["link-arrow", { "link-arrow-selected": Gi(n.lineId) }]),
          style: Fe(St("link", n.lineId))
        }, [
          v("path", {
            d: n.path
          }, null, 8, Cc),
          (w(!0), N(P, null, H(n.tailLines, (i, a) => (w(), N("line", {
            key: `tail-line-${a}`,
            class: "link-arrow-tail-line",
            x1: i.x1,
            y1: i.y1,
            x2: i.x2,
            y2: i.y2
          }, null, 8, Mc))), 128)),
          (w(!0), N(P, null, H(n.tailCircles, (i, a) => (w(), N("circle", {
            key: `tail-circle-${a}`,
            class: "link-arrow-tail-circle",
            cx: i.cx,
            cy: i.cy,
            r: i.r
          }, null, 8, _c))), 128))
        ], 6))), 128)),
        de.value && M.value === "l" && Re.value ? (w(), N("line", {
          key: 0,
          class: "track track-temp",
          x1: U(Re.value.x1),
          y1: G(Re.value.y1),
          x2: U(Re.value.x2),
          y2: G(Re.value.y2),
          style: Fe({ strokeWidth: Me.value.track.strokeWidth })
        }, null, 12, Vc)) : ie("", !0),
        (w(!0), N(P, null, H(Io.value, (n) => (w(), N("rect", {
          id: n.id,
          key: n.id,
          class: "anchor snapobj",
          x: Si(n),
          y: vo(n),
          width: ne.size,
          height: ne.size,
          onMousedown: (i) => Vr(i, n)
        }, null, 40, $c))), 128)),
        (w(!0), N(P, null, H(sn.value, (n, i) => (w(), N("circle", {
          key: `cp-${i}`,
          class: Oe(["crosspoint", `rela${n.code}`]),
          cx: U(n.x),
          cy: G(n.y),
          r: "4"
        }, null, 10, zc))), 128)),
        de.value && Nt.value ? (w(), N("circle", {
          key: 1,
          class: "perpendicular",
          cx: U(Nt.value.x),
          cy: G(Nt.value.y),
          r: "4"
        }, null, 8, Fc)) : ie("", !0)
      ]),
      y.showNodes ? (w(), N("g", Ac, [
        (w(!0), N(P, null, H(O.value, (n) => (w(), N("circle", {
          id: n.id,
          key: `node-${n.id}`,
          class: Oe(["node snapobj", { "node-selected": as(n.id) }]),
          cx: U(n.x),
          cy: G(n.y),
          r: Me.value.node.radius,
          style: Fe(Hr(n.id)),
          onMouseenter: (i) => ot("node", n.id),
          onMouseleave: (i) => et("node", n.id),
          onMousedown: (i) => ar(i, n.id)
        }, null, 46, Ec))), 128)),
        de.value && M.value === "n" && ht.value.visible ? (w(), N("circle", {
          key: 0,
          class: "node node-temp",
          cx: U(ht.value.x),
          cy: G(ht.value.y),
          r: Me.value.node.radius
        }, null, 8, Bc)) : ie("", !0)
      ])) : ie("", !0),
      an.value ? (w(), N("g", Rc, [
        (w(!0), N(P, null, H(on.value, (n) => (w(), N("line", {
          key: `route-line-${n.id}`,
          class: "route-highlight-line",
          x1: U(n.x1),
          y1: G(n.y1),
          x2: U(n.x2),
          y2: G(n.y2),
          style: Fe(yi.value)
        }, null, 12, Tc))), 128)),
        (w(!0), N(P, null, H(Gt.value, (n) => (w(), N("path", {
          key: `route-curve-${n.id}`,
          class: "route-highlight-curve",
          d: Cs(n),
          style: Fe(yi.value)
        }, null, 12, Xc))), 128)),
        (w(!0), N(P, null, H(jn.value, (n) => (w(), N("circle", {
          key: `route-node-${n.id}`,
          class: "route-highlight-node",
          cx: U(n.x),
          cy: G(n.y),
          r: me.value,
          style: Fe(ro.value)
        }, null, 12, Yc))), 128)),
        je.value ? (w(), N("polygon", {
          key: 0,
          class: "route-highlight-arrow",
          points: je.value.points,
          style: Fe(uo.value)
        }, null, 12, Pc)) : ie("", !0)
      ])) : ie("", !0),
      y.showCellNames && Jl.value.length > 0 ? (w(), N("g", Oc, [
        (w(!0), N(P, null, H(Jl.value, (n) => (w(), N("text", {
          key: `cell-name-${n.key}`,
          class: Oe(["cell-name", { "cell-name-hovered": $e("cellName", n.key) }]),
          x: U(n.x),
          y: G(n.y),
          onMouseenter: (i) => ot("cellName", n.key),
          onMouseleave: (i) => et("cellName", n.key),
          onMousedown: t[2] || (t[2] = Qt(() => {
          }, ["stop", "prevent"])),
          onClick: Qt((i) => Mo(n), ["stop", "prevent"])
        }, _(n.name), 43, Wc))), 128))
      ])) : ie("", !0),
      v("g", Gc, [
        (w(!0), N(P, null, H(xe.value, (n) => (w(), N("g", {
          id: String(n.id),
          key: `signal-${n.id}`,
          class: Oe(["signal", ps(n), { "signal-selected": Ko(n.id) }]),
          transform: gs(n),
          style: Fe(St("signal", n.id)),
          onMouseenter: (i) => ot("signal", n.id),
          onMouseleave: (i) => et("signal", n.id),
          onMousedown: (i) => Fa(i, n.id)
        }, [
          (w(!0), N(P, null, H(hs(n), (i, a) => (w(), ce(eo(i.tag), fi({
            key: `signal-element-${n.id}-${a}`
          }, { ref_for: !0 }, i.attrs), null, 16))), 128))
        ], 46, Uc))), 128)),
        (w(!0), N(P, null, H(xe.value, (n) => Dl((w(), N("text", {
          key: `signal-name-${n.id}`,
          class: Oe(["signalname", { "name-selected": Ko(n.id) }]),
          style: Fe(Ki("signalName", Ko(n.id), xt("signal", n.id))),
          x: Xr(n),
          y: Yr(n),
          "text-anchor": Pr(n),
          "dominant-baseline": "middle",
          onMouseenter: (i) => ot("signal", n.id),
          onMouseleave: (i) => et("signal", n.id),
          onMousedown: (i) => Fa(i, n.id)
        }, _(Sn(n, "SIGNAL")), 47, Jc)), [
          [ya, Sn(n, "SIGNAL")]
        ])), 128)),
        de.value && M.value === "s" && Ue.value.visible ? (w(), N("g", {
          key: 0,
          id: "tempsignal",
          class: Oe(["signal", ps(Ue.value), "signal-temp"]),
          transform: fu()
        }, [
          (w(!0), N(P, null, H(hs(Ue.value), (n, i) => (w(), ce(eo(n.tag), fi({
            key: `temp-signal-element-${i}`
          }, { ref_for: !0 }, n.attrs), null, 16))), 128))
        ], 10, qc)) : ie("", !0)
      ]),
      v("g", jc, [
        (w(!0), N(P, null, H(le.value, (n) => (w(), N("g", {
          id: String(n.id),
          key: `ij-${n.id}`,
          class: Oe(["insulationjoint insulationjoint-normal", { "insulationjoint-selected": Rr(n.id) }]),
          transform: `translate(${U(n.position.x)},${G(n.position.y)})`,
          style: Fe(St("insulationJoint", n.id)),
          onMouseenter: (i) => ot("insulationJoint", n.id),
          onMouseleave: (i) => et("insulationJoint", n.id),
          onMousedown: (i) => sr(i, n.id)
        }, [...t[6] || (t[6] = [
          v("line", {
            x1: "0",
            y1: "-5",
            x2: "0",
            y2: "5"
          }, null, -1)
        ])], 46, Hc))), 128)),
        de.value && M.value === "i" && yt.value.visible ? (w(), N("g", Kc, [
          v("line", {
            x1: U(yt.value.x),
            x2: U(yt.value.x),
            y1: G(yt.value.y) - 5,
            y2: G(yt.value.y) + 5
          }, null, 8, Zc)
        ])) : ie("", !0)
      ]),
      v("g", Qc, [
        (w(!0), N(P, null, H(fe.value, (n) => (w(), N("g", {
          id: String(n.id),
          key: `buffer-stop-${n.id}`,
          class: Oe(["bufferstop", Ss(n), { "bufferstop-selected": ds(n.id) }]),
          transform: Ds(n),
          onMouseenter: (i) => ot("bufferStop", n.id),
          onMouseleave: (i) => et("bufferStop", n.id),
          onMousedown: (i) => rr(i, n.id)
        }, [
          v("g", {
            class: "bufferstop-shape",
            style: Fe(Ns()),
            transform: ks(n)
          }, [
            (w(!0), N(P, null, H(bs(n), (i, a) => (w(), ce(eo(i.tag), fi({
              key: `buffer-stop-element-${n.id}-${a}`
            }, { ref_for: !0 }, i.attrs), null, 16))), 128))
          ], 12, td),
          ds(n.id) ? (w(), N("rect", {
            key: 0,
            class: "bufferstop-selection",
            x: "0",
            y: Is(n),
            width: Gr(n),
            height: ws(n),
            style: Fe(St("bufferStop", n.id))
          }, null, 12, nd)) : ie("", !0)
        ], 42, ed))), 128)),
        de.value && M.value === "e" && Ve.value.visible ? (w(), N("g", {
          key: 0,
          id: "tempbufferstop",
          class: Oe(["bufferstop", Ss(Ve.value), "bufferstop-temp"]),
          transform: mu()
        }, [
          v("g", {
            class: "bufferstop-shape",
            style: Fe(Ns()),
            transform: ks(Ve.value)
          }, [
            (w(!0), N(P, null, H(bs(Ve.value), (n, i) => (w(), ce(eo(n.tag), fi({
              key: `temp-buffer-stop-element-${i}`
            }, { ref_for: !0 }, n.attrs), null, 16))), 128))
          ], 12, id)
        ], 10, ld)) : ie("", !0)
      ]),
      v("g", od, [
        (w(!0), N(P, null, H(te.value, (n) => (w(), N("g", {
          id: String(n.id),
          key: `sw-${n.id}`,
          class: Oe(["switch", { "switch-selected": Qo(n.id) }]),
          transform: yu(n),
          onMouseenter: (i) => ot("switch", n.id),
          onMouseleave: (i) => et("switch", n.id),
          onMousedown: (i) => ur(i, n.id)
        }, [
          (w(!0), N(P, null, H(n.branchVectorList, (i, a) => (w(), N("line", {
            key: `sw-line-${n.id}-${a}`,
            class: "switchbranch",
            x1: Qi(n, i).x1,
            y1: Qi(n, i).y1,
            x2: Qi(n, i).x2,
            y2: Qi(n, i).y2,
            style: Fe(Kr(n.id))
          }, null, 12, sd))), 128)),
          v("text", {
            class: Oe(["switchname", { "name-selected": Qo(n.id) }]),
            style: Fe(Ki("switchName", Qo(n.id), xt("switch", n.id))),
            x: "4",
            y: "-4"
          }, _(Sn(n, "SWITCH")), 7)
        ], 42, ad))), 128))
      ]),
      v("g", rd, [
        (w(!0), N(P, null, H(Be.value, (n) => (w(), N("g", {
          id: String(n.id),
          key: `platform-${n.id}`,
          class: Oe(["platform", { "platform-selected": ta(n.id) }]),
          onMouseenter: (i) => ot("platform", n.id),
          onMouseleave: (i) => et("platform", n.id),
          onMousedown: (i) => cr(i, n.id)
        }, [
          v("rect", {
            x: U(n.x),
            y: G(n.y),
            width: $n(n.width),
            height: un(n.height),
            style: Fe(Ls(n.id))
          }, null, 12, cd),
          v("text", {
            class: Oe(["platformname", { "name-selected": ta(n.id) }]),
            x: Lt(n.x, n.width),
            y: vi(n.y, n.height),
            style: Fe(Ki("platformName", ta(n.id), xt("platform", n.id)))
          }, _(Sn(n, "PLATFORM")), 15, dd)
        ], 42, ud))), 128)),
        de.value && M.value === "p" && Te.value ? (w(), N("rect", {
          key: 0,
          class: "platform platform-temp",
          x: U(Wi.value.x),
          y: G(Wi.value.y),
          width: $n(Wi.value.width),
          height: un(Wi.value.height),
          style: Fe(Ls())
        }, null, 12, fd)) : ie("", !0)
      ]),
      v("g", md, [
        (w(!0), N(P, null, H(Ee.value, (n) => (w(), N("g", {
          id: String(n.id),
          key: `annotation-${n.id}`,
          class: Oe(["annotation", { "annotation-selected": ms(n.id) }]),
          transform: bo(n),
          onMouseenter: (i) => ot("annotation", n.id),
          onMouseleave: (i) => et("annotation", n.id),
          onMousedown: (i) => dr(i, n.id)
        }, [
          v("text", {
            class: Oe(["annotation-text", { "name-selected": ms(n.id) }]),
            x: "0",
            y: "0",
            "font-family": n.fontFamily,
            "font-size": n.fontSize,
            "font-weight": n.fontWeight,
            "font-style": n.fontStyle,
            fill: xt("annotation", n.id) || n.textColor
          }, _(n.text), 11, vd),
          wo(n) ? (w(), N("circle", {
            key: 0,
            class: "annotation-text-anchor",
            cx: "0",
            cy: "0",
            r: Ad,
            onMousedown: (i) => Lr(i, n.id)
          }, null, 40, gd)) : ie("", !0)
        ], 42, yd))), 128))
      ]),
      v("g", pd, [
        rn.value.visible ? (w(), N("rect", {
          key: 0,
          class: "selection-box",
          x: rn.value.x,
          y: rn.value.y,
          width: rn.value.width,
          height: rn.value.height
        }, null, 8, hd)) : ie("", !0),
        de.value && M.value === "s" ? (w(), N("g", xd, [...t[7] || (t[7] = [
          v("rect", {
            x: "12",
            y: "12",
            width: "245",
            height: "28",
            rx: "4"
          }, null, -1),
          v("text", {
            x: "24",
            y: "31"
          }, "方向: 按 w / e / s / d 设置", -1)
        ])])) : ie("", !0),
        de.value ? (w(), N(P, { key: 2 }, [
          v("rect", {
            class: "cursor",
            x: U(ee.value.x) - ee.value.size / 2,
            y: G(ee.value.y) - ee.value.size / 2,
            width: ee.value.size,
            height: ee.value.size
          }, null, 8, Sd),
          v("line", {
            id: "cursorlineh",
            class: "cursor",
            x1: U(ee.value.x) - ee.value.barLength / 2,
            x2: U(ee.value.x) + ee.value.barLength / 2,
            y1: G(ee.value.y),
            y2: G(ee.value.y)
          }, null, 8, bd),
          v("line", {
            id: "cursorlinev",
            class: "cursor",
            x1: U(ee.value.x),
            x2: U(ee.value.x),
            y1: G(ee.value.y) - ee.value.barLength / 2,
            y2: G(ee.value.y) + ee.value.barLength / 2
          }, null, 8, wd),
          M.value === "s" ? (w(), N("g", {
            key: 0,
            class: "signal-direction-compass",
            transform: `translate(${U(ee.value.x)},${G(ee.value.y)})`
          }, [
            (w(), N(P, null, H(_l, (n) => v("text", {
              key: n.key,
              x: n.x,
              y: n.y
            }, _(n.label), 9, kd)), 64))
          ], 8, Id)) : ie("", !0)
        ], 64)) : ie("", !0)
      ])
    ], 44, bc));
  }
}, Bd = /* @__PURE__ */ ba(Ed, [["__scopeId", "data-v-efc97cb9"]]), Rd = {
  zh: {
    stationLayout: {
      toolbar: {
        edit: "编辑工具栏",
        compact: "简洁模式",
        full: "完整模式"
      },
      menu: {
        stationScheme: "车站方案",
        file: "文件",
        loadData: "载入数据",
        saveData: "保存数据",
        gridSnap: "网格追踪",
        objectSnap: "对象追踪",
        snapDistance: "吸附阈值",
        showGrid: "显示网格",
        gridSpacing: "网格间距",
        undo: "撤销",
        redo: "重做",
        clearSelection: "清除选择",
        deleteSelection: "删除选择"
      },
      placeholders: {
        selectInstance: "请先选择车站",
        selectStationScheme: "选择车站方案"
      },
      schemeManager: {
        manage: "管理",
        title: "车站方案管理",
        add: "新增",
        edit: "编辑",
        save: "保存",
        cancel: "取消",
        delete: "删除",
        close: "关闭",
        confirm: "确定",
        id: "方案 ID",
        name: "方案名称",
        operation: "操作",
        namePlaceholder: "方案名称",
        idRequired: "方案 ID 不能为空",
        nameRequired: "方案名称不能为空",
        createSuccess: "车站方案已新增",
        createFailed: "新增车站方案失败",
        updateSuccess: "车站方案已更新",
        updateFailed: "更新车站方案失败",
        deleteSuccess: "车站方案已删除",
        deleteFailed: "删除车站方案失败",
        deleteTitle: "删除车站方案",
        deleteConfirm: "确定删除“{name}”及其全部布置图数据吗？"
      },
      group: {
        drawingObject: "绘图对象",
        curveDisplay: "曲线显示",
        displayScale: "显示比例"
      },
      scale: { x: "X", y: "Y" },
      mode: { select: "选择", draw: "绘图" },
      draw: {
        line: "线",
        node: "节点",
        signal: "信号机",
        switch: "道岔",
        insulation: "钢轨绝缘",
        route: "进路",
        buffer: "车挡",
        platform: "站台",
        annotation: "注释"
      },
      tools: {
        showCrossPoint: "显示交点",
        hideCrossPoint: "隐藏交点",
        snapLine: "处理虚接",
        separateLine: "线路分段",
        generateNode: "节点生成",
        generateSwitch: "道岔生成",
        generateCurve: "曲线生成",
        fitFullView: "显示全图"
      },
      curveDisplay: { arc: "圆弧", tangent: "切线" },
      messages: {
        saveSuccess: "保存成功：",
        saveFailed: "保存失败：",
        loadFailed: "获取失败：",
        loadSchemesFailed: "加载车站方案失败",
        readonly: "当前为只读模式，不能执行写操作"
      }
    }
  },
  en: {
    stationLayout: {
      toolbar: {
        edit: "Edit toolbar",
        compact: "Compact",
        full: "Full"
      },
      menu: {
        stationScheme: "Station Scheme",
        file: "File",
        loadData: "Load",
        saveData: "Save",
        gridSnap: "Grid Snap",
        objectSnap: "Object Snap",
        snapDistance: "Snap Distance",
        showGrid: "Show Grid",
        gridSpacing: "Grid Spacing",
        undo: "Undo",
        redo: "Redo",
        clearSelection: "Clear Selection",
        deleteSelection: "Delete Selection"
      },
      placeholders: {
        selectInstance: "Please select a station",
        selectStationScheme: "Select station scheme"
      },
      schemeManager: {
        manage: "Manage",
        title: "Station Scheme Manager",
        add: "Add",
        edit: "Edit",
        save: "Save",
        cancel: "Cancel",
        delete: "Delete",
        close: "Close",
        confirm: "Confirm",
        id: "Scheme ID",
        name: "Scheme Name",
        operation: "Operation",
        namePlaceholder: "Scheme name",
        idRequired: "Scheme ID is required",
        nameRequired: "Scheme name is required",
        createSuccess: "Station scheme created",
        createFailed: "Failed to create station scheme",
        updateSuccess: "Station scheme updated",
        updateFailed: "Failed to update station scheme",
        deleteSuccess: "Station scheme deleted",
        deleteFailed: "Failed to delete station scheme",
        deleteTitle: "Delete station scheme",
        deleteConfirm: 'Delete "{name}" and all of its layout data?'
      },
      group: {
        drawingObject: "Drawing Object",
        curveDisplay: "Curve View",
        displayScale: "Display Scale"
      },
      scale: { x: "X", y: "Y" },
      mode: { select: "Select", draw: "Draw" },
      draw: {
        line: "Line",
        node: "Node",
        signal: "Signal",
        switch: "Switch",
        insulation: "Insulation",
        route: "Route",
        buffer: "Buffer",
        platform: "Platform",
        annotation: "Annotation"
      },
      tools: {
        showCrossPoint: "Show Cross",
        hideCrossPoint: "Hide Cross",
        snapLine: "Snap Line",
        separateLine: "Separate",
        generateNode: "Generate Node",
        generateSwitch: "Generate Switch",
        generateCurve: "Generate Curve",
        fitFullView: "Fit All"
      },
      curveDisplay: { arc: "Arc", tangent: "Tangent" },
      messages: {
        saveSuccess: "Save successful: ",
        saveFailed: "Save failed: ",
        loadFailed: "Load failed: ",
        loadSchemesFailed: "Failed to load station schemes",
        readonly: "Read-only mode does not allow write operations"
      }
    }
  }
};
function js($ = "zh") {
  const g = Rd[$];
  return (F, y) => {
    let q = g;
    for (const j of F.split("."))
      q = q && typeof q == "object" ? q[j] : void 0;
    return (typeof q == "string" ? q : F).replace(/\{([^{}]+)\}/g, (j, D) => String(y?.[D] ?? `{${D}}`));
  };
}
const Td = ["aria-label"], Xd = { class: "station-layout-toolbar__row station-layout-toolbar__row--main" }, Yd = { class: "station-layout-toolbar__identity" }, Pd = { class: "station-layout-toolbar__primary" }, Od = { class: "station-layout-toolbar__actions" }, Wd = { class: "station-layout-toolbar__row station-layout-toolbar__row--essential" }, Gd = { class: "station-layout-toolbar__row station-layout-toolbar__row--advanced" }, Ud = /* @__PURE__ */ Us({
  __name: "StationLayoutEditToolbar",
  props: /* @__PURE__ */ pu({
    translate: { type: Function }
  }, {
    density: { type: String, default: "compact" },
    densityModifiers: {}
  }),
  emits: ["update:density"],
  setup($) {
    const g = $, F = js("zh"), y = (D, M) => g.translate?.(D, M) ?? F(D, M), q = gu($, "density"), Z = A(() => q.value === "full");
    function j() {
      q.value = Z.value ? "compact" : "full";
    }
    return (D, M) => {
      const de = ke("el-button");
      return w(), N("section", {
        class: Oe(["station-layout-toolbar station-layout-edit-toolbar", { "is-full": Z.value, "is-compact": !Z.value }]),
        "aria-label": y("stationLayout.toolbar.edit")
      }, [
        v("div", Xd, [
          v("div", Yd, [
            di(D.$slots, "context", {}, void 0, !0)
          ]),
          v("div", Pd, [
            di(D.$slots, "primary", {}, void 0, !0)
          ]),
          v("div", Od, [
            di(D.$slots, "actions", {}, void 0, !0),
            c(de, {
              class: "station-layout-toolbar__density",
              size: "small",
              text: "",
              icon: Z.value ? J(xu) : J(Su),
              title: Z.value ? y("stationLayout.toolbar.compact") : y("stationLayout.toolbar.full"),
              onClick: j
            }, {
              default: x(() => [
                V(_(Z.value ? y("stationLayout.toolbar.compact") : y("stationLayout.toolbar.full")), 1)
              ]),
              _: 1
            }, 8, ["icon", "title"])
          ])
        ]),
        v("div", Wd, [
          di(D.$slots, "essential", {}, void 0, !0)
        ]),
        Dl(v("div", Gd, [
          di(D.$slots, "advanced", {}, void 0, !0)
        ], 512), [
          [ya, Z.value]
        ])
      ], 10, Td);
    };
  }
}), Jd = /* @__PURE__ */ ba(Ud, [["__scopeId", "data-v-c61ba569"]]), qd = { class: "station-layout-page" }, jd = { class: "station-scheme-control-row" }, Hd = { class: "station-toolbar-group__label" }, Kd = { class: "station-toolbar-group" }, Zd = { class: "station-toolbar-group__label" }, Qd = { class: "drawing-object-button-label" }, ef = { class: "drawing-object-button-label" }, tf = { class: "station-toolbar-group" }, nf = { class: "station-toolbar-group" }, lf = { class: "station-toolbar-switch-control" }, of = { class: "station-toolbar-switch-control__label" }, af = { class: "station-toolbar-switch-control" }, sf = { class: "station-toolbar-group" }, rf = { class: "station-toolbar-group scale-toolbar-group" }, uf = { class: "station-toolbar-group__label" }, cf = { class: "scale-slider" }, df = { class: "scale-slider-label" }, ff = { class: "scale-slider-value" }, mf = { class: "scale-slider" }, yf = { class: "scale-slider-label" }, vf = { class: "scale-slider-value" }, gf = { class: "station-toolbar-group" }, pf = { class: "station-toolbar-group" }, hf = { class: "station-toolbar-switch-control" }, xf = { class: "station-toolbar-switch-control__label" }, Sf = { class: "station-toolbar-switch-control" }, bf = { class: "station-toolbar-switch-control__label" }, wf = { class: "toolbar-field-item" }, If = { class: "toolbar-group-label" }, kf = { class: "toolbar-field-item" }, Nf = { class: "toolbar-group-label" }, Df = { class: "station-toolbar-group" }, Lf = { class: "station-toolbar-group" }, Cf = { class: "station-toolbar-group" }, Mf = { class: "station-toolbar-switch-control" }, _f = { class: "station-toolbar-switch-control__label" }, Vf = { class: "station-toolbar-switch-control__state" }, $f = { class: "station-toolbar-switch-control" }, zf = ["disabled"], Ff = { class: "station-scheme-manager" }, Af = { class: "station-scheme-create-row" }, Ef = { key: 1 }, Bf = {
  key: 0,
  class: "station-scheme-actions"
}, Rf = {
  key: 1,
  class: "station-scheme-actions"
}, Tf = { class: "binding-correction-dialog" }, Xf = { class: "binding-correction-summary" }, Yf = { key: 0 }, Pf = { class: "binding-correction-footer" }, Of = { class: "binding-correction-actions" }, Wf = { class: "layout-style-table" }, Gf = { class: "layout-style-label" }, Uf = { class: "layout-style-grid" }, Jf = { class: "layout-style-section" }, qf = { class: "layout-style-field" }, jf = { class: "layout-style-field" }, Hf = { class: "layout-style-section" }, Kf = { class: "layout-style-field" }, Zf = { class: "layout-style-field" }, Qf = { class: "layout-style-section" }, em = { class: "layout-style-field" }, tm = { class: "layout-style-field" }, nm = { class: "layout-style-section" }, lm = { class: "layout-style-field" }, im = { class: "layout-style-section" }, om = { class: "layout-style-field" }, am = { class: "layout-style-field" }, sm = { class: "layout-style-section" }, rm = { class: "layout-style-field" }, um = { class: "layout-style-field" }, cm = {
  key: 0,
  class: "annotation-editor-row"
}, dm = { class: "station-layout-workspace" }, fm = {
  key: 0,
  class: "cell-side-panel"
}, mm = { class: "cell-side-panel-header" }, ym = { class: "cell-side-panel-subtitle" }, vm = { class: "cell-side-panel-body" }, gm = { class: "cell-panel-section" }, pm = { class: "cell-panel-section-header" }, hm = { class: "cell-panel-actions" }, xm = { class: "cell-panel-section cell-detail-section" }, Sm = { class: "cell-panel-section-header" }, bm = {
  key: 0,
  class: "cell-detail-content"
}, wm = { class: "cell-link-toolbar" }, Im = { class: "cell-link-toolbar-actions" }, km = { class: "cell-link-detail" }, Nm = { class: "cell-link-detail-value" }, Dm = { class: "cell-link-detail-value" }, Lm = {
  key: 1,
  class: "equipment-side-panel"
}, Cm = { class: "equipment-side-panel-header" }, Mm = { class: "equipment-side-panel-title" }, _m = { class: "equipment-side-panel-body" }, Vm = {
  key: 8,
  class: "equipment-form-grid"
}, $m = {
  key: 9,
  class: "equipment-form-grid"
}, zm = {
  key: 10,
  class: "equipment-form-grid"
}, Fm = { class: "equipment-side-panel-footer" }, Am = {
  key: 2,
  class: "route-search-panel"
}, Em = { class: "route-search-panel-header" }, Bm = { class: "route-search-panel-subtitle" }, Rm = { class: "route-search-panel-body" }, Tm = { class: "route-search-form" }, Xm = { class: "route-search-input-row" }, Ym = { class: "route-search-input-row" }, Pm = { class: "route-search-actions" }, Om = { class: "dwg-extract-form" }, Wm = ["disabled"], Os = 20, Gm = 500, fa = "TEMP_CELL_", Um = {
  __name: "StationLayout",
  props: {
    selectedInstanceId: {
      type: String,
      default: ""
    },
    gateway: {
      type: Object,
      required: !0
    },
    translate: {
      type: Function,
      default: null
    },
    formatError: {
      type: Function,
      default: null
    },
    readonly: {
      type: Boolean,
      default: !1
    }
  },
  setup($) {
    const g = $, F = js("zh"), y = (o, l) => g.translate?.(o, l) ?? F(o, l);
    function q(o, l = /* @__PURE__ */ new Set()) {
      if (typeof o == "string") return o.trim();
      if (o instanceof Error && o.message) return String(o.message).trim();
      if (!o || typeof o != "object" || l.has(o)) return "";
      if (l.add(o), Array.isArray(o)) {
        for (const r of o) {
          const S = q(r, l);
          if (S) return S;
        }
        return "";
      }
      for (const r of ["message", "detail", "title", "error", "errors", "data", "body", "payload", "response", "cause"]) {
        const S = q(o[r], l);
        if (S) return S;
      }
      try {
        const r = JSON.stringify(o);
        return r && r !== "{}" ? r : "";
      } catch {
        return "";
      }
    }
    function Z(o, l) {
      return q(o) || l;
    }
    function j(o, l) {
      try {
        const r = g.formatError?.(o, l);
        if (typeof r == "string" && r.trim())
          return r.trim();
      } catch (r) {
        console.warn("StationLayout formatError failed:", r);
      }
      return Z(o, l);
    }
    function D(o = {}) {
      return g.readonly ? (o?.silent !== !0 && X.warning(y("stationLayout.messages.readonly")), !1) : !0;
    }
    const M = k(null), de = k(null), it = k(null), pt = k(!1), Ml = k(null), at = k(null), Pt = k(null), be = k("0"), tn = k(!1), st = k(!1), ee = k(!1), _l = k("compact"), ne = k(""), tt = k(!1), Ne = k([]), Cn = k(!1), we = k(!1), Jn = k({ name: "" }), Mn = k(""), Ot = k({ name: "" }), Wt = k(1), _n = k(1), Me = k(!0), Vl = k(!0), qn = k(!0), nn = k(!0), ln = k(Os), on = k(!1), Gt = k(!1), jn = k(null), Hn = k(null), an = k([]), kt = k([]), $l = A(() => Wt.value.toFixed(2)), yi = A(() => _n.value.toFixed(2));
    function ro(o, l = {}) {
      if (!o) return;
      const r = Math.max(0, Number(l.screenMargin ?? 48)), S = de.value;
      if (S) {
        const h = Math.max(1, Number(o.maxX) - Number(o.minX)), E = Math.max(1, Number(o.maxY) - Number(o.minY)), Q = Math.max(0.25, Math.min(4, Math.min(
          (S.clientWidth - r * 2) / h,
          (S.clientHeight - r * 2) / E
        )));
        Wt.value = Number(Q.toFixed(2)), _n.value = Number(Q.toFixed(2));
      }
      Un(() => M.value?.scrollDataRectIntoView?.(o, {
        screenMargin: r,
        padding: l.padding ?? 160
      }));
    }
    function uo() {
      const o = M.value?.getFullViewRect?.();
      ro(o, { screenMargin: 48, padding: 160 });
    }
    const me = k(null), je = k(null), rt = k(!1), z = k({}), Y = k({}), Ae = k(!1), O = k(0), xe = k("auto"), le = A(() => O.value === 0), fe = A(() => !!je.value?.batch);
    let Be = null;
    const te = k(!1), Ee = k(!1), ye = k(""), De = k({
      startNodeId: "",
      endNodeId: ""
    }), We = k([]), Ge = k(-1), _e = k(!1), W = k([]), se = k(""), oe = k(""), Le = k("cell"), mt = k(!1), pe = k(An()), sn = k(!1), Nt = k({
      tracks: [],
      nodes: [],
      insulationJoints: []
    }), Re = A(() => Ge.value < 0 ? null : We.value[Ge.value] || null), Ue = A(() => Re.value?.linkIds || []), yt = A(() => Re.value?.nodeIds || []), Ve = A(() => Nt.value.tracks || []), ht = A(() => {
      const o = /* @__PURE__ */ new Map();
      for (const l of Ve.value) {
        const r = String(l?.id ?? "").trim();
        r && o.set(r, l);
      }
      return o;
    }), Te = A(() => _e.value ? "cell_editing" : ""), He = A(() => Xe(pe.value.linkIDList)), Kn = A(() => W.value.map((o) => {
      if (o.id !== se.value) return o;
      const l = Bt(pe.value) || Bt(o), r = String(pe.value.name || "").trim();
      return {
        ...o,
        id: String(o.id || pe.value.id || "").trim(),
        isNew: l,
        name: r || (l ? "" : String(o.id || "").trim()),
        linkIDList: mn(pe.value.linkIDList)
      };
    })), Zn = A(() => {
      const o = {};
      for (const l of W.value) {
        const r = l.id === se.value ? pe.value.linkIDList : l.linkIDList;
        for (const S of new Set(Xe(r)))
          o[S] = (o[S] || 0) + 1;
      }
      return o;
    }), Qn = A(() => {
      if (!_e.value || !se.value) return [];
      const o = String(oe.value || "").trim();
      return Le.value === "link" && o ? [o] : He.value;
    }), Qe = A(() => {
      const o = /* @__PURE__ */ new Set();
      for (const l of Ue.value) {
        const r = String(l ?? "").trim();
        r && o.add(r);
      }
      for (const l of Qn.value) {
        const r = String(l ?? "").trim();
        r && o.add(r);
      }
      return [...o];
    }), Dt = k(Ll), Ft = k(en), rn = k(mi), el = k("l"), Ut = /* @__PURE__ */ new Set(["l", "n", "s", "w", "i", "r", "e", "p", "a"]), Vn = A(() => pc.map((o) => {
      const l = Array.isArray(o.children) && o.children.length > 0 ? o.children : [{ label: o.label, value: o.value }];
      return {
        label: o.label,
        value: o.value,
        showLabel: l.length > 1,
        options: l.map((r) => ({
          label: r.label,
          value: r.value
        }))
      };
    })), co = A(() => {
      const o = String(rn.value || "");
      for (const l of Vn.value) {
        const r = l.options.find((S) => S.value === o);
        if (r) return r.label;
      }
      return o || y("stationLayout.draw.signal");
    }), m = A(
      () => un(ua, Ft.value, y("stationLayout.draw.buffer"))
    ), U = A(
      () => un(ra, Dt.value, "")
    ), G = A(
      () => `${y("stationLayout.draw.signal")} ${co.value}`
    ), $n = A(
      () => `${y("stationLayout.draw.buffer")} ${m.value}/${U.value}`
    );
    function un(o, l, r = "") {
      const S = String(l || "");
      return o.find((h) => h.value === S)?.label || S || r;
    }
    function Lt(o) {
      return !le.value && el.value === o ? "primary" : "default";
    }
    const vi = ["Arial", "Microsoft YaHei", "SimSun", "SimHei", "Times New Roman", "Consolas"], gi = [
      { label: "常规", value: "normal" },
      { label: "加粗", value: "bold" }
    ], pi = [
      { label: "常规", value: "normal" },
      { label: "斜体", value: "italic" }
    ], zl = [
      { key: "switchName", label: "道岔编号" },
      { key: "platformName", label: "站台名称" },
      { key: "signalName", label: "信号机名称" },
      { key: "lineName", label: "线路名称" }
    ], cn = {
      switchName: { fontSize: 8, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
      platformName: { fontSize: 10, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
      signalName: { fontSize: 8, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
      lineName: { fontSize: 10, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
      track: { strokeWidth: 2, color: "#fefded" },
      curve: { strokeWidth: 4, color: "#ffb347" },
      platform: { strokeWidth: 1, color: "#87ceeb" },
      signal: { scale: 0.5 },
      switch: { strokeWidth: 5, color: "#00ffff" },
      node: { radius: 5, color: "#ffffff" }
    }, K = k(gn()), dn = [
      { label: "不绘制", value: "" },
      { label: "左侧", value: "L" },
      { label: "右侧", value: "R" },
      { label: "左右两侧", value: "LR" }
    ], zn = [
      { label: "不绘制", value: "" },
      { label: "旅客列车进路", value: "P" },
      { label: "货物列车进路", value: "F" },
      { label: "客货列车进路", value: "PF" },
      { label: "机车出段", value: "LO" },
      { label: "机车入段", value: "LI" },
      { label: "机车出入段（左入右出）", value: "LIRO" },
      { label: "机车出入段（左出右入）", value: "LORI" },
      { label: "超限货物列车进路", value: "OF" }
    ], Ct = {
      link: "Link",
      signal: "信号机",
      switch: "道岔",
      platform: "站台",
      insulationJoint: "钢轨绝缘",
      bufferStop: "车挡"
    }, At = A(() => {
      if (!je.value) return "设备信息";
      const o = Ct[je.value.kind] || "设备";
      return fe.value ? `${o} 批处理（${je.value.count || 0} 个）` : `${o} ${je.value.id || ""}`;
    });
    function Et() {
      O.value = 0, M.value?.setEditMode(0);
    }
    function Fl() {
      if (!D()) {
        Et();
        return;
      }
      O.value = 1, M.value?.setEditMode(1), M.value?.setDrawingObject(el.value);
    }
    function fo(o) {
      Number(o) === 0 ? Et() : (ye.value = "", Fl());
    }
    function mo() {
      const o = de.value;
      return !!(o?.isConnected && o.getClientRects().length > 0);
    }
    function hi(o) {
      return o instanceof HTMLElement ? !!(o.isContentEditable || o.closest("input, textarea, select, [contenteditable='true'], [contenteditable='']")) : !1;
    }
    function yo(o) {
      return o.key === "F4" && !o.altKey && !o.ctrlKey && !o.metaKey && !o.shiftKey;
    }
    function xi(o) {
      yo(o) && (!mo() || hi(o.target) || (o.preventDefault(), Be == null && (Be = O.value, O.value === 1 && Et())));
    }
    function Al() {
      const o = Be;
      Be = null, o === 1 && Fl();
    }
    function tl(o) {
      o.key !== "F4" || Be == null || (o.preventDefault(), Al());
    }
    function Fn() {
      te.value = !te.value, te.value ? Et() : (ye.value = "", nt());
    }
    function Si(o) {
      ye.value = ye.value === o ? "" : o, ye.value && Et();
    }
    function vo(o) {
      const l = String(o?.nodeId ?? "").trim(), r = o?.target || ye.value;
      l && (r === "start" ? De.value.startNodeId = l : r === "end" && (De.value.endNodeId = l), ye.value = "");
    }
    function nt() {
      Ge.value = -1;
    }
    function El(o) {
      Ge.value = Number(o?.index ?? -1);
    }
    function fn() {
      We.value = [], nt();
    }
    function go(o) {
      o?.requiresRepair && (sn.value = !0, Mt(), fn());
    }
    function Bl() {
      sn.value && (sn.value = !1, kl.alert(
        "已保存最新拓扑。线路分段、节点生成或节点合并可能已影响设备绑定、道岔/曲线关联、Cell 构成以及既有进路。请使用“修正设备绑定节点”检查设备，并在 Cell 面板中重新检查或生成 Cell；如已配置进路，也请重新校验进路。",
        "请检查拓扑关联",
        {
          type: "warning",
          confirmButtonText: "知道了",
          closeOnClickModal: !1
        }
      ).catch(() => {
      }));
    }
    function An() {
      return {
        instanceID: g.selectedInstanceId || "",
        stationSchemeID: ne.value || "",
        id: "",
        isNew: !1,
        name: "",
        linkIDList: ""
      };
    }
    function En(o, ...l) {
      for (const r of l) {
        const S = o?.[r];
        if (S != null) return String(S);
      }
      return "";
    }
    function Xe(o) {
      return Array.isArray(o) ? o.map((l) => String(l ?? "").trim()).filter((l) => l !== "") : String(o ?? "").split(/[\s,，;；]+/).map((l) => l.trim()).filter((l) => l !== "");
    }
    function mn(o) {
      return [...new Set(Xe(o))].join(",");
    }
    function Bn(o, l) {
      return String(o ?? "").localeCompare(String(l ?? ""), void 0, {
        numeric: !0,
        sensitivity: "base"
      });
    }
    function Rl(o) {
      return String(o ?? "").trim().startsWith(fa);
    }
    function Bt(o) {
      return !!(o?.isNew || o?.IsNew) || Rl(o?.id ?? o?.ID);
    }
    function Rn(o = /* @__PURE__ */ new Set()) {
      const l = new Set(W.value.map((h) => h.id));
      for (const h of o)
        l.add(h);
      let r = 1, S = `${fa}${r}`;
      for (; l.has(S); )
        r++, S = `${fa}${r}`;
      return S;
    }
    function Tl(o) {
      const l = String(o?.id ?? o?.ID ?? "").trim();
      return !l || Bt(o) ? "保存后生成" : l;
    }
    function Xl(o, l = /* @__PURE__ */ new Set()) {
      let r = En(o, "id", "ID").trim();
      const S = En(o, "name", "Name").trim(), h = !!(o?.isNew || o?.IsNew) || Rl(r) || !r;
      return r || (r = Rn(l)), {
        instanceID: En(o, "instanceID", "InstanceID").trim() || g.selectedInstanceId || "",
        stationSchemeID: En(o, "stationSchemeID", "StationSchemeID").trim() || ne.value || "",
        id: r,
        isNew: h,
        name: S || (h ? "" : r),
        linkIDList: mn(En(o, "linkIDList", "LinkIDList"))
      };
    }
    function po(o) {
      if (!Array.isArray(o)) return [];
      const l = /* @__PURE__ */ new Set(), r = [];
      for (const S of o) {
        const h = Xl(S, l);
        !h.id || l.has(h.id) || (l.add(h.id), r.push(h));
      }
      return r.sort((S, h) => Bn(S.id, h.id));
    }
    function bi() {
      W.value = [], se.value = "", oe.value = "", Le.value = "cell", mt.value = !1, pe.value = An();
    }
    function ot(o) {
      W.value = po(o?.cells || []);
      const l = W.value.find((r) => r.id === se.value) || W.value[0] || null;
      l ? $e(l) : (se.value = "", oe.value = "", Le.value = "cell", pe.value = An());
    }
    function et(o) {
      return {
        instanceID: o?.instanceID || g.selectedInstanceId || "",
        stationSchemeID: o?.stationSchemeID || ne.value || "",
        id: o?.id || "",
        isNew: Bt(o),
        name: o?.name || (Bt(o) ? "" : o?.id) || "",
        linkIDList: mn(o?.linkIDList)
      };
    }
    function $e(o) {
      if (!o) return;
      se.value = o.id, pe.value = et(o);
      const l = Xe(pe.value.linkIDList);
      oe.value = l[0] || "", Le.value = "cell";
    }
    function wi(o) {
      St(), $e(o);
    }
    function xt(o) {
      if (!o?.id || !St({ showWarning: !0 })) return;
      const l = W.value.find((r) => r.id === o.id);
      l && ($e(l), _e.value || (_e.value = !0, Mt()));
    }
    function ho(o) {
      return Xe(o?.linkIDList).length;
    }
    function St(o = {}) {
      if (g.readonly) return !0;
      const l = se.value;
      if (!l) return !0;
      const r = Xl(pe.value);
      if (!r.id)
        return o.showWarning && X.warning("Cell 保存标识不能为空"), !1;
      if (W.value.some((E) => E.id === r.id && E.id !== l))
        return o.showWarning && X.warning(`Cell ID ${r.id} 已存在`), !1;
      const h = W.value.findIndex((E) => E.id === l);
      return h < 0 ? !1 : (W.value[h] = r, se.value = r.id, pe.value = et(r), !0);
    }
    function yn() {
      return St(), W.value.map((o) => {
        const l = Xl(o);
        return {
          instanceID: g.selectedInstanceId || l.instanceID,
          stationSchemeID: ne.value || l.stationSchemeID,
          id: Bt(l) ? "" : l.id,
          linkIDList: l.linkIDList,
          name: l.name
        };
      });
    }
    function xo() {
      if (!D() || !St({ showWarning: !0 })) return;
      const o = Rn(), l = {
        instanceID: g.selectedInstanceId || "",
        stationSchemeID: ne.value || "",
        id: o,
        isNew: !0,
        name: `Cell ${W.value.length + 1}`,
        linkIDList: ""
      };
      W.value = [...W.value, l], $e(l);
    }
    async function lt() {
      if (!D() || !se.value) return;
      const o = W.value.find((S) => S.id === se.value), l = o?.name || Tl(o);
      try {
        await kl.confirm(
          `确定删除 Cell ${l} 吗？`,
          "删除 Cell",
          {
            confirmButtonText: "删除",
            cancelButtonText: "取消",
            type: "warning"
          }
        );
      } catch {
        return;
      }
      W.value = W.value.filter((S) => S.id !== se.value);
      const r = W.value[0] || null;
      r ? $e(r) : (se.value = "", oe.value = "", Le.value = "cell", pe.value = An());
    }
    function Tn(o) {
      if (!D()) return;
      const l = [...new Set(o.map((r) => String(r ?? "").trim()).filter((r) => r !== ""))];
      pe.value.linkIDList = l.join(","), Le.value = "cell", oe.value && !l.includes(oe.value) && (oe.value = l[0] || ""), oe.value || (oe.value = l[0] || ""), St();
    }
    function Ii(o) {
      if (!D()) return;
      if (!se.value) {
        X.warning("请先选择或新建一个 Cell");
        return;
      }
      const l = String(o ?? "").trim();
      if (!l) return;
      const r = Xe(pe.value.linkIDList);
      if (r.includes(l)) {
        oe.value = l, Le.value = "link", X.info(`Link ${l} 已在当前 Cell 中`);
        return;
      }
      Tn([...r, l]), oe.value = l, Le.value = "link", X.success(`已加入 Link ${l}`);
    }
    function So() {
      if (!D()) return;
      const o = String(oe.value || "").trim();
      o && (Tn(Xe(pe.value.linkIDList).filter((l) => l !== o)), Le.value = "cell");
    }
    function bo() {
      D() && (Tn([]), Le.value = "cell");
    }
    function Je() {
      Mt(), Le.value = "link";
    }
    function wo() {
      if (D()) {
        if (!se.value) {
          X.warning("请先选择或新建一个 Cell");
          return;
        }
        mt.value = !mt.value, mt.value && Et();
      }
    }
    function Yl() {
      _e.value = !_e.value, _e.value ? (Mt(), !se.value && W.value.length > 0 && $e(W.value[0])) : (mt.value = !1, oe.value = "", Le.value = "cell");
    }
    function ki(o) {
      return {
        ...o,
        id: String(o?.id ?? o?.ID ?? "").trim(),
        name: String(o?.name ?? o?.Name ?? "").trim(),
        fromNodeID: String(o?.fromNodeID ?? o?.FromNodeID ?? "").trim(),
        toNodeID: String(o?.toNodeID ?? o?.ToNodeID ?? "").trim()
      };
    }
    function Io(o) {
      return {
        ...o,
        id: String(o?.id ?? o?.ID ?? "").trim(),
        bindingNodeID: String(o?.bindingNodeID ?? o?.BindingNodeID ?? "").trim()
      };
    }
    function vn(o) {
      Nt.value = {
        tracks: Array.isArray(o?.tracks) ? o.tracks.map(ki) : [],
        nodes: Array.isArray(o?.nodes) ? o.nodes.map((l) => ({ ...l })) : [],
        insulationJoints: Array.isArray(o?.insulationJoints) ? o.insulationJoints.map(Io) : []
      };
    }
    function Mt() {
      const o = M.value?.buildJsonData?.();
      if (o)
        try {
          vn(JSON.parse(o));
        } catch (l) {
          console.error("Failed to refresh station layout snapshot:", l);
        }
    }
    function Pl() {
      const o = new Set(Ve.value.map((l) => l.id).filter(Boolean));
      if (o.size !== 0 && (W.value = W.value.map((l) => ({
        ...l,
        linkIDList: Xe(l.linkIDList).filter((r) => o.has(r)).join(",")
      })), se.value)) {
        const l = W.value.find((r) => r.id === se.value);
        l && $e(l);
      }
    }
    function nl(o, l) {
      const r = String(o ?? "").trim(), S = String(l ?? "").trim();
      if (!(!r || !S || r === S) && (W.value = W.value.map((h) => ({
        ...h,
        linkIDList: Xe(h.linkIDList).map((E) => E === r ? S : E).join(",")
      })), oe.value === r && (oe.value = S), se.value)) {
        const h = W.value.find((E) => E.id === se.value);
        h && (pe.value = et(h));
      }
    }
    function ll(o) {
      const l = String(o ?? "").trim(), r = ht.value.get(l);
      return r?.name ? `${r.name} (${l})` : `Link ${l}`;
    }
    function Xn(o) {
      const l = ht.value.get(String(o ?? "").trim());
      if (!l) return "当前图中未找到该 Link";
      const r = l.fromNodeID || "-", S = l.toNodeID || "-";
      return `${r} -> ${S}`;
    }
    function Ol() {
      Mt();
      const o = Ve.value.filter((Q) => Q.id), l = new Set(
        (Nt.value.insulationJoints || []).map((Q) => String(Q.bindingNodeID || "").trim()).filter((Q) => Q !== "")
      ), r = /* @__PURE__ */ new Map(), S = new Map(o.map((Q) => [Q.id, /* @__PURE__ */ new Set()]));
      for (const Q of o)
        for (const Se of [Q.fromNodeID, Q.toNodeID]) {
          const ae = String(Se || "").trim();
          ae && (r.has(ae) || r.set(ae, []), r.get(ae).push(Q.id));
        }
      for (const [Q, Se] of r.entries())
        if (!l.has(Q))
          for (const ae of Se) {
            const ze = S.get(ae);
            for (const wt of Se)
              wt !== ae && ze.add(wt);
          }
      const h = /* @__PURE__ */ new Set(), E = [];
      for (const Q of [...o].sort((Se, ae) => Bn(Se.id, ae.id))) {
        if (h.has(Q.id)) continue;
        const Se = [Q.id], ae = [];
        for (h.add(Q.id); Se.length > 0; ) {
          const ze = Se.shift();
          ae.push(ze);
          for (const wt of S.get(ze) || [])
            h.has(wt) || (h.add(wt), Se.push(wt));
        }
        E.push(ae.sort(Bn));
      }
      return E;
    }
    async function ko() {
      if (!D()) return;
      const o = Ol();
      if (o.length === 0) {
        X.warning("当前车站布置图中没有可生成 Cell 的 Link");
        return;
      }
      if (W.value.length > 0)
        try {
          await kl.confirm(
            "自动生成会覆盖当前 Cell 列表，是否继续？",
            "自动生成 Cell",
            {
              confirmButtonText: "生成",
              cancelButtonText: "取消",
              type: "warning"
            }
          );
        } catch {
          return;
        }
      const l = /* @__PURE__ */ new Set();
      W.value = o.map((r, S) => {
        const h = Rn(l);
        return l.add(h), {
          instanceID: g.selectedInstanceId || "",
          stationSchemeID: ne.value || "",
          id: h,
          isNew: !0,
          name: `Cell ${S + 1}`,
          linkIDList: r.join(",")
        };
      }), $e(W.value[0]), _e.value = !0, X.success(`已生成 ${W.value.length} 个 Cell`);
    }
    async function Wl() {
      D() && St({ showWarning: !0 }) && await Pn({
        silent: !0,
        successMessage: "Cell 已保存",
        failurePrefix: "Cell 保存失败："
      });
    }
    function il(o, l) {
      for (const r of l) {
        const S = o?.[r];
        if (Array.isArray(S))
          return S.map((h) => String(h)).filter((h) => h !== "");
      }
      return [];
    }
    function Ni(o, l) {
      const r = il(o, ["nodeIds", "nodeIDs", "NodeIds", "NodeIDs"]), S = il(o, ["linkIds", "linkIDs", "LinkIds", "LinkIDs"]), h = il(o, ["cellIds", "cellIDs", "CellIds", "CellIDs"]);
      return {
        ...o,
        index: l,
        direction: String(o?.direction ?? o?.Direction ?? ""),
        nodeIds: r,
        linkIds: S,
        cellIds: h
      };
    }
    function No(o) {
      return o === "LeftToRight" ? "左向右" : o === "RightToLeft" ? "右向左" : o || "-";
    }
    function Do(o) {
      return o ? o.nodeIds.length > 0 ? o.nodeIds.join(" -> ") : `${o.linkIds.length} links` : "";
    }
    async function Di() {
      if (!g.selectedInstanceId) {
        X.warning(y("stationLayout.placeholders.selectInstance"));
        return;
      }
      const o = String(De.value.startNodeId || "").trim(), l = String(De.value.endNodeId || "").trim();
      if (!o || !l) {
        X.warning("请输入起点和终点 Node ID");
        return;
      }
      const r = Number(o), S = Number(l);
      if (!Number.isInteger(r) || !Number.isInteger(S)) {
        X.warning("Node ID 必须为整数");
        return;
      }
      Ee.value = !0;
      try {
        const h = await g.gateway.searchRoutes({
          instanceId: g.selectedInstanceId,
          stationSchemeId: ne.value,
          startNodeId: r,
          endNodeId: S
        }), E = Array.isArray(h?.routes) ? h.routes : Array.isArray(h?.Routes) ? h.Routes : [];
        We.value = E.map((Q, Se) => Ni(Q, Se)), Ge.value = We.value.length > 0 ? 0 : -1, X.success(`搜索完成，共 ${We.value.length} 条路径`);
      } catch (h) {
        We.value = [], Ge.value = -1, X.error(j(h, "路径搜索失败"));
      } finally {
        Ee.value = !1;
      }
    }
    function gn() {
      return JSON.parse(JSON.stringify(cn));
    }
    function Gl(o) {
      const l = gn(), r = o && typeof o == "object" && !Array.isArray(o) ? o : {};
      for (const S of zl)
        r[S.key] && typeof r[S.key] == "object" && !Array.isArray(r[S.key]) && (l[S.key] = { ...l[S.key], ...r[S.key] });
      for (const S of ["track", "curve", "platform", "signal", "switch", "node"])
        r[S] && typeof r[S] == "object" && !Array.isArray(r[S]) && (l[S] = { ...l[S], ...r[S] });
      return l;
    }
    function Li(o) {
      K.value = Gl(o);
    }
    function Ci(o) {
      const l = Number(o);
      return !Number.isFinite(l) || l <= 0 ? Os : Math.min(Gm, Math.max(1, l));
    }
    function Mi(o) {
      const l = Number(o);
      return Number.isFinite(l) ? l : 0;
    }
    function Ul(o) {
      const l = o && typeof o == "object" && !Array.isArray(o) ? o : {}, r = l.showGrid ?? l.ShowGrid;
      return {
        showGrid: r == null ? !0 : r !== !1,
        spacing: Ci(l.spacing ?? l.Spacing ?? l.gridSpacing ?? l.GridSpacing),
        originX: Mi(l.originX ?? l.OriginX),
        originY: Mi(l.originY ?? l.OriginY)
      };
    }
    function Lo(o) {
      return {
        ...Ul(o),
        showGrid: nn.value !== !1,
        spacing: Ci(ln.value)
      };
    }
    function ol(o) {
      const l = Ul(o);
      return nn.value = l.showGrid, ln.value = l.spacing, l;
    }
    function Co() {
      ol();
    }
    function _i(o) {
      const l = JSON.parse(o);
      return l.cells = yn(), l.metadata = {
        ...l.metadata || {},
        displayStyles: Gl(K.value),
        gridSettings: Lo(l.metadata?.gridSettings)
      }, JSON.stringify(l);
    }
    function Jl() {
      K.value = gn();
    }
    async function Mo() {
      if (!D()) return;
      await Pn({
        silent: !0,
        successMessage: "显示样式已保存",
        failurePrefix: "显示样式保存失败："
      }) && (on.value = !1);
    }
    function pn() {
      M.value?.clearSelectedLines(), M.value?.clearSelectedNodes(), M.value?.clearSelectedEquipment();
    }
    const ql = {
      signal: "信号机",
      insulationJoint: "钢轨绝缘",
      switch: "道岔",
      bufferStop: "车挡"
    };
    function Ce(o) {
      const l = Object.entries(o?.counts || {}).filter(([, E]) => Number(E) > 0).map(([E, Q]) => `${ql[E] || E} ${Q} 个`).join("、"), r = (o?.boundEquipment || []).slice(0, 8).map((E) => {
        const Q = ql[E.kind] || E.kind, Se = E.name || E.id || "";
        return `${Q}${Se ? ` ${Se}` : ""}`;
      }).join("、"), S = Math.max(0, (o?.boundEquipment?.length || 0) - 8), h = r ? `

绑定设备：${r}${S > 0 ? ` 等 ${o.boundEquipment.length} 个` : ""}` : "";
      return `所选 ${o?.nodeIds?.length || 0} 个节点上绑定了 ${l || "设备"}。确认删除节点，并一并删除这些绑定设备吗？${h}`;
    }
    async function Vi(o) {
      if (!o?.requiresConfirmation) return !0;
      try {
        return await kl.confirm(
          Ce(o),
          "删除节点及绑定设备",
          {
            confirmButtonText: "删除",
            cancelButtonText: "取消",
            type: "warning"
          }
        ), !0;
      } catch {
        return !1;
      }
    }
    async function jl() {
      if (!D()) return;
      const o = M.value;
      if (!o) return;
      const l = o.getSelectedNodeDeletePlan?.();
      await Vi(l) && (o.deleteLine(), o.deleteNode({ deleteBoundEquipment: !0 }), o.deleteEquipment(), Mt(), Pl());
    }
    function ut() {
      D() && M.value?.revoke();
    }
    function al() {
      D() && M.value?.redo();
    }
    const qe = k(!0), _t = k(!0), hn = k(10);
    function sl(o) {
      qe.value === !1 ? M.value?.setMouseGridSnapModeCode(0) : M.value?.setMouseGridSnapModeCode(1);
    }
    function Hl() {
      M.value?.setMouseObjectSnapModeCode(_t.value ? 1 : 0);
    }
    function xn(o) {
      const l = String(o?.id ?? o?.ID ?? "").trim();
      if (!l) return null;
      const r = String(o?.name ?? o?.Name ?? l).trim() || l, S = o?.revision ?? o?.Revision, h = Number(S);
      return {
        id: l,
        name: r,
        ...Number.isSafeInteger(h) && h >= 0 ? { revision: h } : {}
      };
    }
    function Kl(o = ne.value) {
      const l = Ne.value.find((S) => S.id === o), r = Number(l?.revision);
      return Number.isSafeInteger(r) && r >= 0 ? r : void 0;
    }
    function Sn(o, l) {
      const r = Number(l);
      !o || !Number.isSafeInteger(r) || r < 0 || (Ne.value = Ne.value.map((S) => {
        if (S.id !== o) return S;
        const h = Number(S.revision), E = Number.isSafeInteger(h) && h >= 0 ? Math.max(h, r) : r;
        return { ...S, revision: E };
      }));
    }
    function Zl(o, l = !0) {
      const r = new Map(
        Ne.value.map((h) => [h.id, h])
      ), S = /* @__PURE__ */ new Map();
      for (const h of o) {
        if (!h?.id || S.has(h.id)) continue;
        const E = r.get(h.id), Q = [h.revision, E?.revision].map(Number).filter((ae) => Number.isSafeInteger(ae) && ae >= 0), Se = Q.length > 0 ? Math.max(...Q) : void 0;
        S.set(h.id, {
          ...h,
          ...Se !== void 0 ? { revision: Se } : {}
        });
      }
      Ne.value = Array.from(S.values()), l && Jt();
    }
    function Jt(o, l) {
      const r = ne.value?.trim();
      if (!r) return;
      if (Ne.value.some((h) => h.id === r)) {
        Sn(r, l);
        return;
      }
      const S = Number(l);
      Ne.value = [
        ...Ne.value,
        {
          id: r,
          name: r,
          ...Number.isSafeInteger(S) && S >= 0 ? { revision: S } : {}
        }
      ];
    }
    function Yn(o) {
      return o?.id ? o.name || o.id : "";
    }
    function Rt(o = {}) {
      const l = o?.includeCurrent !== !1, r = g.selectedInstanceId;
      return r ? (tt.value = !0, g.gateway.getStationSchemes({ instanceId: r }).then((S) => {
        if (g.selectedInstanceId !== r)
          return [];
        const h = (S || []).map(xn).filter(Boolean);
        return Zl(h, l), h;
      }).catch((S) => g.selectedInstanceId !== r ? [] : (console.error("Failed to load station schemes:", S), X.error(j(S, y("stationLayout.messages.loadSchemesFailed"))), Ne.value)).finally(() => {
        g.selectedInstanceId === r && (tt.value = !1);
      })) : (Ne.value = [], tt.value = !1, Promise.resolve([]));
    }
    function Ql() {
      Jn.value = { name: "" };
    }
    function bn() {
      Mn.value = "", Ot.value = { name: "" };
    }
    async function _o() {
      if (!g.selectedInstanceId) {
        X.warning(y("stationLayout.placeholders.selectInstance"));
        return;
      }
      Cn.value = !0, Ql(), bn(), await Rt();
    }
    async function Vo() {
      if (!D()) return;
      const o = Jn.value.name.trim();
      if (!o) {
        X.warning(y("stationLayout.schemeManager.nameRequired"));
        return;
      }
      we.value = !0;
      try {
        const l = await g.gateway.createStationScheme({
          instanceId: g.selectedInstanceId,
          name: o
        }), S = xn(l)?.id || "";
        Ql(), ne.value = S, await Rt(), S && Vt({ stationSchemeId: S }), X.success(y("stationLayout.schemeManager.createSuccess"));
      } catch (l) {
        X.error(j(l, y("stationLayout.schemeManager.createFailed")));
      } finally {
        we.value = !1;
      }
    }
    function $o(o) {
      D() && (Mn.value = o.id, Ot.value = {
        name: o.name || o.id
      });
    }
    async function zo() {
      if (!D()) return;
      const o = Mn.value, l = Ot.value.name.trim();
      if (!o) {
        X.warning(y("stationLayout.schemeManager.idRequired"));
        return;
      }
      const r = ne.value === o;
      we.value = !0;
      try {
        await g.gateway.editStationScheme({
          instanceId: g.selectedInstanceId,
          originalId: o,
          name: l
        }), bn(), await Rt(), r && Vt({ stationSchemeId: o }), X.success(y("stationLayout.schemeManager.updateSuccess"));
      } catch (S) {
        X.error(j(S, y("stationLayout.schemeManager.updateFailed")));
      } finally {
        we.value = !1;
      }
    }
    async function Fo(o) {
      if (!D()) return;
      try {
        await kl.confirm(
          y("stationLayout.schemeManager.deleteConfirm", { name: Yn(o) }),
          y("stationLayout.schemeManager.deleteTitle"),
          {
            confirmButtonText: y("stationLayout.schemeManager.confirm"),
            cancelButtonText: y("stationLayout.schemeManager.cancel"),
            type: "warning"
          }
        );
      } catch {
        return;
      }
      const l = ne.value === o.id;
      we.value = !0;
      try {
        if (await g.gateway.deleteStationScheme({
          instanceId: g.selectedInstanceId,
          stationSchemeId: o.id
        }), l && (ne.value = ""), bn(), await Rt({ includeCurrent: !l }), l) {
          const r = Ne.value[0]?.id || "";
          ne.value = r, r ? Vt({ stationSchemeId: r }) : M.value?.clearElements();
        }
        X.success(y("stationLayout.schemeManager.deleteSuccess"));
      } catch (r) {
        X.error(j(r, y("stationLayout.schemeManager.deleteFailed")));
      } finally {
        we.value = !1;
      }
    }
    function Pn(o = {}) {
      const l = o?.silent === !0;
      if (!D({ silent: l }))
        return Promise.resolve(!1);
      if (!g.selectedInstanceId)
        return X.warning(y("stationLayout.placeholders.selectInstance")), Promise.resolve(!1);
      var r = M.value?.buildJsonData();
      if (!r)
        return X.warning("当前没有可保存的车站布置图数据"), Promise.resolve(!1);
      if (se.value && !St({ showWarning: !0 }))
        return Promise.resolve(!1);
      try {
        r = _i(r);
      } catch (ae) {
        return console.error("Failed to attach layout display styles:", ae), X.error("显示样式保存失败，请检查车站布置图数据"), Promise.resolve(!1);
      }
      const S = o?.successMessage || "设备信息已保存", h = o?.failurePrefix || "设备信息保存失败：", E = W.value.some(Bt);
      ee.value = !0;
      const Q = {
        json: r,
        instanceId: g.selectedInstanceId,
        stationSchemeId: ne.value
      }, Se = Kl();
      return Se !== void 0 && (Q.expectedRevision = Se), g.gateway.saveJson(Q).then((ae) => {
        const ze = ae?.stationSchemeId || ae?.stationSchemeID || ne.value;
        return ne.value = ze, Jt(void 0, ae?.revision ?? ae?.Revision), Sn(ze, ae?.revision ?? ae?.Revision), Rt(), l ? X.success(S) : alert(y("stationLayout.messages.saveSuccess") + (ae?.message || ae)), E && Vt({ stationSchemeId: ze }), Bl(), !0;
      }).catch((ae) => {
        const ze = j(ae, y("stationLayout.messages.saveFailed"));
        return l ? X.error(h + ze) : alert(y("stationLayout.messages.saveFailed") + ze), !1;
      }).finally(() => {
        ee.value = !1;
      });
    }
    function Vt(o = {}) {
      if (!g.selectedInstanceId) {
        ne.value = "", Ne.value = [], Jl(), Co(), M.value?.clearElements(), ye.value = "", fn(), bi(), vn({});
        return;
      }
      const l = g.selectedInstanceId, r = o?.stationSchemeId ?? ne.value;
      st.value = !0, g.gateway.getJson({
        instanceId: l,
        stationSchemeId: r
      }).then(async (S) => {
        g.selectedInstanceId === l && (ne.value = S?.metadata?.stationSchemeID || r || "", Li(S?.metadata?.displayStyles), ol(S?.metadata?.gridSettings), Jt(void 0, S?.metadata?.revision ?? S?.metadata?.Revision), await Un(), g.selectedInstanceId === l && (M.value?.loadDataFromJson(S), vn(S), ot(S), ye.value = "", fn()));
      }).catch((S) => {
        if (g.selectedInstanceId !== l)
          return;
        const h = j(S, y("stationLayout.messages.loadFailed"));
        alert(y("stationLayout.messages.loadFailed") + h);
      }).finally(() => {
        g.selectedInstanceId === l && (st.value = !1);
      });
    }
    function $t(o) {
      o && (ye.value = "", fn(), sn.value = !1, Vt({ stationSchemeId: o }));
    }
    function ei() {
      const o = M.value?.buildJsonData();
      if (!o) {
        X.warning("当前没有可导出的车站布置图数据");
        return;
      }
      try {
        const l = JSON.parse(_i(o)), r = JSON.stringify(l, null, 2), S = new Blob([r], { type: "application/json;charset=utf-8" }), h = URL.createObjectURL(S), E = document.createElement("a");
        E.href = h, E.download = Bi(l), document.body.appendChild(E), E.click(), document.body.removeChild(E), URL.revokeObjectURL(h), X.success("JSON 文件已导出");
      } catch (l) {
        console.error("Failed to export station layout JSON:", l), X.error("导出 JSON 文件失败");
      }
    }
    function $i(o) {
      me.value = o;
    }
    function Ao(o) {
      if (Mt(), mt.value) {
        o?.kind === "link" && Ii(o.id), z.value = {}, Y.value = {}, je.value = null;
        return;
      }
      if (je.value = o, !o) {
        z.value = {}, Y.value = {};
        return;
      }
      z.value = Ro(o), Y.value = zi(z.value);
    }
    function zi(o) {
      return JSON.parse(JSON.stringify(o || {}));
    }
    function Eo(o) {
      return o?.type || o?.SignalType || o?.signalType || "";
    }
    function Bo(o) {
      return o?.direction || o?.Direction || "";
    }
    function Fi(o) {
      return o?.type || o?.Type || "";
    }
    function Ro(o) {
      return o?.batch ? cl(o) : rl(o);
    }
    function rl(o) {
      const l = o?.data || {}, r = ["signal", "switch", "platform"].includes(o?.kind), S = o?.kind === "signal" ? Eo(l) : o?.kind === "bufferStop" ? Fi(l) || en : Fi(l), h = {
        kind: o?.kind || "",
        originalId: l.id || o?.id || "",
        id: l.id || "",
        name: l.name ?? (r && l.id || ""),
        type: S,
        direction: Bo(l),
        bindingNodeID: l.bindingNodeID || "",
        x: Number(l.x ?? l.position?.x ?? 0),
        y: Number(l.y ?? l.position?.y ?? 0),
        x1: Number(l.x1 ?? 0),
        y1: Number(l.y1 ?? 0),
        x2: Number(l.x2 ?? 0),
        y2: Number(l.y2 ?? 0),
        width: Number(l.width ?? 0),
        height: Number(l.height ?? 0),
        fromNodeID: l.fromNodeID || "",
        toNodeID: l.toNodeID || "",
        arrowDirection: String(l.arrowDirection ?? l.ArrowDirection ?? "").trim().toUpperCase(),
        arrowType: String(l.arrowType ?? l.ArrowType ?? "").trim().toUpperCase(),
        branchVectorListText: ""
      };
      return o?.kind === "switch" && (h.branchVectorListText = JSON.stringify(l.branchVectorList || [], null, 2)), h;
    }
    function zt(o) {
      return ["x", "y", "x1", "y1", "x2", "y2", "width", "height"].includes(o) ? null : "";
    }
    function On(o, l) {
      return Object.is(o, l);
    }
    function ul(o, l) {
      if (!Array.isArray(o) || o.length === 0) return zt(l);
      const r = o[0]?.[l];
      return o.every((S) => On(S?.[l], r)) ? r : zt(l);
    }
    function cl(o) {
      const l = (Array.isArray(o?.items) ? o.items : []).map((h) => rl({
        kind: o.kind,
        id: h?.id,
        data: h?.data || {}
      })), r = rl(o);
      if (l.length === 0)
        return {
          ...r,
          originalId: "",
          id: ""
        };
      const S = {
        ...r,
        originalId: ""
      };
      for (const h of Object.keys(S))
        h === "kind" || h === "originalId" || (S[h] = ul(l, h));
      return S;
    }
    function bt(o) {
      const l = Number(o);
      return Number.isFinite(l) ? l : 0;
    }
    function Ye(o) {
      return String(o ?? "").trim();
    }
    function Ai(o) {
      return Ye(o).toUpperCase();
    }
    function Ei(o, l, r = {}) {
      if (r.changedOnly !== !0) return !0;
      const S = r.baseline?.[o];
      if (S == null && l !== S) return !0;
      const h = r.normalize || ((E) => E);
      return h(l) !== h(S);
    }
    function ve(o, l, r, S, h = {}) {
      Ei(r, S, h) && (o[l] = h.normalize ? h.normalize(S) : S);
    }
    function wn(o, l, r = {}) {
      const S = {};
      ve(S, "x", "x", l.x, { ...r, normalize: bt }), ve(S, "y", "y", l.y, { ...r, normalize: bt }), Object.keys(S).length > 0 && (o.position = S);
    }
    function To(o = {}) {
      const l = z.value, r = {};
      if (o.includeId !== !1 && (r.id = Ye(l.id)), l.kind === "signal")
        ve(r, "name", "name", l.name, { ...o, normalize: Ye }), ve(r, "type", "type", l.type, { ...o, normalize: Ye }), ve(r, "direction", "direction", l.direction, { ...o, normalize: Ye }), ve(r, "bindingNodeID", "bindingNodeID", l.bindingNodeID, { ...o, normalize: Ye }), wn(r, l, o);
      else if (l.kind === "switch") {
        if (ve(r, "name", "name", l.name, { ...o, normalize: Ye }), ve(r, "type", "type", l.type, { ...o, normalize: Ye }), ve(r, "bindingNodeID", "bindingNodeID", l.bindingNodeID, { ...o, normalize: Ye }), wn(r, l, o), Ei("branchVectorListText", l.branchVectorListText, {
          ...o,
          normalize: (h) => String(h ?? "").trim()
        }))
          try {
            const h = l.branchVectorListText?.trim() ? JSON.parse(l.branchVectorListText) : [];
            if (!Array.isArray(h))
              throw new Error("branchVectorList must be an array.");
            r.branchVectorList = h;
          } catch {
            return X.error("道岔分支向量 JSON 格式不正确"), null;
          }
      } else l.kind === "platform" ? (ve(r, "name", "name", l.name, { ...o, normalize: Ye }), ve(r, "x", "x", l.x, { ...o, normalize: bt }), ve(r, "y", "y", l.y, { ...o, normalize: bt }), ve(r, "width", "width", l.width, { ...o, normalize: bt }), ve(r, "height", "height", l.height, { ...o, normalize: bt })) : l.kind === "insulationJoint" ? (ve(r, "type", "type", l.type, { ...o, normalize: Ye }), ve(r, "bindingNodeID", "bindingNodeID", l.bindingNodeID, { ...o, normalize: Ye }), wn(r, l, o)) : l.kind === "bufferStop" ? (ve(r, "type", "type", l.type || en, { ...o, normalize: Ye }), ve(r, "direction", "direction", l.direction || Ll, { ...o, normalize: Ye }), ve(r, "bindingNodeID", "bindingNodeID", l.bindingNodeID, { ...o, normalize: Ye }), wn(r, l, o)) : l.kind === "link" && (ve(r, "name", "name", l.name, { ...o, normalize: Ye }), ve(r, "x1", "x1", l.x1, { ...o, normalize: bt }), ve(r, "y1", "y1", l.y1, { ...o, normalize: bt }), ve(r, "x2", "x2", l.x2, { ...o, normalize: bt }), ve(r, "y2", "y2", l.y2, { ...o, normalize: bt }), ve(r, "fromNodeID", "fromNodeID", l.fromNodeID, { ...o, normalize: Ye }), ve(r, "toNodeID", "toNodeID", l.toNodeID, { ...o, normalize: Ye }), ve(r, "arrowDirection", "arrowDirection", l.arrowDirection, { ...o, normalize: Ai }), ve(r, "arrowType", "arrowType", l.arrowType, { ...o, normalize: Ai }));
      return r;
    }
    async function dl() {
      if (!D() || !je.value) return;
      const o = To(
        fe.value ? { changedOnly: !0, includeId: !1, baseline: Y.value } : {}
      );
      if (o) {
        if (!fe.value && !o.id) {
          X.warning("设备 ID 不能为空");
          return;
        }
        if (fe.value && Object.keys(o).length === 0) {
          X.warning("请先修改需要批处理的字段");
          return;
        }
        Ae.value = !0;
        try {
          const l = z.value.originalId;
          fe.value ? M.value?.updateSelectedEquipmentBatch(
            je.value.kind,
            je.value.ids || [],
            o
          ) : M.value?.updateSelectedEquipment(
            je.value.kind,
            l,
            o
          ), !fe.value && je.value.kind === "link" ? (nl(l, o.id), Mt()) : je.value.kind === "link" && Mt(), await Pn({ silent: !0 }) && (fe.value || (z.value.originalId = o.id), Y.value = zi(z.value));
        } finally {
          Ae.value = !1;
        }
      }
    }
    function Tt(o) {
      D() && me.value && M.value?.updateSelectedAnnotation(o);
    }
    function In() {
      me.value && Tt({
        position: {
          x: me.value.position?.x || 0,
          y: me.value.position?.y || 0
        }
      });
    }
    function Bi(o) {
      const l = o?.metadata?.instanceID || g.selectedInstanceId || "station-layout", r = o?.metadata?.stationSchemeID || ne.value || "scheme", S = (/* @__PURE__ */ new Date()).toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "");
      return `${`${l}-${r}-${S}`.replace(/[\\/:*?"<>|]/g, "_")}.json`;
    }
    function ti() {
      D() && at.value && (at.value.value = "", at.value.click());
    }
    async function Xo(o) {
      if (!D()) {
        o.target.value = "";
        return;
      }
      const l = o.target.files?.[0];
      if (l) {
        if (!l.name.toLowerCase().endsWith(".json")) {
          o.target.value = "", X.error("请选择 JSON 格式文件");
          return;
        }
        try {
          const r = await l.text(), S = JSON.parse(r);
          Ri(S), ne.value = S?.metadata?.stationSchemeID || ne.value, Li(S?.metadata?.displayStyles), ol(S?.metadata?.gridSettings), Jt(), await Un(), M.value?.loadDataFromJson(S), vn(S), ot(S), X.success("JSON 文件已导入");
        } catch (r) {
          console.error("Failed to import station layout JSON:", r), X.error("导入 JSON 文件失败，请检查文件格式");
        } finally {
          o.target.value = "";
        }
      }
    }
    function Ri(o) {
      if (!o || typeof o != "object" || Array.isArray(o))
        throw new Error("Invalid station layout JSON root.");
      const l = ["tracks", "curves", "nodes", "signals", "insulationJoints", "bufferStops", "platforms", "switches", "cells", "annotations"];
      for (const r of l)
        if (o[r] !== void 0 && !Array.isArray(o[r]))
          throw new Error(`Invalid station layout JSON field: ${r}`);
    }
    function Yo(o) {
      o === "load" ? Vt() : o === "save" ? Pn() : o === "importJson" ? ti() : o === "exportJson" ? ei() : o === "extractDwg" && Pi();
    }
    function Xt() {
      D() && M.value?.autoSeparateLine();
    }
    function Po() {
      D() && M.value?.markCrossPoint();
    }
    function Ti() {
      D() && M.value?.removeCrossPoint();
    }
    function ni() {
      D() && M.value?.snapLine();
    }
    function vt(o) {
      if (!D() || le.value) return;
      const l = String(o || "");
      Ut.has(l) && (el.value = l, M.value?.setDrawingObject(l));
    }
    function Oo(o) {
      if (!D() || le.value) return;
      const l = Array.isArray(o) ? o[o.length - 1] : o;
      rn.value = l, M.value?.setDrawingSignalType(l), vt("s"), it.value?.handleClose?.();
    }
    function Wo(o) {
      if (!D() || le.value) return;
      const l = String(o?.type || Ft.value || en), r = String(o?.direction || Dt.value || Ll);
      Ft.value = l, Dt.value = r, M.value?.setDrawingBufferStopType(l), M.value?.setDrawingBufferStopDirection(r), vt("e");
    }
    function Go() {
      D() && M.value?.autoGenerateNodes();
    }
    function fl() {
      D() && M.value?.autoMergeNode();
    }
    function Uo() {
      if (!D()) return;
      const o = M.value?.getEquipmentBindingNodeCorrectionPlan?.();
      if (o) {
        if (o.totalCount === 0) {
          X.info("当前没有需要修正绑定节点的设备");
          return;
        }
        if (!Array.isArray(o.items) || o.items.length === 0) {
          const l = o.unmatchedCount > 0 ? `，${o.unmatchedCount} 个设备当前位置没有节点` : "";
          X.info(`设备绑定节点已正确${l}`);
          return;
        }
        Hn.value = o, an.value = o.items, kt.value = [], Gt.value = !0, Un(() => {
          jn.value?.clearSelection?.();
          for (const l of an.value)
            jn.value?.toggleRowSelection?.(l, !0);
        });
      }
    }
    function Xi(o) {
      kt.value = Array.isArray(o) ? o : [];
    }
    function ml() {
      Gt.value = !1;
    }
    function yl() {
      Hn.value = null, an.value = [], kt.value = [];
    }
    function vl() {
      if (!D()) return;
      if (kt.value.length === 0) {
        X.warning("请先勾选需要修正的设备");
        return;
      }
      const o = M.value?.applyEquipmentBindingNodeCorrections?.(kt.value);
      if (!o) return;
      Gt.value = !1;
      const l = o.unmatchedCount > 0 ? `，${o.unmatchedCount} 个设备当前位置没有节点或已不存在` : "";
      o.fixedCount > 0 ? X.success(`已修正 ${o.fixedCount} 个设备绑定节点${l}`) : o.alreadyCorrectCount > 0 ? X.info(`所选设备绑定节点已正确${l}`) : X.warning(`未修正设备绑定节点${l}`);
    }
    function gt(o) {
      return Array.isArray(o) && o.length > 0 ? o.join(", ") : "无";
    }
    function li(o) {
      const l = o?.reconstructItems || [], r = l.slice(0, 8).map((E) => `${E.switchName || E.switchId || "未命名道岔"}（节点 ${E.nodeName || E.nodeId}：${gt(E.previousLineIds)} -> ${gt(E.nextLineIds)}）`).join(`
`), S = l.length > 8 ? `
等 ${l.length} 组道岔` : "", h = o?.createCount > 0 ? `

同时将新增生成 ${o.createCount} 组道岔。` : "";
      return `检测到 ${l.length} 组道岔绑定节点的邻接边发生变化，需要重新构造。确认后将保留原道岔 ID，并按当前邻接边重构。${r ? `

${r}${S}` : ""}${h}`;
    }
    async function Yi(o) {
      if (!o?.requiresConfirmation) return !0;
      try {
        return await kl.confirm(
          li(o),
          "重构道岔",
          {
            confirmButtonText: "重构",
            cancelButtonText: "取消",
            type: "warning"
          }
        ), !0;
      } catch {
        return !1;
      }
    }
    async function ct() {
      if (!D()) return;
      const o = M.value;
      if (!o) return;
      const l = o.getAutoGenerateSwitchPlan?.();
      if (!l || !await Yi(l)) return;
      const S = o.autoGenerateSwitches({ plan: l, confirmed: !0 });
      if (!S?.applied) {
        X.info("没有需要生成或重构的道岔");
        return;
      }
      const h = [];
      S.reconstructCount > 0 && h.push(`重构 ${S.reconstructCount} 组`), S.createCount > 0 && h.push(`新增 ${S.createCount} 组`), X.success(`道岔生成完成：${h.join("，")}`);
    }
    function qt() {
      if (!D()) return;
      const o = M.value?.autoGenerateCurves?.() ?? 0;
      X.success(`已生成 ${o} 条曲线`);
    }
    function Pi() {
      D() && (Pt.value = null, be.value = "0", pt.value = !0, Ml.value && (Ml.value.value = ""));
    }
    function gl(o) {
      if (!D()) {
        Pt.value = null, o.target.value = "";
        return;
      }
      const l = o.target.files?.[0];
      if (!l) {
        Pt.value = null;
        return;
      }
      if (!l.name.toLowerCase().endsWith(".dwg")) {
        Pt.value = null, o.target.value = "", X.error("请选择 DWG 格式文件");
        return;
      }
      Pt.value = l;
    }
    function Jo() {
      if (D()) {
        if (!Pt.value) {
          X.warning("请先选择 DWG 文件");
          return;
        }
        tn.value = !0, g.gateway.extractDwgFile({
          file: Pt.value,
          layerName: be.value || "0"
        }).then(async (o) => {
          pt.value = !1;
          const l = o?.layout;
          if (!l) {
            X.error("DWG 提取失败：服务器未返回图形数据");
            return;
          }
          Ri(l), M.value?.clearElements(), ne.value = l?.metadata?.stationSchemeID || ne.value, ol(l?.metadata?.gridSettings), Jt(), await Un(), M.value?.loadDataFromJson(l), vn(l), ot(l), X.success(`DWG 提取完成，共生成 ${o?.segmentCount || 0} 条线段`);
        }).catch((o) => {
          X.error("DWG 提取失败：" + j(o, "未知错误"));
        }).finally(() => {
          tn.value = !1;
        });
      }
    }
    return Ws(() => {
      document.addEventListener("keydown", xi), document.addEventListener("keyup", tl), window.addEventListener("blur", Al), Rt(), Vt();
    }), Gs(() => {
      document.removeEventListener("keydown", xi), document.removeEventListener("keyup", tl), window.removeEventListener("blur", Al);
    }), ma(
      () => g.selectedInstanceId,
      () => {
        ne.value = "", Ne.value = [], ye.value = "", fn(), bi(), vn({}), Rt(), Vt();
      }
    ), ma(
      () => g.readonly,
      (o) => {
        o && (Be = null, Et(), mt.value = !1, Gt.value = !1, pt.value = !1, bn());
      },
      { immediate: !0 }
    ), (o, l) => {
      const r = ke("el-option"), S = ke("el-select"), h = ke("el-button"), E = ke("el-icon"), Q = ke("el-radio-button"), Se = ke("el-radio-group"), ae = ke("el-button-group"), ze = ke("el-dropdown-item"), wt = ke("el-dropdown-menu"), ii = ke("el-dropdown"), jt = ke("el-switch"), oi = ke("el-slider"), Ie = ke("el-input-number"), Pe = ke("el-input"), Ke = ke("el-table-column"), pl = ke("el-table"), Wn = ke("el-dialog"), kn = ke("el-color-picker"), ai = ke("el-tab-pane"), Oi = ke("el-tabs"), hl = ke("el-form"), re = ke("el-form-item"), Nn = ke("el-empty"), qo = ke("el-tag"), si = hu("loading");
      return Dl((w(), N("div", qd, [
        c(Jd, {
          density: _l.value,
          "onUpdate:density": l[24] || (l[24] = (u) => _l.value = u),
          translate: y
        }, {
          context: x(() => [
            v("div", jd, [
              v("span", Hd, _(y("stationLayout.menu.stationScheme")), 1),
              c(S, {
                modelValue: ne.value,
                "onUpdate:modelValue": l[0] || (l[0] = (u) => ne.value = u),
                size: "small",
                filterable: "",
                class: "station-scheme-select",
                loading: tt.value,
                disabled: !g.selectedInstanceId || tt.value || st.value || ee.value,
                placeholder: y("stationLayout.placeholders.selectStationScheme"),
                onChange: $t
              }, {
                default: x(() => [
                  (w(!0), N(P, null, H(Ne.value, (u) => (w(), ce(r, {
                    key: u.id,
                    label: Yn(u),
                    value: u.id
                  }, null, 8, ["label", "value"]))), 128))
                ]),
                _: 1
              }, 8, ["modelValue", "loading", "disabled", "placeholder"]),
              c(h, {
                size: "small",
                icon: J(Au),
                disabled: !g.selectedInstanceId || tt.value || st.value || ee.value,
                onClick: Qt(_o, ["stop"])
              }, {
                default: x(() => [
                  V(_(y("stationLayout.schemeManager.manage")), 1)
                ]),
                _: 1
              }, 8, ["icon", "disabled"])
            ])
          ]),
          primary: x(() => [
            c(Se, {
              modelValue: O.value,
              "onUpdate:modelValue": l[1] || (l[1] = (u) => O.value = u),
              class: "mode-toggle",
              size: "small",
              disabled: g.readonly,
              onChange: fo
            }, {
              default: x(() => [
                c(Q, { value: 0 }, {
                  default: x(() => [
                    c(E, null, {
                      default: x(() => [
                        c(J($u))
                      ]),
                      _: 1
                    }),
                    v("span", null, _(y("stationLayout.mode.select")), 1)
                  ]),
                  _: 1
                }),
                c(Q, { value: 1 }, {
                  default: x(() => [
                    c(E, null, {
                      default: x(() => [
                        c(J(As))
                      ]),
                      _: 1
                    }),
                    v("span", null, _(y("stationLayout.mode.draw")), 1)
                  ]),
                  _: 1
                })
              ]),
              _: 1
            }, 8, ["modelValue", "disabled"]),
            c(ae, null, {
              default: x(() => [
                c(h, {
                  size: "small",
                  icon: J(zu),
                  disabled: g.readonly,
                  onClick: ut
                }, {
                  default: x(() => [
                    V(_(y("stationLayout.menu.undo")), 1)
                  ]),
                  _: 1
                }, 8, ["icon", "disabled"]),
                c(h, {
                  size: "small",
                  icon: J(Fu),
                  disabled: g.readonly,
                  onClick: al
                }, {
                  default: x(() => [
                    V(_(y("stationLayout.menu.redo")), 1)
                  ]),
                  _: 1
                }, 8, ["icon", "disabled"])
              ]),
              _: 1
            })
          ]),
          actions: x(() => [
            c(h, {
              size: "small",
              icon: J($s),
              onClick: uo
            }, {
              default: x(() => [
                V(_(y("stationLayout.tools.fitFullView")), 1)
              ]),
              _: 1
            }, 8, ["icon"]),
            _l.value === "compact" ? (w(), ce(ii, {
              key: 0,
              trigger: "click",
              onCommand: Yo
            }, {
              dropdown: x(() => [
                c(wt, null, {
                  default: x(() => [
                    c(ze, { command: "load" }, {
                      default: x(() => [
                        V(_(y("stationLayout.menu.loadData")), 1)
                      ]),
                      _: 1
                    }),
                    c(ze, {
                      command: "importJson",
                      disabled: g.readonly
                    }, {
                      default: x(() => [...l[95] || (l[95] = [
                        V("导入 JSON", -1)
                      ])]),
                      _: 1
                    }, 8, ["disabled"]),
                    c(ze, { command: "exportJson" }, {
                      default: x(() => [...l[96] || (l[96] = [
                        V("导出 JSON", -1)
                      ])]),
                      _: 1
                    }),
                    c(ze, {
                      command: "extractDwg",
                      disabled: g.readonly
                    }, {
                      default: x(() => [...l[97] || (l[97] = [
                        V("提取 DWG", -1)
                      ])]),
                      _: 1
                    }, 8, ["disabled"])
                  ]),
                  _: 1
                })
              ]),
              default: x(() => [
                c(h, {
                  size: "small",
                  icon: J(to)
                }, {
                  default: x(() => [
                    V(_(y("stationLayout.menu.file")) + " ", 1),
                    c(E, { class: "el-icon--right" }, {
                      default: x(() => [
                        c(J(sa))
                      ]),
                      _: 1
                    })
                  ]),
                  _: 1
                }, 8, ["icon"])
              ]),
              _: 1
            })) : ie("", !0),
            c(h, {
              type: "primary",
              size: "small",
              icon: J(Vs),
              loading: ee.value,
              disabled: g.readonly,
              onClick: Pn
            }, {
              default: x(() => [
                V(_(y("stationLayout.menu.saveData")), 1)
              ]),
              _: 1
            }, 8, ["icon", "loading", "disabled"])
          ]),
          essential: x(() => [
            v("div", Kd, [
              v("span", Zd, _(y("stationLayout.group.drawingObject")), 1),
              c(ae, null, {
                default: x(() => [
                  c(h, {
                    size: "small",
                    icon: J(Iu),
                    disabled: g.readonly || le.value,
                    type: Lt("l"),
                    onClick: l[2] || (l[2] = (u) => vt("l"))
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.draw.line")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled", "type"]),
                  c(h, {
                    size: "small",
                    icon: J(ku),
                    disabled: g.readonly || le.value,
                    type: Lt("n"),
                    onClick: l[3] || (l[3] = (u) => vt("n"))
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.draw.node")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled", "type"]),
                  c(ii, {
                    ref_key: "signalDropdownRef",
                    ref: it,
                    trigger: "click",
                    disabled: g.readonly || le.value,
                    onCommand: Oo
                  }, {
                    dropdown: x(() => [
                      c(wt, { class: "drawing-object-dropdown-menu" }, {
                        default: x(() => [
                          (w(!0), N(P, null, H(Vn.value, (u, ue) => (w(), N(P, {
                            key: u.value
                          }, [
                            u.showLabel ? (w(), ce(ze, {
                              key: 0,
                              disabled: "",
                              class: "drawing-option-group-label",
                              divided: ue > 0
                            }, {
                              default: x(() => [
                                V(_(u.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["divided"])) : ie("", !0),
                            (w(!0), N(P, null, H(u.options, (Yt) => (w(), ce(ze, {
                              key: Yt.value,
                              command: Yt.value
                            }, {
                              default: x(() => [
                                V(_(Yt.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["command"]))), 128))
                          ], 64))), 128))
                        ]),
                        _: 1
                      })
                    ]),
                    default: x(() => [
                      c(h, {
                        size: "small",
                        icon: J(Nu),
                        disabled: g.readonly || le.value,
                        type: Lt("s"),
                        onClick: l[4] || (l[4] = (u) => vt("s"))
                      }, {
                        default: x(() => [
                          v("span", Qd, _(G.value), 1),
                          c(E, { class: "el-icon--right" }, {
                            default: x(() => [
                              c(J(sa))
                            ]),
                            _: 1
                          })
                        ]),
                        _: 1
                      }, 8, ["icon", "disabled", "type"])
                    ]),
                    _: 1
                  }, 8, ["disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(Du),
                    disabled: g.readonly || le.value,
                    type: Lt("w"),
                    onClick: l[5] || (l[5] = (u) => vt("w"))
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.draw.switch")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled", "type"]),
                  c(h, {
                    size: "small",
                    icon: J(Lu),
                    disabled: g.readonly || le.value,
                    type: Lt("i"),
                    onClick: l[6] || (l[6] = (u) => vt("i"))
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.draw.insulation")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled", "type"]),
                  c(h, {
                    size: "small",
                    icon: J(Fs),
                    disabled: g.readonly || le.value,
                    type: Lt("r"),
                    onClick: l[7] || (l[7] = (u) => vt("r"))
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.draw.route")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled", "type"]),
                  c(ii, {
                    trigger: "click",
                    disabled: g.readonly || le.value,
                    onCommand: Wo
                  }, {
                    dropdown: x(() => [
                      c(wt, null, {
                        default: x(() => [
                          (w(!0), N(P, null, H(J(ua), (u, ue) => (w(), N(P, {
                            key: u.value
                          }, [
                            c(ze, {
                              disabled: "",
                              class: "drawing-option-group-label",
                              divided: ue > 0
                            }, {
                              default: x(() => [
                                V(_(u.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["divided"]),
                            (w(!0), N(P, null, H(J(ra), (Yt) => (w(), ce(ze, {
                              key: `${u.value}-${Yt.value}`,
                              command: { type: u.value, direction: Yt.value }
                            }, {
                              default: x(() => [
                                V(_(Yt.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["command"]))), 128))
                          ], 64))), 128))
                        ]),
                        _: 1
                      })
                    ]),
                    default: x(() => [
                      c(h, {
                        size: "small",
                        icon: J(Cu),
                        disabled: g.readonly || le.value,
                        type: Lt("e"),
                        onClick: l[8] || (l[8] = (u) => vt("e"))
                      }, {
                        default: x(() => [
                          v("span", ef, _($n.value), 1),
                          c(E, { class: "el-icon--right" }, {
                            default: x(() => [
                              c(J(sa))
                            ]),
                            _: 1
                          })
                        ]),
                        _: 1
                      }, 8, ["icon", "disabled", "type"])
                    ]),
                    _: 1
                  }, 8, ["disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(Mu),
                    disabled: g.readonly || le.value,
                    type: Lt("p"),
                    onClick: l[9] || (l[9] = (u) => vt("p"))
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.draw.platform")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled", "type"]),
                  c(h, {
                    size: "small",
                    icon: J(As),
                    disabled: g.readonly || le.value,
                    type: Lt("a"),
                    onClick: l[10] || (l[10] = (u) => vt("a"))
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.draw.annotation")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled", "type"])
                ]),
                _: 1
              })
            ]),
            v("div", tf, [
              l[98] || (l[98] = v("span", { class: "station-toolbar-group__label" }, "选择", -1)),
              c(ae, null, {
                default: x(() => [
                  c(h, {
                    size: "small",
                    icon: J(_u),
                    onClick: pn
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.menu.clearSelection")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon"]),
                  c(h, {
                    size: "small",
                    icon: J(Vu),
                    type: "danger",
                    plain: "",
                    disabled: g.readonly,
                    onClick: jl
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.menu.deleteSelection")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled"])
                ]),
                _: 1
              })
            ]),
            v("div", nf, [
              l[100] || (l[100] = v("span", { class: "station-toolbar-group__label" }, "显示", -1)),
              v("div", lf, [
                v("span", of, _(y("stationLayout.menu.showGrid")), 1),
                c(jt, {
                  modelValue: nn.value,
                  "onUpdate:modelValue": l[11] || (l[11] = (u) => nn.value = u),
                  size: "small"
                }, null, 8, ["modelValue"])
              ]),
              v("div", af, [
                l[99] || (l[99] = v("span", { class: "station-toolbar-switch-control__label" }, "节点", -1)),
                c(jt, {
                  modelValue: Vl.value,
                  "onUpdate:modelValue": l[12] || (l[12] = (u) => Vl.value = u),
                  size: "small"
                }, null, 8, ["modelValue"])
              ])
            ]),
            v("div", sf, [
              l[103] || (l[103] = v("span", { class: "station-toolbar-group__label" }, "辅助面板", -1)),
              c(h, {
                size: "small",
                icon: J(Es),
                type: _e.value ? "primary" : "default",
                "aria-pressed": _e.value,
                onClick: Yl
              }, {
                default: x(() => [...l[101] || (l[101] = [
                  V(" 轨道电路区段 ", -1)
                ])]),
                _: 1
              }, 8, ["icon", "type", "aria-pressed"]),
              c(h, {
                size: "small",
                icon: J(aa),
                type: rt.value ? "primary" : "default",
                "aria-pressed": rt.value,
                onClick: l[13] || (l[13] = (u) => rt.value = !rt.value)
              }, {
                default: x(() => [...l[102] || (l[102] = [
                  V(" 设备信息 ", -1)
                ])]),
                _: 1
              }, 8, ["icon", "type", "aria-pressed"])
            ]),
            v("div", rf, [
              v("span", uf, _(y("stationLayout.group.displayScale")), 1),
              v("div", cf, [
                v("span", df, _(y("stationLayout.scale.x")), 1),
                c(oi, {
                  modelValue: Wt.value,
                  "onUpdate:modelValue": l[14] || (l[14] = (u) => Wt.value = u),
                  min: 0.25,
                  max: 4,
                  step: 0.05,
                  size: "small"
                }, null, 8, ["modelValue"]),
                v("span", ff, _($l.value), 1)
              ]),
              v("div", mf, [
                v("span", yf, _(y("stationLayout.scale.y")), 1),
                c(oi, {
                  modelValue: _n.value,
                  "onUpdate:modelValue": l[15] || (l[15] = (u) => _n.value = u),
                  min: 0.25,
                  max: 4,
                  step: 0.05,
                  size: "small"
                }, null, 8, ["modelValue"]),
                v("span", vf, _(yi.value), 1)
              ])
            ])
          ]),
          advanced: x(() => [
            v("div", gf, [
              l[107] || (l[107] = v("span", { class: "station-toolbar-group__label" }, "文件", -1)),
              c(ae, null, {
                default: x(() => [
                  c(h, {
                    size: "small",
                    icon: J(to),
                    onClick: Vt
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.menu.loadData")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon"]),
                  c(h, {
                    size: "small",
                    icon: J(Vs),
                    disabled: g.readonly,
                    onClick: ti
                  }, {
                    default: x(() => [...l[104] || (l[104] = [
                      V("导入 JSON", -1)
                    ])]),
                    _: 1
                  }, 8, ["icon", "disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(to),
                    onClick: ei
                  }, {
                    default: x(() => [...l[105] || (l[105] = [
                      V("导出 JSON", -1)
                    ])]),
                    _: 1
                  }, 8, ["icon"]),
                  c(h, {
                    size: "small",
                    icon: J(to),
                    disabled: g.readonly,
                    onClick: Pi
                  }, {
                    default: x(() => [...l[106] || (l[106] = [
                      V("提取 DWG", -1)
                    ])]),
                    _: 1
                  }, 8, ["icon", "disabled"])
                ]),
                _: 1
              })
            ]),
            v("div", pf, [
              l[108] || (l[108] = v("span", { class: "station-toolbar-group__label" }, "捕捉与网格", -1)),
              v("div", hf, [
                v("span", xf, _(y("stationLayout.menu.gridSnap")), 1),
                c(jt, {
                  modelValue: qe.value,
                  "onUpdate:modelValue": l[16] || (l[16] = (u) => qe.value = u),
                  size: "small",
                  onChange: sl
                }, null, 8, ["modelValue"])
              ]),
              v("div", Sf, [
                v("span", bf, _(y("stationLayout.menu.objectSnap")), 1),
                c(jt, {
                  modelValue: _t.value,
                  "onUpdate:modelValue": l[17] || (l[17] = (u) => _t.value = u),
                  size: "small",
                  onChange: Hl
                }, null, 8, ["modelValue"])
              ]),
              v("div", wf, [
                v("span", If, _(y("stationLayout.menu.snapDistance")), 1),
                c(Ie, {
                  modelValue: hn.value,
                  "onUpdate:modelValue": l[18] || (l[18] = (u) => hn.value = u),
                  size: "small",
                  min: 0,
                  max: 200,
                  step: 1,
                  "controls-position": "right",
                  disabled: !_t.value
                }, null, 8, ["modelValue", "disabled"])
              ]),
              v("div", kf, [
                v("span", Nf, _(y("stationLayout.menu.gridSpacing")), 1),
                c(Ie, {
                  modelValue: ln.value,
                  "onUpdate:modelValue": l[19] || (l[19] = (u) => ln.value = u),
                  size: "small",
                  min: 1,
                  max: 500,
                  step: 1,
                  "controls-position": "right"
                }, null, 8, ["modelValue"])
              ])
            ]),
            v("div", Df, [
              l[113] || (l[113] = v("span", { class: "station-toolbar-group__label" }, "拓扑", -1)),
              c(Se, {
                modelValue: xe.value,
                "onUpdate:modelValue": l[20] || (l[20] = (u) => xe.value = u),
                size: "small",
                disabled: g.readonly
              }, {
                default: x(() => [
                  c(Q, { value: "auto" }, {
                    default: x(() => [...l[109] || (l[109] = [
                      V("自动", -1)
                    ])]),
                    _: 1
                  }),
                  c(Q, { value: "manual" }, {
                    default: x(() => [...l[110] || (l[110] = [
                      V("手动", -1)
                    ])]),
                    _: 1
                  })
                ]),
                _: 1
              }, 8, ["modelValue", "disabled"]),
              c(ae, null, {
                default: x(() => [
                  c(h, {
                    size: "small",
                    icon: J($s),
                    disabled: g.readonly,
                    onClick: Po
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.tools.showCrossPoint")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(bu),
                    disabled: g.readonly,
                    onClick: Ti
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.tools.hideCrossPoint")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(oa),
                    disabled: g.readonly,
                    onClick: ni
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.tools.snapLine")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(wu),
                    disabled: g.readonly,
                    onClick: Xt
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.tools.separateLine")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(zs),
                    disabled: g.readonly,
                    onClick: Go
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.tools.generateNode")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(zs),
                    disabled: g.readonly,
                    onClick: fl
                  }, {
                    default: x(() => [...l[111] || (l[111] = [
                      V("节点合并", -1)
                    ])]),
                    _: 1
                  }, 8, ["icon", "disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(aa),
                    disabled: g.readonly,
                    onClick: ct
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.tools.generateSwitch")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(oa),
                    disabled: g.readonly,
                    onClick: qt
                  }, {
                    default: x(() => [
                      V(_(y("stationLayout.tools.generateCurve")), 1)
                    ]),
                    _: 1
                  }, 8, ["icon", "disabled"]),
                  c(h, {
                    size: "small",
                    icon: J(oa),
                    disabled: g.readonly,
                    onClick: Uo
                  }, {
                    default: x(() => [...l[112] || (l[112] = [
                      V("修正设备绑定", -1)
                    ])]),
                    _: 1
                  }, 8, ["icon", "disabled"])
                ]),
                _: 1
              })
            ]),
            v("div", Lf, [
              l[115] || (l[115] = v("span", { class: "station-toolbar-group__label" }, "路径测试", -1)),
              c(h, {
                size: "small",
                icon: J(Fs),
                type: te.value ? "primary" : "default",
                onClick: Fn
              }, {
                default: x(() => [...l[114] || (l[114] = [
                  V("路径搜索", -1)
                ])]),
                _: 1
              }, 8, ["icon", "type"])
            ]),
            v("div", Cf, [
              l[118] || (l[118] = v("span", { class: "station-toolbar-group__label" }, "显示", -1)),
              v("div", Mf, [
                v("span", _f, _(y("stationLayout.group.curveDisplay")), 1),
                c(jt, {
                  modelValue: Me.value,
                  "onUpdate:modelValue": l[21] || (l[21] = (u) => Me.value = u),
                  size: "small"
                }, null, 8, ["modelValue"]),
                v("span", Vf, _(Me.value ? y("stationLayout.curveDisplay.arc") : y("stationLayout.curveDisplay.tangent")), 1)
              ]),
              v("div", $f, [
                l[116] || (l[116] = v("span", { class: "station-toolbar-switch-control__label" }, "Cell Name", -1)),
                c(jt, {
                  modelValue: qn.value,
                  "onUpdate:modelValue": l[22] || (l[22] = (u) => qn.value = u),
                  size: "small"
                }, null, 8, ["modelValue"])
              ]),
              c(h, {
                size: "small",
                icon: J(aa),
                onClick: l[23] || (l[23] = (u) => on.value = !0)
              }, {
                default: x(() => [...l[117] || (l[117] = [
                  V("显示样式", -1)
                ])]),
                _: 1
              }, 8, ["icon"])
            ])
          ]),
          _: 1
        }, 8, ["density"]),
        v("input", {
          ref_key: "importJsonFileInputRef",
          ref: at,
          type: "file",
          accept: ".json,application/json",
          class: "hidden-file-input",
          disabled: g.readonly,
          onChange: Xo
        }, null, 40, zf),
        c(Wn, {
          modelValue: Cn.value,
          "onUpdate:modelValue": l[28] || (l[28] = (u) => Cn.value = u),
          title: y("stationLayout.schemeManager.title"),
          width: "760px",
          "close-on-click-modal": !1
        }, {
          footer: x(() => [
            c(h, {
              onClick: l[27] || (l[27] = (u) => Cn.value = !1)
            }, {
              default: x(() => [
                V(_(y("stationLayout.schemeManager.close")), 1)
              ]),
              _: 1
            })
          ]),
          default: x(() => [
            v("div", Ff, [
              v("div", Af, [
                c(Pe, {
                  modelValue: Jn.value.name,
                  "onUpdate:modelValue": l[25] || (l[25] = (u) => Jn.value.name = u),
                  size: "small",
                  class: "station-scheme-name-input",
                  disabled: g.readonly,
                  placeholder: y("stationLayout.schemeManager.namePlaceholder")
                }, null, 8, ["modelValue", "disabled", "placeholder"]),
                c(h, {
                  type: "primary",
                  size: "small",
                  loading: we.value,
                  disabled: g.readonly,
                  onClick: Vo
                }, {
                  default: x(() => [
                    V(_(y("stationLayout.schemeManager.add")), 1)
                  ]),
                  _: 1
                }, 8, ["loading", "disabled"])
              ]),
              Dl((w(), ce(pl, {
                data: Ne.value,
                height: "360",
                class: "station-scheme-table"
              }, {
                default: x(() => [
                  c(Ke, {
                    prop: "id",
                    label: y("stationLayout.schemeManager.id"),
                    width: "220"
                  }, {
                    default: x(({ row: u }) => [
                      v("span", null, _(u.id), 1)
                    ]),
                    _: 1
                  }, 8, ["label"]),
                  c(Ke, {
                    prop: "name",
                    label: y("stationLayout.schemeManager.name")
                  }, {
                    default: x(({ row: u }) => [
                      Mn.value === u.id ? (w(), ce(Pe, {
                        key: 0,
                        modelValue: Ot.value.name,
                        "onUpdate:modelValue": l[26] || (l[26] = (ue) => Ot.value.name = ue),
                        size: "small",
                        disabled: g.readonly
                      }, null, 8, ["modelValue", "disabled"])) : (w(), N("span", Ef, _(u.name || u.id), 1))
                    ]),
                    _: 1
                  }, 8, ["label"]),
                  c(Ke, {
                    label: y("stationLayout.schemeManager.operation"),
                    width: "220"
                  }, {
                    default: x(({ row: u }) => [
                      Mn.value === u.id ? (w(), N("div", Bf, [
                        c(h, {
                          type: "success",
                          size: "small",
                          disabled: g.readonly,
                          onClick: zo
                        }, {
                          default: x(() => [
                            V(_(y("stationLayout.schemeManager.save")), 1)
                          ]),
                          _: 1
                        }, 8, ["disabled"]),
                        c(h, {
                          size: "small",
                          onClick: bn
                        }, {
                          default: x(() => [
                            V(_(y("stationLayout.schemeManager.cancel")), 1)
                          ]),
                          _: 1
                        })
                      ])) : (w(), N("div", Rf, [
                        c(h, {
                          type: "primary",
                          size: "small",
                          disabled: g.readonly,
                          onClick: (ue) => $o(u)
                        }, {
                          default: x(() => [
                            V(_(y("stationLayout.schemeManager.edit")), 1)
                          ]),
                          _: 1
                        }, 8, ["disabled", "onClick"]),
                        c(h, {
                          type: "danger",
                          size: "small",
                          disabled: g.readonly,
                          onClick: (ue) => Fo(u)
                        }, {
                          default: x(() => [
                            V(_(y("stationLayout.schemeManager.delete")), 1)
                          ]),
                          _: 1
                        }, 8, ["disabled", "onClick"])
                      ]))
                    ]),
                    _: 1
                  }, 8, ["label"])
                ]),
                _: 1
              }, 8, ["data"])), [
                [si, tt.value || we.value]
              ])
            ])
          ]),
          _: 1
        }, 8, ["modelValue", "title"]),
        c(Wn, {
          modelValue: Gt.value,
          "onUpdate:modelValue": l[29] || (l[29] = (u) => Gt.value = u),
          title: "修正设备绑定节点",
          width: "860px",
          "close-on-click-modal": !1,
          onClosed: yl
        }, {
          footer: x(() => [
            v("div", Pf, [
              v("span", null, "已选择 " + _(kt.value.length) + " / " + _(an.value.length), 1),
              v("div", Of, [
                c(h, { onClick: ml }, {
                  default: x(() => [...l[119] || (l[119] = [
                    V("取消", -1)
                  ])]),
                  _: 1
                }),
                c(h, {
                  type: "primary",
                  disabled: g.readonly || kt.value.length === 0,
                  onClick: vl
                }, {
                  default: x(() => [...l[120] || (l[120] = [
                    V(" 确认修正 ", -1)
                  ])]),
                  _: 1
                }, 8, ["disabled"])
              ])
            ])
          ]),
          default: x(() => [
            v("div", Tf, [
              v("div", Xf, [
                V(" 检测到 " + _(an.value.length) + " 个设备的 binding node 将被修改，请勾选确认需要修正的项。 ", 1),
                Hn.value?.unmatchedCount > 0 ? (w(), N("span", Yf, " 另有 " + _(Hn.value.unmatchedCount) + " 个设备当前位置没有节点，已跳过。 ", 1)) : ie("", !0)
              ]),
              c(pl, {
                ref_key: "bindingCorrectionTableRef",
                ref: jn,
                data: an.value,
                "row-key": "key",
                size: "small",
                height: "360",
                onSelectionChange: Xi
              }, {
                default: x(() => [
                  c(Ke, {
                    type: "selection",
                    width: "48"
                  }),
                  c(Ke, {
                    prop: "kindLabel",
                    label: "设备类型",
                    width: "110"
                  }),
                  c(Ke, {
                    label: "设备",
                    "min-width": "160",
                    "show-overflow-tooltip": ""
                  }, {
                    default: x(({ row: u }) => [
                      v("span", null, _(u.equipmentName || u.equipmentId), 1)
                    ]),
                    _: 1
                  }),
                  c(Ke, {
                    prop: "previousBindingNodeID",
                    label: "原 binding node",
                    width: "150"
                  }),
                  c(Ke, {
                    prop: "nextBindingNodeID",
                    label: "修正为 node",
                    width: "140"
                  }),
                  c(Ke, {
                    label: "设备位置",
                    width: "150"
                  }, {
                    default: x(({ row: u }) => [
                      v("span", null, "(" + _(u.position?.x) + ", " + _(u.position?.y) + ")", 1)
                    ]),
                    _: 1
                  })
                ]),
                _: 1
              }, 8, ["data"])
            ])
          ]),
          _: 1
        }, 8, ["modelValue"]),
        c(Wn, {
          modelValue: on.value,
          "onUpdate:modelValue": l[43] || (l[43] = (u) => on.value = u),
          title: "显示样式配置",
          width: "920px",
          class: "layout-style-dialog",
          "close-on-click-modal": !1
        }, {
          footer: x(() => [
            c(h, {
              disabled: g.readonly,
              onClick: l[41] || (l[41] = (u) => D() && Jl())
            }, {
              default: x(() => [...l[144] || (l[144] = [
                V("恢复默认", -1)
              ])]),
              _: 1
            }, 8, ["disabled"]),
            c(h, {
              type: "primary",
              loading: ee.value,
              disabled: g.readonly,
              onClick: Mo
            }, {
              default: x(() => [...l[145] || (l[145] = [
                V("保存", -1)
              ])]),
              _: 1
            }, 8, ["loading", "disabled"]),
            c(h, {
              onClick: l[42] || (l[42] = (u) => on.value = !1)
            }, {
              default: x(() => [...l[146] || (l[146] = [
                V("关闭", -1)
              ])]),
              _: 1
            })
          ]),
          default: x(() => [
            c(hl, {
              disabled: g.readonly
            }, {
              default: x(() => [
                c(Oi, null, {
                  default: x(() => [
                    c(ai, { label: "文字" }, {
                      default: x(() => [
                        v("div", Wf, [
                          l[121] || (l[121] = v("div", { class: "layout-style-table-header" }, "对象", -1)),
                          l[122] || (l[122] = v("div", { class: "layout-style-table-header" }, "大小", -1)),
                          l[123] || (l[123] = v("div", { class: "layout-style-table-header" }, "字体", -1)),
                          l[124] || (l[124] = v("div", { class: "layout-style-table-header" }, "粗细", -1)),
                          l[125] || (l[125] = v("div", { class: "layout-style-table-header" }, "样式", -1)),
                          l[126] || (l[126] = v("div", { class: "layout-style-table-header" }, "颜色", -1)),
                          (w(), N(P, null, H(zl, (u) => (w(), N(P, {
                            key: u.key
                          }, [
                            v("div", Gf, _(u.label), 1),
                            c(Ie, {
                              modelValue: K.value[u.key].fontSize,
                              "onUpdate:modelValue": (ue) => K.value[u.key].fontSize = ue,
                              size: "small",
                              min: 6,
                              max: 48,
                              step: 1,
                              "controls-position": "right"
                            }, null, 8, ["modelValue", "onUpdate:modelValue"]),
                            c(S, {
                              modelValue: K.value[u.key].fontFamily,
                              "onUpdate:modelValue": (ue) => K.value[u.key].fontFamily = ue,
                              size: "small"
                            }, {
                              default: x(() => [
                                (w(), N(P, null, H(vi, (ue) => c(r, {
                                  key: ue,
                                  label: ue,
                                  value: ue
                                }, null, 8, ["label", "value"])), 64))
                              ]),
                              _: 1
                            }, 8, ["modelValue", "onUpdate:modelValue"]),
                            c(S, {
                              modelValue: K.value[u.key].fontWeight,
                              "onUpdate:modelValue": (ue) => K.value[u.key].fontWeight = ue,
                              size: "small"
                            }, {
                              default: x(() => [
                                (w(), N(P, null, H(gi, (ue) => c(r, {
                                  key: ue.value,
                                  label: ue.label,
                                  value: ue.value
                                }, null, 8, ["label", "value"])), 64))
                              ]),
                              _: 1
                            }, 8, ["modelValue", "onUpdate:modelValue"]),
                            c(S, {
                              modelValue: K.value[u.key].fontStyle,
                              "onUpdate:modelValue": (ue) => K.value[u.key].fontStyle = ue,
                              size: "small"
                            }, {
                              default: x(() => [
                                (w(), N(P, null, H(pi, (ue) => c(r, {
                                  key: ue.value,
                                  label: ue.label,
                                  value: ue.value
                                }, null, 8, ["label", "value"])), 64))
                              ]),
                              _: 1
                            }, 8, ["modelValue", "onUpdate:modelValue"]),
                            c(kn, {
                              modelValue: K.value[u.key].color,
                              "onUpdate:modelValue": (ue) => K.value[u.key].color = ue,
                              size: "small",
                              "show-alpha": ""
                            }, null, 8, ["modelValue", "onUpdate:modelValue"])
                          ], 64))), 64))
                        ])
                      ]),
                      _: 1
                    }),
                    c(ai, { label: "线条与设备" }, {
                      default: x(() => [
                        v("div", Uf, [
                          v("section", Jf, [
                            l[129] || (l[129] = v("h4", null, "轨道线条", -1)),
                            v("div", qf, [
                              l[127] || (l[127] = v("span", null, "粗细", -1)),
                              c(Ie, {
                                modelValue: K.value.track.strokeWidth,
                                "onUpdate:modelValue": l[30] || (l[30] = (u) => K.value.track.strokeWidth = u),
                                size: "small",
                                min: 0.5,
                                max: 12,
                                step: 0.5,
                                "controls-position": "right"
                              }, null, 8, ["modelValue"])
                            ]),
                            v("div", jf, [
                              l[128] || (l[128] = v("span", null, "颜色", -1)),
                              c(kn, {
                                modelValue: K.value.track.color,
                                "onUpdate:modelValue": l[31] || (l[31] = (u) => K.value.track.color = u),
                                size: "small",
                                "show-alpha": ""
                              }, null, 8, ["modelValue"])
                            ])
                          ]),
                          v("section", Hf, [
                            l[132] || (l[132] = v("h4", null, "曲线线条", -1)),
                            v("div", Kf, [
                              l[130] || (l[130] = v("span", null, "粗细", -1)),
                              c(Ie, {
                                modelValue: K.value.curve.strokeWidth,
                                "onUpdate:modelValue": l[32] || (l[32] = (u) => K.value.curve.strokeWidth = u),
                                size: "small",
                                min: 0.5,
                                max: 12,
                                step: 0.5,
                                "controls-position": "right"
                              }, null, 8, ["modelValue"])
                            ]),
                            v("div", Zf, [
                              l[131] || (l[131] = v("span", null, "颜色", -1)),
                              c(kn, {
                                modelValue: K.value.curve.color,
                                "onUpdate:modelValue": l[33] || (l[33] = (u) => K.value.curve.color = u),
                                size: "small",
                                "show-alpha": ""
                              }, null, 8, ["modelValue"])
                            ])
                          ]),
                          v("section", Qf, [
                            l[135] || (l[135] = v("h4", null, "站台线条", -1)),
                            v("div", em, [
                              l[133] || (l[133] = v("span", null, "粗细", -1)),
                              c(Ie, {
                                modelValue: K.value.platform.strokeWidth,
                                "onUpdate:modelValue": l[34] || (l[34] = (u) => K.value.platform.strokeWidth = u),
                                size: "small",
                                min: 0.5,
                                max: 12,
                                step: 0.5,
                                "controls-position": "right"
                              }, null, 8, ["modelValue"])
                            ]),
                            v("div", tm, [
                              l[134] || (l[134] = v("span", null, "颜色", -1)),
                              c(kn, {
                                modelValue: K.value.platform.color,
                                "onUpdate:modelValue": l[35] || (l[35] = (u) => K.value.platform.color = u),
                                size: "small",
                                "show-alpha": ""
                              }, null, 8, ["modelValue"])
                            ])
                          ]),
                          v("section", nm, [
                            l[137] || (l[137] = v("h4", null, "信号机", -1)),
                            v("div", lm, [
                              l[136] || (l[136] = v("span", null, "大小", -1)),
                              c(Ie, {
                                modelValue: K.value.signal.scale,
                                "onUpdate:modelValue": l[36] || (l[36] = (u) => K.value.signal.scale = u),
                                size: "small",
                                min: 0.2,
                                max: 2,
                                step: 0.05,
                                "controls-position": "right"
                              }, null, 8, ["modelValue"])
                            ])
                          ]),
                          v("section", im, [
                            l[140] || (l[140] = v("h4", null, "道岔", -1)),
                            v("div", om, [
                              l[138] || (l[138] = v("span", null, "线条粗细", -1)),
                              c(Ie, {
                                modelValue: K.value.switch.strokeWidth,
                                "onUpdate:modelValue": l[37] || (l[37] = (u) => K.value.switch.strokeWidth = u),
                                size: "small",
                                min: 1,
                                max: 16,
                                step: 0.5,
                                "controls-position": "right"
                              }, null, 8, ["modelValue"])
                            ]),
                            v("div", am, [
                              l[139] || (l[139] = v("span", null, "颜色", -1)),
                              c(kn, {
                                modelValue: K.value.switch.color,
                                "onUpdate:modelValue": l[38] || (l[38] = (u) => K.value.switch.color = u),
                                size: "small",
                                "show-alpha": ""
                              }, null, 8, ["modelValue"])
                            ])
                          ]),
                          v("section", sm, [
                            l[143] || (l[143] = v("h4", null, "节点", -1)),
                            v("div", rm, [
                              l[141] || (l[141] = v("span", null, "大小", -1)),
                              c(Ie, {
                                modelValue: K.value.node.radius,
                                "onUpdate:modelValue": l[39] || (l[39] = (u) => K.value.node.radius = u),
                                size: "small",
                                min: 1,
                                max: 24,
                                step: 1,
                                "controls-position": "right"
                              }, null, 8, ["modelValue"])
                            ]),
                            v("div", um, [
                              l[142] || (l[142] = v("span", null, "颜色", -1)),
                              c(kn, {
                                modelValue: K.value.node.color,
                                "onUpdate:modelValue": l[40] || (l[40] = (u) => K.value.node.color = u),
                                size: "small",
                                "show-alpha": ""
                              }, null, 8, ["modelValue"])
                            ])
                          ])
                        ])
                      ]),
                      _: 1
                    })
                  ]),
                  _: 1
                })
              ]),
              _: 1
            }, 8, ["disabled"])
          ]),
          _: 1
        }, 8, ["modelValue"]),
        me.value ? (w(), N("div", cm, [
          l[147] || (l[147] = v("span", { class: "toolbar-group-label" }, "注释", -1)),
          c(Pe, {
            modelValue: me.value.text,
            "onUpdate:modelValue": l[44] || (l[44] = (u) => me.value.text = u),
            size: "small",
            class: "annotation-text-input",
            disabled: g.readonly,
            onInput: l[45] || (l[45] = (u) => Tt({ text: me.value.text }))
          }, null, 8, ["modelValue", "disabled"]),
          c(S, {
            modelValue: me.value.fontFamily,
            "onUpdate:modelValue": l[46] || (l[46] = (u) => me.value.fontFamily = u),
            size: "small",
            class: "annotation-font-select",
            disabled: g.readonly,
            onChange: l[47] || (l[47] = (u) => Tt({ fontFamily: me.value.fontFamily }))
          }, {
            default: x(() => [
              (w(), N(P, null, H(vi, (u) => c(r, {
                key: u,
                label: u,
                value: u
              }, null, 8, ["label", "value"])), 64))
            ]),
            _: 1
          }, 8, ["modelValue", "disabled"]),
          c(Ie, {
            modelValue: me.value.fontSize,
            "onUpdate:modelValue": l[48] || (l[48] = (u) => me.value.fontSize = u),
            size: "small",
            min: 8,
            max: 96,
            step: 1,
            "controls-position": "right",
            disabled: g.readonly,
            onChange: l[49] || (l[49] = (u) => Tt({ fontSize: me.value.fontSize }))
          }, null, 8, ["modelValue", "disabled"]),
          c(S, {
            modelValue: me.value.fontWeight,
            "onUpdate:modelValue": l[50] || (l[50] = (u) => me.value.fontWeight = u),
            size: "small",
            class: "annotation-small-select",
            disabled: g.readonly,
            onChange: l[51] || (l[51] = (u) => Tt({ fontWeight: me.value.fontWeight }))
          }, {
            default: x(() => [
              (w(), N(P, null, H(gi, (u) => c(r, {
                key: u.value,
                label: u.label,
                value: u.value
              }, null, 8, ["label", "value"])), 64))
            ]),
            _: 1
          }, 8, ["modelValue", "disabled"]),
          c(S, {
            modelValue: me.value.fontStyle,
            "onUpdate:modelValue": l[52] || (l[52] = (u) => me.value.fontStyle = u),
            size: "small",
            class: "annotation-small-select",
            disabled: g.readonly,
            onChange: l[53] || (l[53] = (u) => Tt({ fontStyle: me.value.fontStyle }))
          }, {
            default: x(() => [
              (w(), N(P, null, H(pi, (u) => c(r, {
                key: u.value,
                label: u.label,
                value: u.value
              }, null, 8, ["label", "value"])), 64))
            ]),
            _: 1
          }, 8, ["modelValue", "disabled"]),
          l[148] || (l[148] = v("span", { class: "annotation-field-label" }, "角度", -1)),
          c(Ie, {
            modelValue: me.value.angle,
            "onUpdate:modelValue": l[54] || (l[54] = (u) => me.value.angle = u),
            size: "small",
            min: -180,
            max: 180,
            step: 5,
            "controls-position": "right",
            disabled: g.readonly,
            onChange: l[55] || (l[55] = (u) => Tt({ angle: me.value.angle }))
          }, null, 8, ["modelValue", "disabled"]),
          l[149] || (l[149] = v("span", { class: "annotation-field-label" }, "X", -1)),
          c(Ie, {
            modelValue: me.value.position.x,
            "onUpdate:modelValue": l[56] || (l[56] = (u) => me.value.position.x = u),
            size: "small",
            step: 10,
            "controls-position": "right",
            disabled: g.readonly,
            onChange: In
          }, null, 8, ["modelValue", "disabled"]),
          l[150] || (l[150] = v("span", { class: "annotation-field-label" }, "Y", -1)),
          c(Ie, {
            modelValue: me.value.position.y,
            "onUpdate:modelValue": l[57] || (l[57] = (u) => me.value.position.y = u),
            size: "small",
            step: 10,
            "controls-position": "right",
            disabled: g.readonly,
            onChange: In
          }, null, 8, ["modelValue", "disabled"])
        ])) : ie("", !0),
        v("div", dm, [
          v("div", {
            ref_key: "stationLayoutEditorFrameRef",
            ref: de,
            class: "station-layout-editor-frame"
          }, [
            c(Bd, {
              ref_key: "stationLayoutEditorRef",
              ref: M,
              "display-scale-x": Wt.value,
              "display-scale-y": _n.value,
              "show-curve-arc": Me.value,
              "show-nodes": Vl.value,
              "show-grid": nn.value,
              "object-snap-distance": hn.value,
              readonly: g.readonly,
              "auto-generate-topology": xe.value === "auto",
              "grid-spacing": ln.value,
              "display-styles": K.value,
              "editor-state": Te.value,
              "cell-link-membership-counts": Zn.value,
              cells: Kn.value,
              "show-cell-names": qn.value,
              "route-pick-target": ye.value,
              "highlighted-route-link-ids": Qe.value,
              "highlighted-route-node-ids": yt.value,
              onSelectedAnnotationChange: $i,
              onSelectedEquipmentChange: Ao,
              onRouteNodePick: vo,
              onCellNameClick: xt,
              onTopologyRebuilt: go,
              onDeleteSelectionRequest: jl
            }, null, 8, ["display-scale-x", "display-scale-y", "show-curve-arc", "show-nodes", "show-grid", "object-snap-distance", "readonly", "auto-generate-topology", "grid-spacing", "display-styles", "editor-state", "cell-link-membership-counts", "cells", "show-cell-names", "route-pick-target", "highlighted-route-link-ids", "highlighted-route-node-ids"])
          ], 512),
          _e.value ? (w(), N("aside", fm, [
            v("div", mm, [
              v("div", null, [
                l[151] || (l[151] = v("div", { class: "cell-side-panel-title" }, "轨道电路区段（Cell）", -1)),
                v("div", ym, _(ne.value || "当前方案"), 1)
              ]),
              c(h, {
                text: "",
                size: "small",
                onClick: Yl
              }, {
                default: x(() => [...l[152] || (l[152] = [
                  V("关闭", -1)
                ])]),
                _: 1
              })
            ]),
            v("div", vm, [
              v("section", gm, [
                v("div", pm, [
                  l[156] || (l[156] = v("span", null, "Cell 列表", -1)),
                  v("div", hm, [
                    c(h, {
                      size: "small",
                      icon: J(Es),
                      disabled: g.readonly,
                      onClick: ko
                    }, {
                      default: x(() => [...l[153] || (l[153] = [
                        V("自动生成", -1)
                      ])]),
                      _: 1
                    }, 8, ["icon", "disabled"]),
                    c(h, {
                      size: "small",
                      type: "primary",
                      disabled: g.readonly,
                      onClick: xo
                    }, {
                      default: x(() => [...l[154] || (l[154] = [
                        V("新增", -1)
                      ])]),
                      _: 1
                    }, 8, ["disabled"]),
                    c(h, {
                      size: "small",
                      type: "danger",
                      disabled: g.readonly || !se.value,
                      onClick: lt
                    }, {
                      default: x(() => [...l[155] || (l[155] = [
                        V(" 删除 ", -1)
                      ])]),
                      _: 1
                    }, 8, ["disabled"])
                  ])
                ]),
                c(pl, {
                  data: W.value,
                  class: "cell-table",
                  size: "small",
                  height: "190",
                  "row-key": "id",
                  "highlight-current-row": "",
                  "current-row-key": se.value,
                  onRowClick: wi
                }, {
                  default: x(() => [
                    c(Ke, {
                      label: "ID",
                      width: "112"
                    }, {
                      default: x(({ row: u }) => [
                        V(_(Tl(u)), 1)
                      ]),
                      _: 1
                    }),
                    c(Ke, {
                      prop: "name",
                      label: "Name",
                      "min-width": "120",
                      "show-overflow-tooltip": ""
                    }),
                    c(Ke, {
                      label: "Links",
                      width: "72"
                    }, {
                      default: x(({ row: u }) => [
                        V(_(ho(u)), 1)
                      ]),
                      _: 1
                    })
                  ]),
                  _: 1
                }, 8, ["data", "current-row-key"])
              ]),
              v("section", xm, [
                v("div", Sm, [
                  l[158] || (l[158] = v("span", null, "Cell 信息", -1)),
                  c(h, {
                    size: "small",
                    type: "primary",
                    disabled: g.readonly || !se.value,
                    loading: ee.value,
                    onClick: Wl
                  }, {
                    default: x(() => [...l[157] || (l[157] = [
                      V(" 保存 ", -1)
                    ])]),
                    _: 1
                  }, 8, ["disabled", "loading"])
                ]),
                se.value ? (w(), N("div", bm, [
                  c(hl, {
                    class: "cell-form",
                    "label-width": "116px",
                    size: "small",
                    disabled: g.readonly
                  }, {
                    default: x(() => [
                      c(re, { label: "InstanceID" }, {
                        default: x(() => [
                          c(Pe, {
                            modelValue: pe.value.instanceID,
                            "onUpdate:modelValue": l[58] || (l[58] = (u) => pe.value.instanceID = u),
                            disabled: ""
                          }, null, 8, ["modelValue"])
                        ]),
                        _: 1
                      }),
                      c(re, { label: "StationSchemeID" }, {
                        default: x(() => [
                          c(Pe, {
                            modelValue: pe.value.stationSchemeID,
                            "onUpdate:modelValue": l[59] || (l[59] = (u) => pe.value.stationSchemeID = u),
                            disabled: ""
                          }, null, 8, ["modelValue"])
                        ]),
                        _: 1
                      }),
                      c(re, { label: "ID" }, {
                        default: x(() => [
                          c(Pe, {
                            "model-value": Tl(pe.value),
                            disabled: "",
                            placeholder: "保存后由后端生成"
                          }, null, 8, ["model-value"])
                        ]),
                        _: 1
                      }),
                      c(re, { label: "Name" }, {
                        default: x(() => [
                          c(Pe, {
                            modelValue: pe.value.name,
                            "onUpdate:modelValue": l[60] || (l[60] = (u) => pe.value.name = u)
                          }, null, 8, ["modelValue"])
                        ]),
                        _: 1
                      }),
                      c(re, { label: "LinkIDList" }, {
                        default: x(() => [
                          c(Pe, {
                            modelValue: pe.value.linkIDList,
                            "onUpdate:modelValue": l[61] || (l[61] = (u) => pe.value.linkIDList = u),
                            type: "textarea",
                            rows: 2,
                            onChange: l[62] || (l[62] = (u) => Tn(Xe(pe.value.linkIDList)))
                          }, null, 8, ["modelValue"])
                        ]),
                        _: 1
                      })
                    ]),
                    _: 1
                  }, 8, ["disabled"]),
                  v("div", wm, [
                    l[162] || (l[162] = v("span", { class: "cell-link-toolbar-title" }, "包含 Link", -1)),
                    v("div", Im, [
                      c(h, {
                        size: "small",
                        type: mt.value ? "primary" : "default",
                        disabled: g.readonly,
                        onClick: wo
                      }, {
                        default: x(() => [...l[159] || (l[159] = [
                          V(" 点选新增 ", -1)
                        ])]),
                        _: 1
                      }, 8, ["type", "disabled"]),
                      c(h, {
                        size: "small",
                        disabled: g.readonly || !oe.value,
                        onClick: So
                      }, {
                        default: x(() => [...l[160] || (l[160] = [
                          V(" 移除当前 ", -1)
                        ])]),
                        _: 1
                      }, 8, ["disabled"]),
                      c(h, {
                        size: "small",
                        disabled: g.readonly || He.value.length === 0,
                        onClick: bo
                      }, {
                        default: x(() => [...l[161] || (l[161] = [
                          V(" 清空 ", -1)
                        ])]),
                        _: 1
                      }, 8, ["disabled"])
                    ])
                  ]),
                  He.value.length > 0 ? (w(), ce(Oi, {
                    key: 0,
                    modelValue: oe.value,
                    "onUpdate:modelValue": l[63] || (l[63] = (u) => oe.value = u),
                    class: "cell-link-tabs",
                    type: "card",
                    onTabClick: Je
                  }, {
                    default: x(() => [
                      (w(!0), N(P, null, H(He.value, (u) => (w(), ce(ai, {
                        key: u,
                        name: u,
                        label: ll(u)
                      }, {
                        default: x(() => [
                          v("div", km, [
                            v("div", null, [
                              l[163] || (l[163] = v("span", { class: "cell-link-detail-label" }, "ID", -1)),
                              v("span", Nm, _(u), 1)
                            ]),
                            v("div", null, [
                              l[164] || (l[164] = v("span", { class: "cell-link-detail-label" }, "端点", -1)),
                              v("span", Dm, _(Xn(u)), 1)
                            ])
                          ])
                        ]),
                        _: 2
                      }, 1032, ["name", "label"]))), 128))
                    ]),
                    _: 1
                  }, 8, ["modelValue"])) : (w(), ce(Nn, {
                    key: 1,
                    class: "cell-link-empty",
                    description: "当前 Cell 尚未包含 Link"
                  }))
                ])) : (w(), ce(Nn, {
                  key: 1,
                  class: "cell-empty",
                  description: "请新建或选择一个 Cell"
                }))
              ])
            ])
          ])) : ie("", !0),
          rt.value ? (w(), N("aside", Lm, [
            v("div", Cm, [
              v("div", null, [
                v("div", Mm, _(At.value), 1),
                l[165] || (l[165] = v("div", { class: "equipment-side-panel-subtitle" }, "设备信息", -1))
              ]),
              c(h, {
                text: "",
                size: "small",
                onClick: l[64] || (l[64] = (u) => rt.value = !1)
              }, {
                default: x(() => [...l[166] || (l[166] = [
                  V("关闭", -1)
                ])]),
                _: 1
              })
            ]),
            v("div", _m, [
              je.value ? (w(), ce(hl, {
                key: 0,
                "label-position": "top",
                class: "equipment-form",
                disabled: g.readonly
              }, {
                default: x(() => [
                  c(re, { label: "设备类型" }, {
                    default: x(() => [
                      c(qo, { type: "info" }, {
                        default: x(() => [
                          V(_(Ct[z.value.kind] || "设备"), 1)
                        ]),
                        _: 1
                      })
                    ]),
                    _: 1
                  }),
                  c(re, { label: "ID" }, {
                    default: x(() => [
                      c(Pe, {
                        modelValue: z.value.id,
                        "onUpdate:modelValue": l[65] || (l[65] = (u) => z.value.id = u),
                        disabled: fe.value
                      }, null, 8, ["modelValue", "disabled"])
                    ]),
                    _: 1
                  }),
                  ["link", "signal", "switch", "platform"].includes(z.value.kind) ? (w(), ce(re, {
                    key: 0,
                    label: "Name"
                  }, {
                    default: x(() => [
                      c(Pe, {
                        modelValue: z.value.name,
                        "onUpdate:modelValue": l[66] || (l[66] = (u) => z.value.name = u)
                      }, null, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0),
                  z.value.kind === "link" ? (w(), ce(re, {
                    key: 1,
                    label: "ArrowDirection"
                  }, {
                    default: x(() => [
                      c(S, {
                        modelValue: z.value.arrowDirection,
                        "onUpdate:modelValue": l[67] || (l[67] = (u) => z.value.arrowDirection = u)
                      }, {
                        default: x(() => [
                          (w(), N(P, null, H(dn, (u) => c(r, {
                            key: u.value,
                            label: u.label,
                            value: u.value
                          }, null, 8, ["label", "value"])), 64))
                        ]),
                        _: 1
                      }, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0),
                  z.value.kind === "link" ? (w(), ce(re, {
                    key: 2,
                    label: "ArrowType"
                  }, {
                    default: x(() => [
                      c(S, {
                        modelValue: z.value.arrowType,
                        "onUpdate:modelValue": l[68] || (l[68] = (u) => z.value.arrowType = u)
                      }, {
                        default: x(() => [
                          (w(), N(P, null, H(zn, (u) => c(r, {
                            key: u.value,
                            label: u.label,
                            value: u.value
                          }, null, 8, ["label", "value"])), 64))
                        ]),
                        _: 1
                      }, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0),
                  ["signal", "switch", "insulationJoint"].includes(z.value.kind) ? (w(), ce(re, {
                    key: 3,
                    label: "Type"
                  }, {
                    default: x(() => [
                      c(Pe, {
                        modelValue: z.value.type,
                        "onUpdate:modelValue": l[69] || (l[69] = (u) => z.value.type = u)
                      }, null, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0),
                  z.value.kind === "bufferStop" ? (w(), ce(re, {
                    key: 4,
                    label: "Type"
                  }, {
                    default: x(() => [
                      c(S, {
                        modelValue: z.value.type,
                        "onUpdate:modelValue": l[70] || (l[70] = (u) => z.value.type = u)
                      }, {
                        default: x(() => [
                          (w(!0), N(P, null, H(J(ua), (u) => (w(), ce(r, {
                            key: u.value,
                            label: u.label,
                            value: u.value
                          }, null, 8, ["label", "value"]))), 128))
                        ]),
                        _: 1
                      }, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0),
                  z.value.kind === "signal" ? (w(), ce(re, {
                    key: 5,
                    label: "Direction"
                  }, {
                    default: x(() => [
                      c(S, {
                        modelValue: z.value.direction,
                        "onUpdate:modelValue": l[71] || (l[71] = (u) => z.value.direction = u)
                      }, {
                        default: x(() => [
                          c(r, {
                            label: "e",
                            value: "e"
                          }),
                          c(r, {
                            label: "w",
                            value: "w"
                          }),
                          c(r, {
                            label: "s",
                            value: "s"
                          }),
                          c(r, {
                            label: "d",
                            value: "d"
                          })
                        ]),
                        _: 1
                      }, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0),
                  z.value.kind === "bufferStop" ? (w(), ce(re, {
                    key: 6,
                    label: "Direction"
                  }, {
                    default: x(() => [
                      c(S, {
                        modelValue: z.value.direction,
                        "onUpdate:modelValue": l[72] || (l[72] = (u) => z.value.direction = u)
                      }, {
                        default: x(() => [
                          (w(!0), N(P, null, H(J(ra), (u) => (w(), ce(r, {
                            key: u.value,
                            label: u.label,
                            value: u.value
                          }, null, 8, ["label", "value"]))), 128))
                        ]),
                        _: 1
                      }, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0),
                  ["signal", "switch", "insulationJoint", "bufferStop"].includes(z.value.kind) ? (w(), ce(re, {
                    key: 7,
                    label: "BindingNodeID"
                  }, {
                    default: x(() => [
                      c(Pe, {
                        modelValue: z.value.bindingNodeID,
                        "onUpdate:modelValue": l[73] || (l[73] = (u) => z.value.bindingNodeID = u)
                      }, null, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0),
                  ["signal", "switch", "insulationJoint", "bufferStop"].includes(z.value.kind) ? (w(), N("div", Vm, [
                    c(re, { label: "X" }, {
                      default: x(() => [
                        c(Ie, {
                          modelValue: z.value.x,
                          "onUpdate:modelValue": l[74] || (l[74] = (u) => z.value.x = u),
                          "controls-position": "right",
                          step: 10
                        }, null, 8, ["modelValue"])
                      ]),
                      _: 1
                    }),
                    c(re, { label: "Y" }, {
                      default: x(() => [
                        c(Ie, {
                          modelValue: z.value.y,
                          "onUpdate:modelValue": l[75] || (l[75] = (u) => z.value.y = u),
                          "controls-position": "right",
                          step: 10
                        }, null, 8, ["modelValue"])
                      ]),
                      _: 1
                    })
                  ])) : ie("", !0),
                  z.value.kind === "platform" ? (w(), N("div", $m, [
                    c(re, { label: "X" }, {
                      default: x(() => [
                        c(Ie, {
                          modelValue: z.value.x,
                          "onUpdate:modelValue": l[76] || (l[76] = (u) => z.value.x = u),
                          "controls-position": "right",
                          step: 10
                        }, null, 8, ["modelValue"])
                      ]),
                      _: 1
                    }),
                    c(re, { label: "Y" }, {
                      default: x(() => [
                        c(Ie, {
                          modelValue: z.value.y,
                          "onUpdate:modelValue": l[77] || (l[77] = (u) => z.value.y = u),
                          "controls-position": "right",
                          step: 10
                        }, null, 8, ["modelValue"])
                      ]),
                      _: 1
                    }),
                    c(re, { label: "Width" }, {
                      default: x(() => [
                        c(Ie, {
                          modelValue: z.value.width,
                          "onUpdate:modelValue": l[78] || (l[78] = (u) => z.value.width = u),
                          "controls-position": "right",
                          min: 0,
                          step: 10
                        }, null, 8, ["modelValue"])
                      ]),
                      _: 1
                    }),
                    c(re, { label: "Height" }, {
                      default: x(() => [
                        c(Ie, {
                          modelValue: z.value.height,
                          "onUpdate:modelValue": l[79] || (l[79] = (u) => z.value.height = u),
                          "controls-position": "right",
                          min: 0,
                          step: 10
                        }, null, 8, ["modelValue"])
                      ]),
                      _: 1
                    })
                  ])) : ie("", !0),
                  z.value.kind === "link" ? (w(), N("div", zm, [
                    c(re, { label: "X1" }, {
                      default: x(() => [
                        c(Ie, {
                          modelValue: z.value.x1,
                          "onUpdate:modelValue": l[80] || (l[80] = (u) => z.value.x1 = u),
                          "controls-position": "right",
                          step: 10
                        }, null, 8, ["modelValue"])
                      ]),
                      _: 1
                    }),
                    c(re, { label: "Y1" }, {
                      default: x(() => [
                        c(Ie, {
                          modelValue: z.value.y1,
                          "onUpdate:modelValue": l[81] || (l[81] = (u) => z.value.y1 = u),
                          "controls-position": "right",
                          step: 10
                        }, null, 8, ["modelValue"])
                      ]),
                      _: 1
                    }),
                    c(re, { label: "X2" }, {
                      default: x(() => [
                        c(Ie, {
                          modelValue: z.value.x2,
                          "onUpdate:modelValue": l[82] || (l[82] = (u) => z.value.x2 = u),
                          "controls-position": "right",
                          step: 10
                        }, null, 8, ["modelValue"])
                      ]),
                      _: 1
                    }),
                    c(re, { label: "Y2" }, {
                      default: x(() => [
                        c(Ie, {
                          modelValue: z.value.y2,
                          "onUpdate:modelValue": l[83] || (l[83] = (u) => z.value.y2 = u),
                          "controls-position": "right",
                          step: 10
                        }, null, 8, ["modelValue"])
                      ]),
                      _: 1
                    })
                  ])) : ie("", !0),
                  z.value.kind === "link" ? (w(), ce(re, {
                    key: 11,
                    label: "FromNodeID"
                  }, {
                    default: x(() => [
                      c(Pe, {
                        modelValue: z.value.fromNodeID,
                        "onUpdate:modelValue": l[84] || (l[84] = (u) => z.value.fromNodeID = u)
                      }, null, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0),
                  z.value.kind === "link" ? (w(), ce(re, {
                    key: 12,
                    label: "ToNodeID"
                  }, {
                    default: x(() => [
                      c(Pe, {
                        modelValue: z.value.toNodeID,
                        "onUpdate:modelValue": l[85] || (l[85] = (u) => z.value.toNodeID = u)
                      }, null, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0),
                  z.value.kind === "switch" ? (w(), ce(re, {
                    key: 13,
                    label: "BranchVectorList"
                  }, {
                    default: x(() => [
                      c(Pe, {
                        modelValue: z.value.branchVectorListText,
                        "onUpdate:modelValue": l[86] || (l[86] = (u) => z.value.branchVectorListText = u),
                        type: "textarea",
                        rows: 8
                      }, null, 8, ["modelValue"])
                    ]),
                    _: 1
                  })) : ie("", !0)
                ]),
                _: 1
              }, 8, ["disabled"])) : (w(), ce(Nn, {
                key: 1,
                class: "equipment-empty",
                description: "请选择单个设备"
              }))
            ]),
            v("div", Fm, [
              c(h, {
                onClick: l[87] || (l[87] = (u) => rt.value = !1)
              }, {
                default: x(() => [...l[167] || (l[167] = [
                  V("关闭", -1)
                ])]),
                _: 1
              }),
              c(h, {
                type: "primary",
                disabled: g.readonly || !je.value,
                loading: Ae.value || ee.value,
                onClick: dl
              }, {
                default: x(() => [...l[168] || (l[168] = [
                  V(" 保存 ", -1)
                ])]),
                _: 1
              }, 8, ["disabled", "loading"])
            ])
          ])) : ie("", !0),
          te.value ? (w(), N("aside", Am, [
            v("div", Em, [
              v("div", null, [
                l[169] || (l[169] = v("div", { class: "route-search-panel-title" }, "路径搜索测试", -1)),
                v("div", Bm, _(ne.value || "当前方案"), 1)
              ]),
              c(h, {
                text: "",
                size: "small",
                onClick: Fn
              }, {
                default: x(() => [...l[170] || (l[170] = [
                  V("关闭", -1)
                ])]),
                _: 1
              })
            ]),
            v("div", Rm, [
              v("div", Tm, [
                l[175] || (l[175] = v("label", { class: "route-search-label" }, "起点 Node ID", -1)),
                v("div", Xm, [
                  c(Pe, {
                    modelValue: De.value.startNodeId,
                    "onUpdate:modelValue": l[88] || (l[88] = (u) => De.value.startNodeId = u),
                    size: "small",
                    clearable: ""
                  }, null, 8, ["modelValue"]),
                  c(h, {
                    size: "small",
                    type: ye.value === "start" ? "primary" : "default",
                    onClick: l[89] || (l[89] = (u) => Si("start"))
                  }, {
                    default: x(() => [...l[171] || (l[171] = [
                      V(" 点选 ", -1)
                    ])]),
                    _: 1
                  }, 8, ["type"])
                ]),
                l[176] || (l[176] = v("label", { class: "route-search-label" }, "终点 Node ID", -1)),
                v("div", Ym, [
                  c(Pe, {
                    modelValue: De.value.endNodeId,
                    "onUpdate:modelValue": l[90] || (l[90] = (u) => De.value.endNodeId = u),
                    size: "small",
                    clearable: ""
                  }, null, 8, ["modelValue"]),
                  c(h, {
                    size: "small",
                    type: ye.value === "end" ? "primary" : "default",
                    onClick: l[91] || (l[91] = (u) => Si("end"))
                  }, {
                    default: x(() => [...l[172] || (l[172] = [
                      V(" 点选 ", -1)
                    ])]),
                    _: 1
                  }, 8, ["type"])
                ]),
                v("div", Pm, [
                  c(h, {
                    type: "primary",
                    size: "small",
                    loading: Ee.value,
                    onClick: Di
                  }, {
                    default: x(() => [...l[173] || (l[173] = [
                      V(" 搜索 ", -1)
                    ])]),
                    _: 1
                  }, 8, ["loading"]),
                  c(h, {
                    size: "small",
                    onClick: fn
                  }, {
                    default: x(() => [...l[174] || (l[174] = [
                      V("清空", -1)
                    ])]),
                    _: 1
                  })
                ])
              ]),
              Dl((w(), ce(pl, {
                data: We.value,
                size: "small",
                class: "route-search-table",
                height: "100%",
                "highlight-current-row": "",
                onRowClick: El
              }, {
                default: x(() => [
                  c(Ke, {
                    label: "#",
                    width: "48"
                  }, {
                    default: x(({ row: u }) => [
                      V(_(u.index + 1), 1)
                    ]),
                    _: 1
                  }),
                  c(Ke, {
                    label: "方向",
                    width: "72"
                  }, {
                    default: x(({ row: u }) => [
                      V(_(No(u.direction)), 1)
                    ]),
                    _: 1
                  }),
                  c(Ke, { label: "路径" }, {
                    default: x(({ row: u }) => [
                      v("div", {
                        class: Oe(["route-search-summary", { "is-active": u.index === Ge.value }])
                      }, _(Do(u)), 3)
                    ]),
                    _: 1
                  })
                ]),
                _: 1
              }, 8, ["data"])), [
                [si, Ee.value]
              ])
            ])
          ])) : ie("", !0)
        ]),
        c(Wn, {
          modelValue: pt.value,
          "onUpdate:modelValue": l[94] || (l[94] = (u) => pt.value = u),
          title: "从DWG文件提取",
          width: "420px",
          "close-on-click-modal": !1
        }, {
          footer: x(() => [
            c(h, {
              onClick: l[93] || (l[93] = (u) => pt.value = !1)
            }, {
              default: x(() => [...l[179] || (l[179] = [
                V("取消", -1)
              ])]),
              _: 1
            }),
            c(h, {
              type: "primary",
              loading: tn.value,
              disabled: g.readonly,
              onClick: Jo
            }, {
              default: x(() => [...l[180] || (l[180] = [
                V(" 上传并提取 ", -1)
              ])]),
              _: 1
            }, 8, ["loading", "disabled"])
          ]),
          default: x(() => [
            v("div", Om, [
              l[177] || (l[177] = v("label", { class: "dwg-extract-label" }, "DWG 文件", -1)),
              v("input", {
                ref_key: "dwgFileInputRef",
                ref: Ml,
                type: "file",
                accept: ".dwg",
                disabled: g.readonly,
                onChange: gl
              }, null, 40, Wm),
              l[178] || (l[178] = v("label", { class: "dwg-extract-label" }, "图层名称", -1)),
              c(Pe, {
                modelValue: be.value,
                "onUpdate:modelValue": l[92] || (l[92] = (u) => be.value = u),
                disabled: g.readonly,
                placeholder: "请输入要提取的图层名称"
              }, null, 8, ["modelValue", "disabled"])
            ])
          ]),
          _: 1
        }, 8, ["modelValue"]),
        l[181] || (l[181] = v("div", { id: "equipmentinfolist" }, null, -1))
      ])), [
        [si, st.value || ee.value]
      ]);
    };
  }
}, Jm = /* @__PURE__ */ ba(Um, [["__scopeId", "data-v-88efb177"]]), Zm = /* @__PURE__ */ Us({
  inheritAttrs: !1,
  __name: "StationLayoutPublic",
  props: {
    selectedInstanceId: {
      type: String,
      default: ""
    },
    gateway: {
      type: Object,
      required: !0
    },
    translate: {
      type: Function,
      default: void 0
    },
    formatError: {
      type: Function,
      default: void 0
    },
    readonly: {
      type: Boolean,
      default: !1
    }
  },
  setup($) {
    const g = $;
    return (F, y) => (w(), ce(Jm, fi({
      "selected-instance-id": g.selectedInstanceId,
      gateway: g.gateway,
      translate: g.translate,
      "format-error": g.formatError,
      readonly: g.readonly
    }, F.$attrs), null, 16, ["selected-instance-id", "gateway", "translate", "format-error", "readonly"]));
  }
});
export {
  Ll as DEFAULT_BUFFER_STOP_DIRECTION,
  en as DEFAULT_BUFFER_STOP_TYPE,
  mi as DEFAULT_SIGNAL_TYPE,
  Zm as StationLayout,
  Jd as StationLayoutEditToolbar,
  Bd as StationLayoutEditor,
  ra as bufferStopDirectionOptions,
  va as bufferStopStyleAssets,
  ua as bufferStopTypeOptions,
  js as createStationLayoutTranslator,
  Ru as getBufferStopStyleAsset,
  Sc as getSignalStyleAsset,
  no as normalizeBufferStopDirection,
  so as normalizeBufferStopType,
  Sa as normalizeSignalType,
  Ts as signalStyleAssets,
  pc as signalTypeMenuOptions,
  Km as signalTypeOptions,
  Rd as stationLayoutMessages
};
//# sourceMappingURL=index.js.map
