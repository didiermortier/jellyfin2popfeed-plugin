const PopFeedConfig = {
    pluginUniqueId: '5f3e8a1c-2b7d-4e9f-a6c3-d1e4f8a2b9c5',

    // Load config and test connection on page show
    loadConfig: function (page) {
        ApiClient.getPluginConfiguration(PopFeedConfig.pluginUniqueId).then(function (config) {
            page.querySelector('#handle').value = config.atProtocolHandle || '';
            page.querySelector('#pdsHost').value = config.atProtocolPdsHost || 'popfeed.social';
            page.querySelector('#autoPost').checked = config.autoPostMovies !== false;

            // Show connected badge if tokens exist
            if (config.atProtocolAccessToken) {
                PopFeedConfig.showConnected(page, config.atProtocolHandle, config.atProtocolPdsHost);
            } else {
                PopFeedConfig.showDisconnected(page);
            }

            Dashboard.hideLoadingMsg();
        }).catch(function () {
            Dashboard.hideLoadingMsg();
        });
    },

    // Single call: authenticate and save everything
    authenticate: function (page) {
        const handle = page.querySelector('#handle').value;
        const password = page.querySelector('#password').value;
        const pdsHost = page.querySelector('#pdsHost').value;
        const autoPost = page.querySelector('#autoPost').checked;

        if (!handle || !password) {
            PopFeedConfig.showMessage(page, 'Please enter a handle and password.', 'warning');
            return;
        }

        Dashboard.showLoadingMsg();
        PopFeedConfig.apiPost(page, 'Authenticate', {
            handle: handle,
            password: password,
            pdsHost: pdsHost,
            autoPostMovies: autoPost
        }).then(function (result) {
            PopFeedConfig.showMessage(page, 'Authenticated successfully! Settings saved.', 'success');
            PopFeedConfig.showConnected(page, result.handle, result.pdsHost);
        }).catch(function () {
            PopFeedConfig.showMessage(page, 'Authentication failed. Check credentials and PDS host.', 'error');
        });
    },

    // Single call: save settings, server merges with existing tokens
    saveSettings: function (page) {
        const handle = page.querySelector('#handle').value;
        const password = page.querySelector('#password').value;
        const pdsHost = page.querySelector('#pdsHost').value;
        const autoPost = page.querySelector('#autoPost').checked;

        Dashboard.showLoadingMsg();
        PopFeedConfig.apiPost(page, 'Settings', {
            handle: handle,
            password: password,
            pdsHost: pdsHost,
            autoPostMovies: autoPost
        }).then(function (result) {
            PopFeedConfig.showMessage(page, 'Settings saved!', 'success');
            if (result.connected) {
                PopFeedConfig.showConnected(page, result.handle, result.pdsHost);
            } else {
                PopFeedConfig.showDisconnected(page);
            }
        }).catch(function () {
            PopFeedConfig.showMessage(page, 'Failed to save settings.', 'error');
        });
    },

    // Single call: test connection
    testConnection: function (page) {
        PopFeedConfig.apiGet(page, 'TestConnection').then(function (result) {
            if (result.connected) {
                PopFeedConfig.showMessage(page, 'Connection is working!', 'success');
                PopFeedConfig.showConnected(page, result.handle, result.pdsHost);
            } else {
                PopFeedConfig.showMessage(page, 'Not connected. Please authenticate first.', 'warning');
            }
        }).catch(function () {
            PopFeedConfig.showMessage(page, 'Test failed. Is the Jellyfin server running?', 'error');
        });
    },

    // Single call: disconnect (clear tokens)
    disconnect: function (page) {
        Dashboard.showLoadingMsg();
        PopFeedConfig.apiPost(page, 'Disconnect', {}).then(function () {
            PopFeedConfig.showDisconnected(page);
            PopFeedConfig.showMessage(page, 'Disconnected. Your handle and host are saved for easy re-auth.', 'success');
        }).catch(function () {
            PopFeedConfig.showMessage(page, 'Failed to disconnect.', 'error');
        });
    },

    // --- UI helpers ---

    showConnected: function (page, handle, pdsHost) {
        const badge = page.querySelector('#connectedBadge');
        badge.classList.remove('hide');
        page.querySelector('#badgeHandle').textContent = handle || 'unknown';
        page.querySelector('#badgePdsHost').textContent = 'on ' + (pdsHost || 'popfeed.social');
    },

    showDisconnected: function (page) {
        page.querySelector('#connectedBadge').classList.add('hide');
    },

    showMessage: function (page, message, type) {
        const msgDiv = page.querySelector('#statusMessage');
        msgDiv.classList.remove('hide');
        msgDiv.classList.remove('alert-success', 'alert-error', 'alert-warning');
        if (type === 'success') msgDiv.classList.add('alert-success');
        else if (type === 'error') msgDiv.classList.add('alert-error');
        else if (type === 'warning') msgDiv.classList.add('alert-warning');
        msgDiv.innerHTML = message;
    },

    // -- API helpers --

    apiPost: function (page, action, data) {
        return new Promise(function (resolve, reject) {
            const request = {
                url: ApiClient.getUrl('Jellyfin2PopFeed/PopFeed/' + action),
                dataType: 'json',
                type: 'POST',
                headers: { accept: 'application/json', 'Content-Type': 'application/json' },
                data: JSON.stringify(data)
            };
            ApiClient.fetch(request).then(function (result) {
                Dashboard.hideLoadingMsg();
                resolve(result);
            }).catch(function (result) {
                Dashboard.hideLoadingMsg();
                PopFeedConfig.showMessage(page,
                    (result.status ? result.status + ' - ' : '') +
                    (result.statusText || 'Request failed.'),
                    'error');
                reject(result);
            });
        });
    },

    apiGet: function (page, action) {
        return new Promise(function (resolve, reject) {
            const request = {
                url: ApiClient.getUrl('Jellyfin2PopFeed/PopFeed/' + action),
                dataType: 'json',
                type: 'GET'
            };
            ApiClient.fetch(request).then(function (result) {
                resolve(result);
            }).catch(function (result) {
                PopFeedConfig.showMessage(page,
                    (result.status ? result.status + ' - ' : '') +
                    (result.statusText || 'Request failed.'),
                    'error');
                reject(result);
            });
        });
    }
};

export default function (view) {
    view.querySelector('#authenticateBtn').addEventListener('click', function () {
        PopFeedConfig.authenticate(view);
    });

    view.querySelector('#testConnectionBtn').addEventListener('click', function () {
        PopFeedConfig.testConnection(view);
    });

    view.querySelector('#saveBtn').addEventListener('click', function () {
        PopFeedConfig.saveSettings(view);
    });

    view.querySelector('#disconnectBtn').addEventListener('click', function () {
        PopFeedConfig.disconnect(view);
    });

    view.querySelector('#popfeedConfigurationForm').addEventListener('submit', function (e) {
        PopFeedConfig.saveSettings(view);
        e.preventDefault();
        return false;
    });

    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();
        PopFeedConfig.loadConfig(this);
    });
}