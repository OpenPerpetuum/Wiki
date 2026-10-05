// Lightweight wiki search over the /search_index.json
// (title + description + URL per page). Debounced; shows a result count.
// This file is loaded from <head>, so wait for the body to exist before
// binding the input.
function wikiSearchInit() {
    var input = document.getElementById('wiki-search');
    var box = document.getElementById('search-results');
    if (!input || !box) return;

    var pages = null;
    var rootPath = '';  // site root as an absolute path ('/' or '/user/repo/wiki/')
    var timer = null;
    var DEBOUNCE_MS = 200;

    function escapeHtml(s) {
        return String(s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    // Site root, from the deploy base (body[data-root] = config.base_url):
    // "/" locally, the full GitHub Pages URL on the project site. Only the
    // PATH part matters — the site always lives on the CURRENT origin under
    // that path — so both the index fetch and the result links are built
    // origin-relative (a full-URL fetch would leave the origin).
    var baseRoot = (document.body && document.body.getAttribute('data-root')) || '';
    function rootInfo() {
        if (baseRoot) {
            var p = baseRoot.replace(/^https?:\/\/[^/]+/, '').replace(/\/+$/, ''); // "" | "/Wiki"
            return { fetch: p + '/search_index.json', path: p || '/' };
        }
        // no data-root (older build): climb out of the current path
        var parts = location.pathname.replace(/\/+$/, '').split('/').filter(Boolean);
        return {
            fetch: parts.map(function () { return '..'; }).concat('search_index.json').join('/'),
            path: new URL(parts.map(function () { return '..'; }).join('/') + '/', location.href).pathname
        };
    }

    function loadIndex() {
        if (pages) return Promise.resolve(pages);
        var ri = rootInfo();
        rootPath = ri.path;
        return fetch(ri.fetch, { cache: 'force-cache' })
            .then(function (r) {
                if (!r.ok) throw new Error('HTTP ' + r.status);
                return r.json();
            })
            .then(function (data) {
                pages = data.map(function (p) {
                    var t = p.t || '';
                    var d = p.d || '';
                    // p.u is site-absolute ("/content/x/"); rootPath is "" for
                    // "/" (no change) or the base path ("/Wiki") to prepend.
                    var u = p.u || '/';
                    if (rootPath && rootPath !== '/' && u.indexOf(rootPath + '/') !== 0)
                        u = rootPath + (u.charAt(0) === '/' ? u : '/' + u);
                    return {
                        t: t, d: d, u: u,
                        tl: t.toLowerCase(), dl: (d + ' ' + u).toLowerCase()
                    };
                });
                return pages;
            });
    }

    function search(q) {
        var tokens = q.toLowerCase().split(/\s+/).filter(Boolean);
        if (!tokens.length) return [];
        var out = [];
        for (var i = 0; i < pages.length; i++) {
            var p = pages[i];
            var score = 0;
            var allFound = true;
            for (var j = 0; j < tokens.length; j++) {
                var tk = tokens[j];
                if (p.tl.indexOf(tk) !== -1) score += 3;
                else if (p.dl.indexOf(tk) !== -1) score += 1;
                else { allFound = false; break; }
            }
            if (allFound) out.push([score, p]);
        }
        out.sort(function (a, b) { return b[0] - a[0] || a[1].tl.localeCompare(b[1].tl); });
        return out.slice(0, 25).map(function (x) { return x[1]; });
    }

    function render(q) {
        loadIndex().then(function () {
            var hits = search(q);
            var ui = window.WIKI_UI || {};
            if (!hits.length) {
                box.innerHTML = '<div class="no-results">' + escapeHtml(ui.search_no_results || 'No pages match') + ' “' + escapeHtml(q) + '”.</div>';
            } else {
                var count = hits.length === 1
                    ? (ui.search_pages_found_one || '1 page found')
                    : (ui.search_pages_found_many || '{n} pages found').replace('{n}', hits.length);
                box.innerHTML =
                    '<div class="result-count">' + escapeHtml(count) + '</div>' +
                    hits.map(function (p) {
                        return '<a href="' + escapeHtml(p.u) + '"><strong>' + escapeHtml(p.t) + '</strong>' +
                            (p.d ? '<span class="result-desc"> — ' + escapeHtml(p.d.length > 100 ? p.d.slice(0, 100) + '…' : p.d) + '</span>' : '') +
                            '</a>';
                    }).join('');
            }
            box.hidden = false;
        }).catch(function () {
            box.innerHTML = '<div class="no-results">' + escapeHtml((window.WIKI_UI || {}).search_unavailable || 'Search is unavailable') + '</div>';
            box.hidden = false;
        });
    }

    input.addEventListener('input', function () {
        var v = input.value.trim();
        clearTimeout(timer);
        if (!v) { box.hidden = true; box.innerHTML = ''; return; }
        timer = setTimeout(function () { render(v); }, DEBOUNCE_MS);
    });

    input.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            input.value = '';
            box.hidden = true;
            box.innerHTML = '';
            input.blur();
        } else if (e.key === 'Enter') {
            // Enter follows the top result; with nothing shown, clear the box.
            var first = box.hidden ? null : box.querySelector('a');
            if (first) { window.location.href = first.getAttribute('href'); }
            else { input.value = ''; box.hidden = true; box.innerHTML = ''; }
        }
    });

    document.addEventListener('click', function (e) {
        if (box.hidden) return;
        if (box.contains(e.target) || e.target === input) return;
        box.hidden = true;
    });
}
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', wikiSearchInit);
} else {
    wikiSearchInit();
}
