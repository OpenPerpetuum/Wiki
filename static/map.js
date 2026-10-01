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
    var MIN = 1, MAX = 20;
    var s = 1, tx = 0, ty = 0;

    function apply() {
        svg.style.transform = 'translate(' + tx + 'px,' + ty + 'px) scale(' + s + ')';
    }

    function zoomAt(clientX, clientY, factor) {
        var rect = svg.getBoundingClientRect();
        var cx = rect.width / 2, cy = rect.height / 2;
        var px = clientX - rect.left - cx;
        var py = clientY - rect.top - cy;
        var ns = Math.min(MAX, Math.max(MIN, s * factor));
        if (ns === s) return;
        // Keep the point under the cursor fixed. The CSS transform is
        // translate(tx,ty) scale(s) about the element center, so the cursor's
        // content-space point is (px - tx)/s and must satisfy
        // tx' = cx - ((px - tx)/s) * ns.
        var contentX = (px - tx) / s;
        var contentY = (py - ty) / s;
        tx = cx - contentX * ns;
        ty = cy - contentY * ns;
        s = ns;
        clamp();
        apply();
    }

    function clamp() {
        var rect = wrap.getBoundingClientRect();
        // Content must stay within one screenful of the viewport.
        var mx = rect.width, my = rect.height;
        tx = Math.min(mx, Math.max(-mx, tx));
        ty = Math.min(my, Math.max(-my, ty));
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
                var rect = wrap.getBoundingClientRect();
                zoomAt(rect.left + rect.width / 2, rect.top + rect.height / 2, d / pinch);
            }
            pinch = d;
        }
    }, { passive: false });
    wrap.addEventListener('touchend', function () { pinch = null; }, { passive: true });

    var dragging = false, moved = false, lx = 0, ly = 0;
    svg.addEventListener('pointerdown', function (e) {
        dragging = true; moved = false; lx = e.clientX; ly = e.clientY;
        svg.setPointerCapture(e.pointerId);
        svg.classList.add('dragging');
    });
    svg.addEventListener('pointermove', function (e) {
        if (!dragging) return;
        var dx = e.clientX - lx, dy = e.clientY - ly;
        if (Math.abs(dx) + Math.abs(dy) > 2) moved = true;
        tx += dx; ty += dy;
        lx = e.clientX; ly = e.clientY;
        apply();
    });
    function endDrag(e) {
        if (!dragging) return;
        dragging = false;
        svg.classList.remove('dragging');
        try { svg.releasePointerCapture(e.pointerId); } catch (err) { /* already released */ }
        if (moved && e.type === 'pointerup') e.preventDefault();
    }
    svg.addEventListener('pointerup', endDrag);
    svg.addEventListener('pointercancel', endDrag);
    // Suppress the click-through to the zone link right after a drag.
    svg.addEventListener('click', function (e) {
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
