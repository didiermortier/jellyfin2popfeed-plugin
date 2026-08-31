const PopFeedConfig = {
    loadConfig: function (page) {
        PopFeedConfig.apiGet('Status').then(function (result) {
            page.querySelector('#handle').value = result.handle || '';
            page.querySelector('#pdsHost').value = result.pdsHost || 'popfeed.social';
            page.querySelector('#autoPost').checked = result.autoPostMovies !== false;
            // TMDB key: show placeholder if set, clear if not
            var tmdbInput = page.querySelector('#tmdbApiKey');
            tmdbInput.value = '';
            tmdbInput.placeholder = result.hasTmdbKey ? 'API key saved (enter new to change)' : 'Enter TMDB API Key';
            if (result.connected) {
                PopFeedConfig.showConnected(page, result.handle, result.pdsHost);
            } else {
                PopFeedConfig.showDisconnected(page);
            }
            Dashboard.hideLoadingMsg();
        }).catch(function () {
            Dashboard.hideLoadingMsg();
        });
    },

    authenticate: function (page) {
        const handle = page.querySelector('#handle').value;
        const password = page.querySelector('#password').value;
        const pdsHost = page.querySelector('#pdsHost').value;
        const tmdbApiKey = page.querySelector('#tmdbApiKey').value;
        const autoPost = page.querySelector('#autoPost').checked;

        if (!handle || !password) {
            PopFeedConfig.showMessage(page, 'Please enter a handle and password.', 'warning');
            return;
        }

        Dashboard.showLoadingMsg();
        var data = { handle: handle, password: password, pdsHost: pdsHost, autoPostMovies: autoPost };
        if (tmdbApiKey) data.tmdbApiKey = tmdbApiKey;

        PopFeedConfig.apiPost('Authenticate', data).then(function (result) {
            PopFeedConfig.showMessage(page, 'Authenticated successfully! Movie list discovered and saved.', 'success');
            PopFeedConfig.showConnected(page, result.handle, result.pdsHost);
            // Show TMDB key indicator
            var tmdbInput = page.querySelector('#tmdbApiKey');
            tmdbInput.value = '';
            tmdbInput.placeholder = 'API key saved (enter new to change)';
        }).catch(function (err) {
            PopFeedConfig.showMessage(page, err.message || 'Authentication failed.', 'error');
        });
    },

    testConnection: function (page) {
        PopFeedConfig.apiGet('TestConnection').then(function (result) {
            if (result.connected) {
                PopFeedConfig.showMessage(page, 'Connection is working!', 'success');
                PopFeedConfig.showConnected(page, result.handle, result.pdsHost);
            } else {
                PopFeedConfig.showMessage(page, 'Not connected. Please authenticate first.', 'warning');
            }
        }).catch(function () {
            PopFeedConfig.showMessage(page, 'Test failed.', 'error');
        });
    },

    disconnect: function (page) {
        Dashboard.showLoadingMsg();
        PopFeedConfig.apiPost('Disconnect', {}).then(function () {
            PopFeedConfig.showDisconnected(page);
            PopFeedConfig.showMessage(page, 'Disconnected. Handle and host are saved for easy re-auth.', 'success');
        }).catch(function () {
            PopFeedConfig.showMessage(page, 'Failed to disconnect.', 'error');
        });
    },

    showConnected: function (page, handle, pdsHost) {
        var badge = page.querySelector('#connectedBadge');
        badge.classList.remove('hide');
        page.querySelector('#badgeHandle').textContent = handle || 'unknown';
        page.querySelector('#badgePdsHost').textContent = 'on ' + (pdsHost || 'popfeed.social');
    },

    showDisconnected: function (page) {
        page.querySelector('#connectedBadge').classList.add('hide');
    },

    showMessage: function (page, message, type) {
        var msgDiv = page.querySelector('#statusMessage');
        msgDiv.classList.remove('hide', 'alert-success', 'alert-error', 'alert-warning');
        if (type === 'success') msgDiv.classList.add('alert-success');
        else if (type === 'error') msgDiv.classList.add('alert-error');
        else if (type === 'warning') msgDiv.classList.add('alert-warning');
        msgDiv.innerHTML = message;
    },

    apiPost: function (action, data) {
        return new Promise(function (resolve, reject) {
            ApiClient.fetch({
                url: ApiClient.getUrl('Jellyfin2PopFeed/PopFeed/' + action),
                dataType: 'json', type: 'POST',
                headers: { accept: 'application/json', 'Content-Type': 'application/json' },
                data: JSON.stringify(data)
            }).then(function (result) {
                Dashboard.hideLoadingMsg(); resolve(result);
            }).catch(function (result) {
                Dashboard.hideLoadingMsg();
                reject({ message: (result.status ? result.status + ' - ' : '') + (result.statusText || 'Request failed.') });
            });
        });
    },

    apiGet: function (action) {
        return new Promise(function (resolve, reject) {
            ApiClient.fetch({
                url: ApiClient.getUrl('Jellyfin2PopFeed/PopFeed/' + action),
                dataType: 'json', type: 'GET'
            }).then(function (result) { resolve(result); })
            .catch(function () { reject({ message: 'Request failed.' }); });
        });
    }
};

export default function (view) {
    view.querySelector('#authenticateBtn').addEventListener('click', function () { PopFeedConfig.authenticate(view); });
    view.querySelector('#testConnectionBtn').addEventListener('click', function () { PopFeedConfig.testConnection(view); });
    view.querySelector('#disconnectBtn').addEventListener('click', function () { PopFeedConfig.disconnect(view); });
    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();
        PopFeedConfig.loadConfig(this);
    });
}