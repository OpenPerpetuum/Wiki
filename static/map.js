// Interactive SVG maps + diagrams: wheel to zoom (toward the cursor), drag
// to pan, two-finger pinch on touch. Covers the world map (.zonemap-wrap),
// the extension tree (.map-zoom-wrap) and mermaid diagrams (.mermaid) —
// all with the same rules. Loaded on every page but only acts where one of
// those exists. Zoom is a CSS transform on the SVG, which keeps pointer
// hit-testing (clicks, hover tooltips) accurate — unlike an SVG viewBox
// change, which would also rescale stroke widths and labels.
(function () {
    'use strict';
    // s = 1 is "max zoom out": the whole map/diagram is visible. MAX keeps
    // zoom-in useful. MIN_VISIBLE: panning may show empty space, but at
    // least this share of the viewport stays covered by content (overlap
    // area, shared between both axes).
    var MIN = 1, MAX = 8, MIN_VISIBLE = 0.25;

    function limit(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }

    // Overlap length of [0, len] with the content span centered at offset t.
    function overlapLen(len, size, t) {
        var a = Math.max(0, (len - size) / 2 + t);
        var b = Math.min(len, (len - size) / 2 + t + size);
        return Math.max(0, b - a);
    }

    // Per-wrap zoom state, on the element so it survives mermaid re-renders
    // (theme toggle) replacing the svg inside the same box.
    function stateOf(wrap) {
        if (!wrap._zoomState) wrap._zoomState = { s: 1, tx: 0, ty: 0 };
        return wrap._zoomState;
    }

    function applyWrap(wrap) {
        var svg = wrap._zoomSvg, st = stateOf(wrap);
        if (svg) svg.style.transform = 'translate(' + st.tx + 'px,' + st.ty + 'px) scale(' + st.s + ')';
    }

    // Rendered size of the content inside the svg element. The SVG is
    // letterboxed (preserveAspectRatio meet) inside its box.
    function contentSize(wrap) {
        // clientWidth/Height are the layout (untransformed) element sizes —
        // getBoundingClientRect would include the current scale.
        var svg = wrap._zoomSvg;
        var st = window.getComputedStyle(svg);
        var vw = svg.clientWidth - parseFloat(st.paddingLeft) - parseFloat(st.paddingRight);
        var vh = svg.clientHeight - parseFloat(st.paddingTop) - parseFloat(st.paddingBottom);
        if (svg.viewBox && svg.viewBox.baseVal.width) {
            var vb = svg.viewBox.baseVal;
            var k = Math.min(vw / vb.width, vh / vb.height);
            return { w: vb.width * k, h: vb.height * k };
        }
        return { w: vw, h: vh };
    }

    function clamp(wrap) {
        var st = stateOf(wrap);
        var rect = wrap.getBoundingClientRect();
        var W = rect.width, H = rect.height;
        var c = contentSize(wrap);
        var sw = st.s * c.w, sh = st.s * c.h;
        // Two passes so both axes respect the shared area budget.
        for (var pass = 0; pass < 2; pass++) {
            var needX = MIN_VISIBLE * W * H / Math.max(overlapLen(H, sh, st.ty), 1e-6);
            if (sw >= needX) {
                var x0 = (W - sw) / 2;
                st.tx = limit(st.tx, needX - sw - x0, W - needX - x0);
            } else {
                st.tx = 0;
            }
            var needY = MIN_VISIBLE * W * H / Math.max(overlapLen(W, sw, st.tx), 1e-6);
            if (sh >= needY) {
                var y0 = (H - sh) / 2;
                st.ty = limit(st.ty, needY - sh - y0, H - needY - y0);
            } else {
                st.ty = 0;
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
    function zoomAt(wrap, clientX, clientY, factor) {
        var svg = wrap._zoomSvg, st = stateOf(wrap);
        var rect = svg.getBoundingClientRect();
        var rcx = rect.left + rect.width / 2;
        var rcy = rect.top + rect.height / 2;
        var ns = Math.min(MAX, Math.max(MIN, st.s * factor));
        if (ns === st.s) return;
        var contentX = (clientX - rcx) / st.s;
        var contentY = (clientY - rcy) / st.s;
        st.tx = st.tx + clientX - rcx - ns * contentX;
        st.ty = st.ty + clientY - rcy - ns * contentY;
        st.s = ns;
        clamp(wrap);
        applyWrap(wrap);
    }

    // Pie charts display non-interactively: the wheel keeps scrolling the
    // page, and there is no drag pan, pinch, or reset button. This mermaid
    // build leaves the svg root unclassed, so detect from the pie-specific
    // inner elements once rendered, or — before the render, when the wrap's
    // text is still the diagram source — from the first token of the source.
    function pieNow(wrap) {
        if (wrap._zmPieKnown) return true;
        var s = wrap._zoomSvg || wrap.querySelector('svg');
        var pie = !!(s && s.querySelector('.pieOuterCircle, .pieCircle, .pieTitleText'));
        if (!pie) {
            var txt = (wrap.textContent || '').trim();
            pie = txt.indexOf('pie') === 0 && (txt.length === 3 || /\s/.test(txt.charAt(3)));
        }
        // Cache only a positive result: mermaid inserts the svg shell before
        // its content, so a negative check on the shell would cache forever.
        if (pie) wrap._zmPieKnown = 1;
        return pie;
    }

    // Reset button: present in the map wrappers; created for mermaid boxes.
    // Re-called on every re-render, because mermaid replaces the wrap's
    // contents (and with them any button we appended) when it re-renders a
    // diagram — e.g. on a theme toggle.
    function ensureReset(wrap) {
        var reset = wrap.querySelector('.zonemap-reset, .zoommap-reset');
        if (!reset && wrap.classList.contains('mermaid')) {
            reset = document.createElement('button');
            reset.type = 'button';
            reset.className = 'zonemap-reset';
            reset.title = 'Reset the zoom';
            reset.textContent = '⟲';
            wrap.appendChild(reset);
        }
        if (reset && !reset.getAttribute('data-zr-bound')) {
            reset.setAttribute('data-zr-bound', '1');
            reset.addEventListener('click', function () {
                var st = stateOf(wrap);
                st.s = 1; st.tx = 0; st.ty = 0;
                applyWrap(wrap);
            });
        }
    }

    function attach(wrap) {
        var svg = wrap.querySelector('svg');
        if (!svg) return; // mermaid boxes: the svg appears later; re-scanned
        if (wrap.classList.contains('mermaid') && (wrap._zmPie || pieNow(wrap))) {
            return; // pie charts: no zoom/pan — the wheel keeps scrolling the page
        }
        if (wrap.getAttribute('data-zoom-init')) return;
        wrap.setAttribute('data-zoom-init', '1');
        wrap._zoomSvg = svg;

        // Wheel = zoom (no key needed). The listener is non-passive so the
        // page scroll is suppressed while the pointer is over the map.
        wrap.addEventListener('wheel', function (e) {
            // Re-check in case the pie became detectable only after the
            // render (a pie detected late must not swallow the wheel).
            if (wrap.classList.contains('mermaid') && pieNow(wrap)) return;
            e.preventDefault();
            zoomAt(wrap, e.clientX, e.clientY, Math.exp(-e.deltaY * 0.0015));
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
            if (pinch !== null && e.touches.length === 2 &&
                !(wrap.classList.contains('mermaid') && pieNow(wrap))) {
                e.preventDefault();
                var d = dist(e);
                if (d > 0 && pinch > 0) {
                    // Free two-finger zoom: the anchor is the midpoint
                    // between the fingers, so the map follows the pinch.
                    var mx = (e.touches[0].clientX + e.touches[1].clientX) / 2;
                    var my = (e.touches[0].clientY + e.touches[1].clientY) / 2;
                    zoomAt(wrap, mx, my, d / pinch);
                }
                pinch = d;
            }
        }, { passive: false });
        wrap.addEventListener('touchend', function () { pinch = null; }, { passive: true });

        // Pan handlers live on the wrapper, not the svg: zoomed in and
        // panned, the (transformed) svg can leave empty areas inside the
        // panel, and a drag started there should still pan the map.
        var dragging = false, moved = false, lx = 0, ly = 0;
        wrap.addEventListener('pointerdown', function (e) {
            // consume the stale flag from the previous gesture: the drag's
            // own click echo is already gone (pointerup was prevented), so a
            // new pointerdown means the next click is a real one (e.g. the
            // reset button right after panning)
            moved = false;
            if (e.target.closest && e.target.closest('button')) return; // reset button
            if (wrap.classList.contains('mermaid') && pieNow(wrap)) return;
            dragging = true; moved = false; lx = e.clientX; ly = e.clientY;
            svg.classList.add('dragging');
        });
        wrap.addEventListener('pointermove', function (e) {
            if (!dragging) return;
            var dx = e.clientX - lx, dy = e.clientY - ly;
            if (!moved) {
                if (Math.abs(dx) + Math.abs(dy) <= 2) return; // still a click
                // capture only once it is really a drag — capturing on
                // pointerdown would retarget the pointerup to the wrapper
                // and the derived click event would never reach the <a>
                // elements inside the map (teleport links would be
                // unclickable)
                moved = true;
                try { wrap.setPointerCapture(e.pointerId); } catch (err) { /* no capture */ }
                return;
            }
            var st = stateOf(wrap);
            st.tx += dx; st.ty += dy;
            lx = e.clientX; ly = e.clientY;
            clamp(wrap); // keep at least MIN_VISIBLE of the viewport covered
            applyWrap(wrap);
        });
        function endDrag(e) {
            if (!dragging) return;
            dragging = false;
            svg.classList.remove('dragging');
            if (moved) {
                try { wrap.releasePointerCapture(e.pointerId); } catch (err) { /* already released */ }
                if (e.type === 'pointerup') e.preventDefault();
            }
        }
        wrap.addEventListener('pointerup', endDrag);
        wrap.addEventListener('pointercancel', endDrag);
        // Suppress the click-through to the zone link right after a drag.
        wrap.addEventListener('click', function (e) {
            if (moved) { e.preventDefault(); e.stopPropagation(); moved = false; }
        }, true);

        attachLtp(wrap);
        ensureReset(wrap);
        applyWrap(wrap);
    }

    // Local (in-zone) teleports: every .ltp-line is hidden until the cursor
    // comes near one of its endpoint markers — the .ltp-dot circles, or the
    // teleport-column circles that carry the same data-ltp token — then it
    // stays lit (hysteresis, so it does not flicker at the edge) while the
    // cursor stays in the vicinity. The dots are tiny at the default zoom
    // and the effect really shows zoomed in; the threshold is in screen px,
    // so it tracks the CSS-transform zoom automatically (getBoundingClientRect
    // of the markers includes the current transform).
    function attachLtp(wrap) {
        var lines = wrap.querySelectorAll('.ltp-line');
        if (!lines.length) return;
        var circles = wrap.querySelectorAll('circle[data-ltp]');
        var pairs = [];
        for (var i = 0; i < lines.length; i++) {
            var id = lines[i].getAttribute('data-ltp');
            var anchors = [];
            for (var j = 0; j < circles.length; j++) {
                var toks = (circles[j].getAttribute('data-ltp') || '').split(/\s+/);
                if (toks.indexOf(id) >= 0) anchors.push(circles[j]);
            }
            if (anchors.length) pairs.push({ line: lines[i], anchors: anchors, on: false });
        }
        if (!pairs.length) return;
        var SHOW = 26, KEEP = 44; // screen px (show / keep-lit radius)
        var raf = 0, cx = 0, cy = 0;
        function update() {
            raf = 0;
            for (var i = 0; i < pairs.length; i++) {
                var p = pairs[i];
                var best = Infinity;
                for (var j = 0; j < p.anchors.length; j++) {
                    var r = p.anchors[j].getBoundingClientRect();
                    var dx = cx - (r.left + r.width / 2);
                    var dy = cy - (r.top + r.height / 2);
                    var d = Math.sqrt(dx * dx + dy * dy);
                    if (d < best) best = d;
                }
                var on = best < SHOW ? true : (p.on ? best <= KEEP : false);
                if (on !== p.on) {
                    p.on = on;
                    if (on) p.line.classList.add('ltp-on');
                    else p.line.classList.remove('ltp-on');
                }
            }
        }
        wrap.addEventListener('pointermove', function (e) {
            cx = e.clientX; cy = e.clientY;
            if (!raf) raf = requestAnimationFrame(update);
        });
        wrap.addEventListener('pointerleave', function () {
            for (var i = 0; i < pairs.length; i++) {
                if (pairs[i].on) {
                    pairs[i].on = false;
                    pairs[i].line.classList.remove('ltp-on');
                }
            }
        });
    }

    function scan() {
        var wraps = document.querySelectorAll('.zonemap-wrap, .map-zoom-wrap, .mermaid');
        for (var w = 0; w < wraps.length; w++) {
            var wrap = wraps[w];
            var s = wrap.querySelector('svg');
            if (!s && wrap.classList.contains('mermaid')) {
                // Not rendered yet: the wrap's text is still the diagram
                // source — remember pie charts now, before mermaid replaces
                // the text with the rendered diagram.
                var txt = (wrap.textContent || '').trim();
                if (txt.indexOf('pie') === 0 && (txt.length === 3 || /\s/.test(txt.charAt(3)))) {
                    wrap._zmPie = 1;
                }
                continue;
            }
            if (wrap.classList.contains('mermaid') && s &&
                wrap.getAttribute('data-zoom-init') && pieNow(wrap)) {
                // A pie chart recognized only after attach (mermaid fills the
                // svg's content after inserting the shell): strip the
                // affordances attach gave it. The event-time guards in the
                // handlers already make wheel/drag/pinch no-ops for pies.
                var rb = wrap.querySelector('.zonemap-reset');
                if (rb) rb.remove();
                if (s.style.transform) s.style.transform = '';
                continue;
            }
            if (wrap._zoomSvg && s && wrap._zoomSvg !== s) {
                // mermaid re-rendered in place (theme toggle) — follow the
                // new svg, keep the current zoom on it, and restore the
                // reset button the re-render wiped out
                wrap._zoomSvg = s;
                ensureReset(wrap);
                applyWrap(wrap);
                continue;
            }
            attach(wrap);
        }
    }
    // map.js loads in <head> without defer — at that point the DOM (and
    // document.body) does not exist yet, so boot on DOMContentLoaded instead
    // of running immediately (running immediately used to throw on
    // document.body === null and the zoom/pan handlers never attached).
    function boot() {
        scan();
        if (window.MutationObserver && document.body) {
            // Re-scan when the DOM changes (covers late-rendered mermaid
            // diagrams and the inlined zone teleport maps).
            new MutationObserver(scan).observe(document.body, { childList: true, subtree: true });
        }
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
