// Interactive SVG maps (world map + extension tree): wheel to zoom (toward the cursor), drag to pan.
// Loaded on every page but only acts when a .zonemap-wrap is present, so the
// rest of the site is untouched. Zoom is a CSS transform on the SVG, which
// keeps pointer hit-testing (clicks, hover tooltips) accurate — unlike an SVG
// viewBox change, which would also rescale stroke widths and labels.
(function () {
    'use strict';
    // One zoom/pan wrapper for every interactive SVG: the world map
    // (.zonemap-wrap) and the extension tree (.map-zoom-wrap).
    function scan() {
        var wraps = document.querySelectorAll('.zonemap-wrap, .map-zoom-wrap');
        for (var w = 0; w < wraps.length; w++) attach(wraps[w]);
    }
    // map.js loads in <head> without defer — at that point the DOM (and
    // document.body) does not exist yet, so boot on DOMContentLoaded instead
    // of running immediately (running immediately used to throw on
    // document.body === null and the zoom/pan handlers never attached).
    function boot() {
        scan();
        if (window.MutationObserver && document.body) {
            // Re-scan when the DOM changes (covers late-rendered content).
            new MutationObserver(scan).observe(document.body, { childList: true, subtree: true });
        }
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }

    function attach(wrap) {
    if (wrap.getAttribute('data-zoom-init')) return;
    wrap.setAttribute('data-zoom-init', '1');
    var svg = wrap.querySelector('.zonemap, .zoommap');
    if (!svg) return;
    // s = 1 is "max zoom out": the whole map is visible. MAX keeps zoom-in
    // useful — at 8x the map is already far beyond the level of detail it
    // carries.
    var MIN = 1, MAX = 8;
    var s = 1, tx = 0, ty = 0;

    function apply() {
        svg.style.transform = 'translate(' + tx + 'px,' + ty + 'px) scale(' + s + ')';
    }

    // Rendered size of the map content inside the svg/img element. The SVG
    // is letterboxed (preserveAspectRatio meet) inside its box; an <img>
    // fills its content box. Padding does not matter: the content is
    // centered in the element either way.
    function contentSize() {
        // clientWidth/Height are the layout (untransformed) element sizes —
        // getBoundingClientRect would include the current scale.
        var st = window.getComputedStyle(svg);
        var vw = svg.clientWidth - parseFloat(st.paddingLeft) - parseFloat(st.paddingRight);
        var vh = svg.clientHeight - parseFloat(st.paddingTop) - parseFloat(st.paddingBottom);
        if (svg.tagName === 'svg' && svg.viewBox && svg.viewBox.baseVal.width) {
            var vb = svg.viewBox.baseVal;
            var k = Math.min(vw / vb.width, vh / vb.height);
            return { w: vb.width * k, h: vb.height * k };
        }
        return { w: vw, h: vh };
    }

    function limit(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }

    // Overlap length of [0, len] with the content span centered at offset t.
    function overlapLen(len, size, t) {
        var a = Math.max(0, (len - size) / 2 + t);
        var b = Math.min(len, (len - size) / 2 + t + size);
        return Math.max(0, b - a);
    }

    // Pan limits: panning may show empty space around the map, but at least
    // MIN_VISIBLE of the viewport must stay covered by map content (overlap
    // area, shared between both axes). This also applies at max zoom out —
    // the map can be panned, just not further out than that. (For a span of
    // size >= need inside [0, len], overlap >= need  <=>  the span starts
    // at or before len - need and ends at or after need.)
    var MIN_VISIBLE = 0.25;

    function clamp() {
        var rect = wrap.getBoundingClientRect();
        var W = rect.width, H = rect.height;
        var c = contentSize();
        var sw = s * c.w, sh = s * c.h;
        // Two passes so both axes respect the shared area budget.
        for (var pass = 0; pass < 2; pass++) {
            var needX = MIN_VISIBLE * W * H / Math.max(overlapLen(H, sh, ty), 1e-6);
            if (sw >= needX) {
                var x0 = (W - sw) / 2;
                tx = limit(tx, needX - sw - x0, W - needX - x0);
            } else {
                tx = 0;
            }
            var needY = MIN_VISIBLE * W * H / Math.max(overlapLen(W, sw, tx), 1e-6);
            if (sh >= needY) {
                var y0 = (H - sh) / 2;
                ty = limit(ty, needY - sh - y0, H - needY - y0);
            } else {
                ty = 0;
            }
        }
    }

    // Zoom about an arbitrary screen point (the cursor, or the midpoint of
    // two touch fingers). The CSS transform is translate(tx,ty) scale(s)
    // about the element center; with rc the *transformed* element center
    // (getBoundingClientRect includes the transform) and E0 = rc - t the
    // untransformed one, a content point p sits at E0 + t + s*p. Keeping the
    // point under the screen point C fixed: p = (C - rc)/s and
    // t' = C - E0 - ns*p = C - rc + t - ns*p.
    function zoomAt(clientX, clientY, factor) {
        var rect = svg.getBoundingClientRect();
        var rcx = rect.left + rect.width / 2;
        var rcy = rect.top + rect.height / 2;
        var ns = Math.min(MAX, Math.max(MIN, s * factor));
        if (ns === s) return;
        var contentX = (clientX - rcx) / s;
        var contentY = (clientY - rcy) / s;
        tx = tx + clientX - rcx - ns * contentX;
        ty = ty + clientY - rcy - ns * contentY;
        s = ns;
        clamp();
        apply();
    }

    // Wheel = zoom (no key needed). The listener is non-passive so the page
    // scroll is suppressed while the pointer is over the map; drag = pan.
    wrap.addEventListener('wheel', function (e) {
        e.preventDefault();
        var factor = Math.exp(-e.deltaY * 0.0015);
        zoomAt(e.clientX, e.clientY, factor);
    }, { passive: false });

    // Two-finger pinch on touch devices.
    var pinch = null;
    function dist(e) {
        var d = 0;
        for (var i = 0; i + 1 < e.touches.length; i += 2) {
            var dx = e.touches[i + 1].clientX - e.touches[i].clientX;
            var dy = e.touches[i + 1].clientY - e.touches[i].clientY;
            d += Math.sqrt(dx * dx + dy * dy);
        }
        return d;
    }
    wrap.addEventListener('touchstart', function (e) {
        if (e.touches.length === 2) { pinch = dist(e); dragging = false; }
    }, { passive: true });
    wrap.addEventListener('touchmove', function (e) {
        if (pinch !== null && e.touches.length === 2) {
            e.preventDefault();
            var d = dist(e);
            if (d > 0 && pinch > 0) {
                // Free two-finger zoom: the anchor is the midpoint between
                // the fingers, so the map follows the pinch position.
                var mx = (e.touches[0].clientX + e.touches[1].clientX) / 2;
                var my = (e.touches[0].clientY + e.touches[1].clientY) / 2;
                zoomAt(mx, my, d / pinch);
            }
            pinch = d;
        }
    }, { passive: false });
    wrap.addEventListener('touchend', function () { pinch = null; }, { passive: true });

    // Pan handlers live on the wrapper, not the svg: zoomed in and panned,
    // the (transformed) svg can leave empty areas inside the panel, and a
    // drag started there should still pan the map.
    var dragging = false, moved = false, lx = 0, ly = 0;
    wrap.addEventListener('pointerdown', function (e) {
        if (e.target.closest && e.target.closest('button')) return; // reset button
        dragging = true; moved = false; lx = e.clientX; ly = e.clientY;
        wrap.setPointerCapture(e.pointerId);
        svg.classList.add('dragging');
    });
    wrap.addEventListener('pointermove', function (e) {
        if (!dragging) return;
        var dx = e.clientX - lx, dy = e.clientY - ly;
        if (Math.abs(dx) + Math.abs(dy) > 2) moved = true;
        tx += dx; ty += dy;
        lx = e.clientX; ly = e.clientY;
        clamp(); // keep at least MIN_VISIBLE of the viewport covered
        apply();
    });
    function endDrag(e) {
        if (!dragging) return;
        dragging = false;
        svg.classList.remove('dragging');
        try { wrap.releasePointerCapture(e.pointerId); } catch (err) { /* already released */ }
        if (moved && e.type === 'pointerup') e.preventDefault();
    }
    wrap.addEventListener('pointerup', endDrag);
    wrap.addEventListener('pointercancel', endDrag);
    // Suppress the click-through to the zone link right after a drag.
    wrap.addEventListener('click', function (e) {
        if (moved) { e.preventDefault(); e.stopPropagation(); moved = false; }
    }, true);

    var reset = wrap.querySelector('.zonemap-reset, .zoommap-reset');
    if (reset) {
        reset.addEventListener('click', function () {
            s = 1; tx = 0; ty = 0;
            apply();
        });
    }
    apply();
    }
})();
