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

    // Relative path from the current page back to the site root (works under a
    // subpath deployment too). The index lists site-absolute paths ('/features/x/'),
    // so the root is derived from where the index itself was fetched from.
    function relToRoot() {
        var parts = location.pathname.replace(/\/+$/, '').split('/').filter(Boolean);
        return parts.map(function () { return '..'; }).concat('search_index.json').join('/');
    }

    function loadIndex() {
        if (pages) return Promise.resolve(pages);
        rootPath = new URL(relToRoot(), location.href).pathname.replace(/search_index\.json$/, '');
        return fetch(relToRoot(), { cache: 'force-cache' })
            .then(function (r) {
                if (!r.ok) throw new Error('HTTP ' + r.status);
                return r.json();
            })
            .then(function (data) {
                pages = data.map(function (p) {
                    var t = p.t || '';
                    var d = p.d || '';
                    var u = rootPath + (p.u || '/').replace(/^\//, '');
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
