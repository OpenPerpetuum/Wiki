// "A new update is available" toast for a static site.
//
// How it works: this script is loaded synchronously at the end of <body>,
// i.e. while the document is still being parsed — before DOMContentLoaded
// and before any of the site scripts (nav, maps, mermaid) has mutated the
// DOM. It snapshots the pristine HTML of <main> (the page content; the
// chrome around it is identical on every page and not worth comparing).
//
// Afterwards it re-fetches the current page with cache: 'no-store' and
// compares the <main> of the freshly parsed response against the snapshot.
// Comparing two DOM serializations (not the raw response text) means the
// result only depends on the deployed content: as long as the server
// serves the same bytes the page was loaded from, the comparison is
// stable. A difference means the site was redeployed while this tab was
// open → the toast appears (bottom right); clicking it reloads the page.
//
// Checks run ~2 s after load, then every 60 s while the tab is visible,
// and whenever the tab becomes visible again. A failed fetch (offline)
// is ignored silently.
(function () {
    'use strict';
    var main = document.querySelector('main');
    if (!main || !window.fetch || !window.DOMParser) return;

    var snapshot = main.innerHTML;
    var url = location.pathname; // hash navigation does not change the page
    var toast = null;
    var checking = false;
    var lastCheck = 0;
    var THROTTLE_MS = 30 * 1000; // never more than one in-flight/recent check

    function showToast() {
        if (toast) return;
        toast = document.createElement('button');
        toast.type = 'button';
        toast.className = 'update-toast';
        toast.textContent = (window.WIKI_UI && window.WIKI_UI.updateAvailable) ||
            'A new update is available, click here to refresh.';
        toast.addEventListener('click', function () { location.reload(); });
        document.body.appendChild(toast);
    }

    function hideToast() {
        if (!toast) return;
        var t = toast;
        toast = null;
        t.parentNode.removeChild(t);
    }

    function check() {
        if (checking || document.hidden) return;
        var now = Date.now();
        if (now - lastCheck < THROTTLE_MS) return;
        lastCheck = now;
        checking = true;
        fetch(url, { cache: 'no-store' })
            .then(function (r) {
                if (!r.ok) throw new Error('http ' + r.status);
                return r.text();
            })
            .then(function (text) {
                var doc = new DOMParser().parseFromString(text, 'text/html');
                var fresh = doc.querySelector('main');
                if (fresh && fresh.innerHTML !== snapshot) {
                    showToast(); // deployed site is newer than this tab
                } else {
                    hideToast(); // same content again (or nothing to compare)
                }
            })
            .catch(function () { /* offline or blocked — stay quiet */ })
            .then(function () { checking = false; });
    }

    setTimeout(check, 2000); // soon after load (also covers long-open tabs)
    setInterval(check, 60000);
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) check(); // came back to the tab
    });
})();
