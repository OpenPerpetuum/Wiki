// Zone teleport maps: the generated zone pages embed the maps as plain
// <img src="/zonemaps/<zone>.svg">. The SVGs carry clickable teleport
// elements (<a href="/zones/...">), which an <img> cannot show — so fetch
// the SVG, inline it into the shared .zonemap-wrap wrapper (with the
// reset button) and replace the <img>. static/map.js watches the DOM and
// attaches wheel-zoom / drag-pan / pinch / reset to the new wrapper, so
// the zone maps behave exactly like the world map.
// If the fetch fails (offline, unsupported browser) the static <img>
// stays — the map is still visible, just not interactive.
(function () {
    'use strict';

    // The display mode (height/color) is a user preference: it is
    // remembered and applied to every zone map that gets inlined. (Older
    // versions also had a 'plain' mode; that value now reads as 'height'.)
    var MODE_KEY = 'wiki-zonemap-mode';
    function getMode() {
        try { return localStorage.getItem(MODE_KEY); } catch (err) { return null; }
    }
    function setMode(m) {
        try { localStorage.setItem(MODE_KEY, m); } catch (err) { /* private mode */ }
    }
    function applyMode(hImg, cImg, mode, bar) {
        if (mode !== 'color') mode = 'height';
        hImg.style.display = mode === 'height' ? '' : 'none';
        cImg.style.display = mode === 'color' ? '' : 'none';
        if (bar) {
            Array.prototype.forEach.call(bar.querySelectorAll('.zonemap-mode'), function (x) {
                x.setAttribute('aria-pressed', x.getAttribute('data-mode') === mode ? 'true' : 'false');
            });
        }
    }

    function upgrade(img) {
        var src = img.getAttribute('src');
        fetch(src, { credentials: 'same-origin' }).then(function (r) {
            if (!r.ok) throw new Error('http ' + r.status);
            return r.text();
        }).then(function (text) {
            var doc = new DOMParser().parseFromString(text, 'image/svg+xml');
            if (doc.querySelector('parsererror')) throw new Error('bad svg');
            var svg = doc.querySelector('svg');
            if (!svg) throw new Error('no svg');
            // fill the wrapper like the world map (CSS sizes .zonemap)
            svg.classList.add('zonemap');
            svg.removeAttribute('width');
            svg.removeAttribute('height');

            var wrap = document.createElement('div');
            wrap.className = 'zonemap-wrap zonetp-wrap';
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'zonemap-reset';
            btn.title = 'Reset the zoom';
            btn.textContent = '⟲';
            wrap.appendChild(btn);

            // display modes (Height / Color) — only for zones whose
            // SVG carries both real-terrain images
            var hImg = svg.querySelector('#zm-height');
            var cImg = svg.querySelector('#zm-color');
            var bar = null;
            if (hImg && cImg) {
                var ui = window.WIKI_UI || {};
                bar = document.createElement('div');
                bar.className = 'zonemap-modes';
                [['height', ui.zonemapModeHeight || 'Height'],
                 ['color', ui.zonemapModeColor || 'Color']].forEach(function (m) {
                    var b = document.createElement('button');
                    b.type = 'button';
                    b.className = 'zonemap-mode';
                    b.textContent = m[1];
                    b.setAttribute('data-mode', m[0]);
                    b.addEventListener('click', function () {
                        setMode(m[0]);
                        applyMode(hImg, cImg, m[0], bar);
                    });
                    bar.appendChild(b);
                });
                wrap.appendChild(bar);
            }
            if (hImg && cImg) applyMode(hImg, cImg, getMode(), bar); // remembered choice
            wrap.appendChild(svg);

            // the <img> sits alone in a <p>; replace that paragraph so a
            // <div> is not nested in <p>
            var host = img.parentNode && img.parentNode.nodeName === 'P'
                ? img.parentNode
                : img;
            host.parentNode.replaceChild(wrap, host);
        }).catch(function () { /* keep the static <img> */ });
    }

    function scan() {
        var imgs = document.querySelectorAll('img[src^="/zonemaps/"]');
        for (var i = 0; i < imgs.length; i++) {
            if (imgs[i].getAttribute('data-zonetp-init')) continue;
            imgs[i].setAttribute('data-zonetp-init', '1');
            upgrade(imgs[i]);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', scan);
    } else {
        scan();
    }
})();
