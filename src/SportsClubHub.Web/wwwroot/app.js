// Small helper so interactive pages can set the browser tab title.
window.sportsClubHub = {
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
