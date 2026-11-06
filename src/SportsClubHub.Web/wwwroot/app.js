// Small helper so interactive pages can set the browser tab title.
window.sportsClubHub = {
    prefersReducedMotion: function () {
        return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    },
    downloadText: function (fileName, text) {
        var blob = new Blob([text], { type: 'text/csv;charset=utf-8' });
        var url = URL.createObjectURL(blob);
        var link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
    },
    focusById: function (id) {
        var element = document.getElementById(id);
        if (element) { element.focus(); }
    },
    setTitle: function (title) {
        document.title = title + ' | Sports Club Hub';
    },
    getCurrentPosition: function () {
        return new Promise(function (resolve, reject) {
            if (!navigator.geolocation) {
                reject('This browser does not provide location access.');
                return;
            }

            navigator.geolocation.getCurrentPosition(
                function (position) {
                    resolve({
                        latitude: position.coords.latitude,
                        longitude: position.coords.longitude
                    });
                },
                function (error) {
                    reject(error.code === 1
                        ? 'Location access was declined. You can enable it in your browser settings.'
                        : 'We could not determine your location. Please try again.');
                },
                { enableHighAccuracy: false, maximumAge: 300000, timeout: 10000 }
            );
        });
    }
};

// Header dropdowns ("More" and the mobile "Explore" menu) are plain <details> elements. Close them when a link
// inside is used, when Escape is pressed, or when the person clicks elsewhere, so they never stay open over the page.
(function () {
    function menus() { return document.querySelectorAll('details.nav-more, details.mobile-explore'); }
    document.addEventListener('click', function (event) {
        menus().forEach(function (menu) {
            if (!menu.open) { return; }
            var clickedLink = event.target.closest && event.target.closest('a') && menu.contains(event.target);
            if (clickedLink || !menu.contains(event.target)) { menu.open = false; }
        });
    });
    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Escape') { return; }
        menus().forEach(function (menu) {
            if (menu.open) { menu.open = false; var summary = menu.querySelector('summary'); if (summary) { summary.focus(); } }
        });
    });
})();

// Home page hero video: respect reduced-motion and data-saver, and let visitors pause it (moving content needs a pause control).
(function () {
    function heroVideo() { return document.querySelector('.hero-media video'); }
    function init() {
        var video = heroVideo();
        if (!video) { return; }
        var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        var saveData = navigator.connection && navigator.connection.saveData;
        if (reduce || saveData) { video.pause(); }
    }
    document.addEventListener('click', function (event) {
        var button = event.target.closest && event.target.closest('[data-hero-video-toggle]');
        var video = heroVideo();
        if (!button || !video) { return; }
        if (video.paused) { video.play(); button.textContent = 'Pause background video'; }
        else { video.pause(); button.textContent = 'Play background video'; }
    });
    document.addEventListener('DOMContentLoaded', init);
    window.addEventListener('load', function () {
        init();
        if (window.Blazor && window.Blazor.addEventListener) { window.Blazor.addEventListener('enhancedload', init); }
    });
})();

// Give visitors immediate feedback while an internal route loads. Enhanced navigation
// replaces the route without a full document load, so clear the indicator after each update.
(function () {
    var indicator = document.getElementById('route-loading');
    var mainContent = document.getElementById('main-content');
    var hideTimer;

    function hideRouteLoading() {
        if (hideTimer) { window.clearTimeout(hideTimer); }
        if (indicator) {
            indicator.classList.remove('is-active');
            indicator.setAttribute('aria-hidden', 'true');
        }
        if (mainContent) { mainContent.removeAttribute('aria-busy'); }
    }

    function showRouteLoading() {
        if (!indicator) { return; }
        indicator.classList.add('is-active');
        indicator.setAttribute('aria-hidden', 'false');
        if (mainContent) { mainContent.setAttribute('aria-busy', 'true'); }
        if (hideTimer) { window.clearTimeout(hideTimer); }
        hideTimer = window.setTimeout(hideRouteLoading, 12000);
    }

    document.addEventListener('click', function (event) {
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        var link = event.target.closest && event.target.closest('a[href]');
        if (!link || link.hasAttribute('download') || (link.target && link.target !== '_self')) { return; }

        var destination;
        try { destination = new URL(link.href, window.location.href); }
        catch (_) { return; }

        if (destination.origin !== window.location.origin ||
            (destination.pathname === window.location.pathname && destination.search === window.location.search)) {
            return;
        }

        showRouteLoading();
    });

    document.addEventListener('enhancedload', hideRouteLoading);
    window.addEventListener('pageshow', hideRouteLoading);
    window.addEventListener('popstate', showRouteLoading);
})();
