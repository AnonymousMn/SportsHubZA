// Small helper so interactive pages can set the browser tab title.
window.sportsClubHub = {
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
