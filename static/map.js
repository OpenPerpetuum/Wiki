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
    // area, shared between both axes). The world map starts above s = 1
    // (INITIAL_MAX): at s = 1 its islands — a dense cluster at one end of a
    // tall canvas — are a handful of pixels lost in empty space, so it
    // opens pre-zoomed onto the cluster (initContentZoom, below).
    var MIN = 1, MAX = 8, INITIAL_MAX = 3, MIN_VISIBLE = 0.25;

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
    // letterboxed (preserveAspectRatio meet) inside its box. clientWidth is
    // the LAYOUT (untransformed) size — getBoundingClientRect would include
    // the zoom scale, which clamp() needs to know as st.s, not bake in.
    function contentSize(wrap) {
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
    // two touch fingers) — the point under the mouse stays under the mouse.
    //
    // The CSS transform is translate(tx,ty) scale(s) with the default
    // transform-origin: the ELEMENT box center. So with C0 the element
    // center at the IDENTITY transform, a content point p (screen coords
    // at identity) lands at:
    //
    //     screen(p) = C0 + t + s * (p - C0)          (1)
    //
    // getBoundingClientRect includes the transform, and scaling about the
    // center leaves the center fixed, so the rect center is C0 + t — which
    // gives C0 = rectCenter - t. The content point currently under the
    // screen point C (from (1)):
    //
    //     p - C0 = (C - C0 - t) / s                  (2)
    //
    // For the new scale s' to keep that point under C, solve (1) for t':
    //
    //     t' = C - C0 - s' * (p - C0)               (3)
    //
    // (Earlier code anchored (C - C0)/s instead of (2) — off by t/s in
    // content space, so after any pan the wheel zoom drifted toward the
    // map center instead of following the cursor.)
    function zoomAt(wrap, clientX, clientY, factor) {
        var svg = wrap._zoomSvg, st = stateOf(wrap);
        var rect = svg.getBoundingClientRect();
        var c0x = rect.left + rect.width / 2 - st.tx;  // identity center
        var c0y = rect.top + rect.height / 2 - st.ty;
        var cap = wrap._zmPrezoomed ? MAX : Math.min(MAX, INITIAL_MAX);
        var ns = Math.min(cap, Math.max(MIN, st.s * factor));
        if (ns === st.s) return;
        var qx = (clientX - c0x - st.tx) / st.s;  // (2), relative to C0
        var qy = (clientY - c0y - st.ty) / st.s;
        st.tx = clientX - c0x - ns * qx;          // (3)
        st.ty = clientY - c0y - ns * qy;
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
                if (wrap._zmPrezoomed) {
                    // Back to the pre-zoomed resting state, not s = 1 —
                    // s = 1 on the world map is the lost-in-empty-space view.
                    initContentZoom(wrap);
                } else {
                    st.s = 1; st.tx = 0; st.ty = 0;
                }
                applyWrap(wrap);
            });
        }
    }

    // The world map opens pre-zoomed onto its content: compute the bounding
    // box of the plotted islands (+ a margin) in viewBox coordinates, scale
    // it up to ~88% of the letterboxed content area, and center it. The
    // islands sit at one end of a tall canvas (the rest is empty grid), so
    // s = 1 shows them as a small cluster in a sea of nothing; this starts
    // where the interest actually is. Zooming out to s = 1 afterwards is
    // still possible (INITIAL_MAX only caps the wheel, not the state).
    // The screen mapping under transform (t, s) — verified against the
    // browser: screen(p) = (rc - t) + s * P, where P is the content point's
    // screen position at the IDENTITY transform and rc is the svg element's
    // (transformed) bounding-box center (the transform-origin). So to put
    // the content point at screen position C: t = C - rc - s*P... measured
    // at identity, P is relative to the identity element box, whose center
    // is also the letterbox content center — giving t = (C - rc0) - s*P0
    // where rc0/P0 are both identity-frame positions.
    function initContentZoom(wrap) {
        var svg = wrap._zoomSvg, st = stateOf(wrap);
        if (!svg || !svg.querySelector('.zonemap-tp')) return; // zone maps: no
        var ns, tx = 0, ty = 0;
        try {
            var nodes = svg.querySelectorAll('a > g rect, a > g circle');
            if (nodes.length) {
                var minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
                for (var i = 0; i < nodes.length; i++) {
                    var el = nodes[i], b;
                    try { b = el.getBBox(); } catch (e) { continue; }
                    if (b.x < minX) minX = b.x;
                    if (b.y < minY) minY = b.y;
                    if (b.x + b.width > maxX) maxX = b.x + b.width;
                    if (b.y + b.height > maxY) maxY = b.y + b.height;
                }
                // Identity-frame geometry: the svg currently carries the
                // previous state; measure with it cleared first.
                var prev = svg.style.transform;
                svg.style.transform = '';
                var rc0 = svg.getBoundingClientRect(); // identity element box
                var rcx0 = rc0.left + rc0.width / 2;
                var rcy0 = rc0.top + rc0.height / 2;
                var m0 = svg.getScreenCTM(); // identity user->screen map
                var boxCx0 = m0.a * (minX + maxX) / 2 + m0.c * (minY + maxY) / 2 + m0.e;
                var boxCy0 = m0.d * (minY + maxY) / 2 + m0.b * (minX + maxX) / 2 + m0.f;
                svg.style.transform = prev;
                var wrapRect = wrap.getBoundingClientRect();
                var cW = wrapRect.width, cH = wrapRect.height; // wrap ≈ element at identity
                var contentW = rc0.width - 32, contentH = rc0.height - 32; // minus 2*1rem padding
                var vb = svg.viewBox.baseVal;
                var u = Math.min(contentW / vb.width, contentH / vb.height); // px per unit at identity
                var margin = 26; // viewBox units of breathing room
                var s = Math.min(INITIAL_MAX,
                    0.88 / Math.max((maxX - minX + 2 * margin) * u / contentW,
                                    (maxY - minY + 2 * margin) * u / contentH));
                if (s > 1.05) {
                    // C = wrap center (the target); P = box center at identity
                    // (boxCx0, boxCy0); rc at the NEW transform = rc0 (the
                    // element box is the same; the transform scales ABOUT its
                    // center, so the center stays at rc0's center + t).
                    ns = s;
                    var txTarget = wrapRect.left + wrapRect.width / 2;
                    var tyTarget = wrapRect.top + wrapRect.height / 2;
                    tx = txTarget - rcx0 - s * (boxCx0 - rcx0);
                    ty = tyTarget - rcy0 - s * (boxCy0 - rcy0);
                }
            }
        } catch (e) { /* fall through: stay at s = 1 */ }
        if (typeof ns === 'undefined') return;
        st.s = ns; st.tx = tx; st.ty = ty;
        wrap._zmPrezoomed = true;
        clamp(wrap);
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
        attachTpLines(wrap);
        attachTpSelector(wrap);
        ensureReset(wrap);
        initContentZoom(wrap);
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

    // World map only (the zone teleport maps have no .zonemap-tp lines):
    // the inter-zone TP lines are almost invisible at rest and fade in while
    // the cursor is near the line itself (distance to the segment, in
    // viewBox units — unaffected by the zoom transform). Desktop pointers
    // only: the CSS keeps the lines at normal opacity on touch layouts, and
    // this handler stays inert there (matchMedia).
    var TP_SHOW = 26, TP_KEEP = 46; // viewBox units (show / keep-lit)
    var tpFine = null;
    try { tpFine = window.matchMedia('(hover: hover) and (pointer: fine)'); } catch (e) {}
    function attachTpLines(wrap) {
        var svg = wrap.querySelector('svg.zonemap');
        var lines = svg ? svg.querySelectorAll('line.zonemap-tp') : [];
        if (!lines.length) return;
        var segs = [];
        for (var i = 0; i < lines.length; i++) {
            segs.push({
                el: lines[i], on: false,
                x1: parseFloat(lines[i].getAttribute('x1')),
                y1: parseFloat(lines[i].getAttribute('y1')),
                x2: parseFloat(lines[i].getAttribute('x2')),
                y2: parseFloat(lines[i].getAttribute('y2'))
            });
        }
        var pt = null;
        try { pt = document.createElementNS('http://www.w3.org/2000/svg', 'svg').createSVGPoint(); } catch (e) {}
        if (!pt) return;
        function dist(s, px, py) {
            var vx = s.x2 - s.x1, vy = s.y2 - s.y1;
            var wx = px - s.x1, wy = py - s.y1;
            var l2 = vx * vx + vy * vy;
            var t = l2 ? (wx * vx + wy * vy) / l2 : 0;
            t = Math.max(0, Math.min(1, t));
            var dx = px - (s.x1 + t * vx), dy = py - (s.y1 + t * vy);
            return Math.sqrt(dx * dx + dy * dy);
        }
        var raf = 0, mx = 0, my = 0;
        function update() {
            raf = 0;
            if (!(tpFine && tpFine.matches)) return;
            var m = svg.getScreenCTM();
            if (!m) return;
            pt.x = mx; pt.y = my;
            var p = pt.matrixTransform(m.inverse());
            for (var i = 0; i < segs.length; i++) {
                var s = segs[i];
                var d = dist(s, p.x, p.y);
                var on = d < TP_SHOW ? true : (s.on ? d <= TP_KEEP : false);
                if (on !== s.on) {
                    s.on = on;
                    if (on) s.el.classList.add('zm-tp-on');
                    else s.el.classList.remove('zm-tp-on');
                }
            }
        }
        wrap.addEventListener('pointermove', function (e) {
            if (!(tpFine && tpFine.matches)) return;
            mx = e.clientX; my = e.clientY;
            if (!raf) raf = requestAnimationFrame(update);
        });
        wrap.addEventListener('pointerleave', function () {
            for (var i = 0; i < segs.length; i++)
                if (segs[i].on) { segs[i].on = false; segs[i].el.classList.remove('zm-tp-on'); }
        });
    }

    // Teleport destination selector. An item that can reach MORE THAN ONE
    // other zone — a zone-map TP column (circle[data-dests]) or a world-map
    // island (a[data-dests]) — opens a small selector on hover (desktop)
    // or tap (touch): one entry per destination. While it is open, the
    // dashed exit lines to this item's destinations are emphasised; hovering
    // an entry emphasises ONLY that line, so the route is unambiguous; clicking
    // an entry navigates to the destination's zone page.
    // Single-destination items keep their plain <a> (click = navigate);
    // LTP endpoints carry no data-dests at all — no link, no selector,
    // no click action (a local teleport never reloads the page).
    function attachTpSelector(wrap) {
        var svg = wrap.querySelector('svg.zonemap');
        if (!svg) return;
        var items = svg.querySelectorAll('[data-dests]');
        if (!items.length) return;
        var lines = Array.prototype.slice.call(svg.querySelectorAll('line[data-tpd]'));
        if (!lines.length) return;
        var coarse = false;
        try { coarse = window.matchMedia('(hover: none), (pointer: coarse)').matches; } catch (e) {}

        var pop = document.createElement('div');
        pop.className = 'tp-sel';
        wrap.appendChild(pop);

        var cur = null, closeTimer = 0, inPop = false;

        // mirror of the generator's DisplayName (zone_asi_a_real -> "ASI A Real")
        function nameOf(n) {
            var s = n.replace(/^zone_/, '');
            return s.split('_').filter(Boolean).map(function (tok) {
                var m = tok.match(/^[a-zA-Z]+/);
                var l = m ? m[0] : '';
                if (!l) return tok;
                var rest = tok.slice(l.length);
                if (l === l.toUpperCase() && l !== l.toLowerCase()) return l + rest;
                if (l.length <= 3) return l.toUpperCase() + rest;
                return l.charAt(0).toUpperCase() + l.slice(1).toLowerCase() + rest;
            }).join(' ');
        }
        function hrefOf(n) { return '/zones/' + n.toLowerCase().replace(/_/g, '-') + '/'; }
        function destsOf(item) { return (item.getAttribute('data-dests') || '').split(/\s+/).filter(Boolean); }
        function linesFor(item) {
            var want = {};
            destsOf(item).forEach(function (d) { want[d] = 1; });
            // only lines that START at this item (data-tps: the column's eid
            // on zone maps, the island's zone name on the world map) —
            // another column/island may point at the same destination
            var src = item.getAttribute('data-eid') || item.getAttribute('data-zn');
            return lines.filter(function (l) {
                if (!want[l.getAttribute('data-tpd')]) return false;
                var s = l.getAttribute('data-tps');
                return !src || !s || s === src;
            });
        }
        function setOn(l, on) { l.classList[on ? 'add' : 'remove']('tp-sel-on'); }
        function cancelClose() { if (closeTimer) { clearTimeout(closeTimer); closeTimer = 0; } }
        function close() {
            cancelClose();
            if (!cur) return;
            linesFor(cur).forEach(function (l) { setOn(l, false); });
            cur = null;
            pop.style.display = 'none';
        }
        function scheduleClose() {
            if (inPop) return;
            cancelClose();
            closeTimer = setTimeout(close, 180);
        }
        function open(item) {
            var dests = destsOf(item);
            if (dests.length < 2) return;
            if (cur !== item) close();
            cur = item;
            while (pop.firstChild) pop.removeChild(pop.firstChild);
            var head = document.createElement('div');
            head.className = 'tp-sel-head';
            head.textContent = 'Teleport to';
            pop.appendChild(head);
            var myLines = linesFor(item);
            dests.forEach(function (d) {
                var b = document.createElement('button');
                b.type = 'button';
                b.className = 'tp-sel-item';
                b.textContent = nameOf(d);
                b.addEventListener('pointerenter', function () {
                    // emphasise ONLY this route while the entry is hovered
                    myLines.forEach(function (l) { setOn(l, l.getAttribute('data-tpd') === d); });
                });
                b.addEventListener('click', function () { window.location.href = hrefOf(d); });
                pop.appendChild(b);
            });
            pop.style.display = 'block';
            myLines.forEach(function (l) { setOn(l, true); });
            // position beside the item, kept inside the wrap
            var wr = wrap.getBoundingClientRect();
            var ir = item.getBoundingClientRect();
            var pw = pop.offsetWidth, ph = pop.offsetHeight;
            var x = ir.right - wr.left + 10;
            if (x + pw > wr.width - 6) x = ir.left - wr.left - pw - 10;
            if (x < 6) x = 6;
            var y = ir.top - wr.top + ir.height / 2 - ph / 2;
            y = Math.max(6, Math.min(y, Math.max(6, wr.height - ph - 6)));
            pop.style.left = x + 'px';
            pop.style.top = y + 'px';
        }
        Array.prototype.forEach.call(items, function (item) {
            if (destsOf(item).length < 2) return;
            if (!coarse) {
                item.addEventListener('pointerenter', function (e) {
                    if (e.pointerType && e.pointerType !== 'mouse') return;
                    cancelClose();
                    open(item);
                });
                item.addEventListener('pointerleave', scheduleClose);
            } else {
                // touch: first tap opens the selector (no navigation),
                // a second tap on the same item closes it
                item.addEventListener('click', function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    if (cur === item) close();
                    else open(item);
                });
            }
        });
        pop.addEventListener('pointerenter', function () { inPop = true; cancelClose(); });
        pop.addEventListener('pointerleave', function () { inPop = false; scheduleClose(); });
        wrap.addEventListener('pointerleave', function () { if (!inPop) close(); });
        // touch: a tap outside the item and the selector dismisses it
        document.addEventListener('pointerdown', function (e) {
            if (!coarse || !cur) return;
            if (pop.contains(e.target) || (cur.contains && cur.contains(e.target))) return;
            close();
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
