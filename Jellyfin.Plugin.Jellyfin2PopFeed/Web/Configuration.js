const PopFeedConfigurationPage = {
    pluginUniqueId: '5f3e8a1c-2b7d-4e9f-a6c3-d1e4f8a2b9c5',
    loadConfig: function (page) {
        ApiClient.getPluginConfiguration(PopFeedConfigurationPage.pluginUniqueId).then(function (config) {
            page.querySelector('#handle').value = config.atProtocolHandle || '';
            page.querySelector('#pdsHost').value = config.atProtocolPdsHost || 'popfeed.social';
            page.querySelector('#autoPost').checked = config.autoPostMovies !== false;

            const statusDiv = page.querySelector('#connectionStatus');
            if (config.atProtocolAccessToken) {
                statusDiv.classList.remove('hide');
                statusDiv.innerHTML = '<span style="color:green">&#10003; Connected as ' +
                    (config.atProtocolHandle || config.atProtocolDid || 'unknown') + '</span>';
            } else {
                statusDiv.classList.add('hide');
            }

            Dashboard.hideLoadingMsg();
        }).catch(function () {
            Dashboard.hideLoadingMsg();
        });
    },
    saveConfig: function (page) {
        const config = {
            atProtocolHandle: page.querySelector('#handle').value,
            atProtocolPassword: page.querySelector('#password').value,
            atProtocolPdsHost: page.querySelector('#pdsHost').value,
            autoPostMovies: page.querySelector('#autoPost').checked
        };

        ApiClient.updatePluginConfiguration(PopFeedConfigurationPage.pluginUniqueId, config).then(function (result) {
            Dashboard.processPluginConfigurationUpdateResult(result);
            PopFeedConfigurationPage.showMessage(page, 'Settings saved!', 'success');
        }).catch(function () {
            PopFeedConfigurationPage.showMessage(page, 'Failed to save settings.', 'error');
        });
    },
    authenticate: function (page) {
        const handle = page.querySelector('#handle').value;
        const password = page.querySelector('#password').value;
        const pdsHost = page.querySelector('#pdsHost').value;

        if (!handle || !password) {
            PopFeedConfigurationPage.showMessage(page, 'Please enter a handle and password.', 'warning');
            return;
        }

        Dashboard.showLoadingMsg();

        const headers = { accept: 'application/json', 'Content-Type': 'application/json' };
        const request = {
            url: ApiClient.getUrl('Jellyfin2PopFeed/PopFeed/Authenticate'),
            dataType: 'json',
            type: 'POST',
            headers: headers,
            data: JSON.stringify({ handle: handle, password: password, pdsHost: pdsHost })
        };

        ApiClient.fetch(request).then(function (result) {
            // Save the config including tokens
            PopFeedConfigurationPage.saveConfig(page);
            PopFeedConfigurationPage.showMessage(page, 'Authenticated successfully!', 'success');
            const statusDiv = page.querySelector('#connectionStatus');
            statusDiv.classList.remove('hide');
            statusDiv.innerHTML = '<span style="color:green">&#10003; Connected as ' +
                (result.handle || handle) + '</span>';
        }).catch(function (result) {
            const msg = result.status + ' - ' + (result.statusText || 'Authentication failed');
            PopFeedConfigurationPage.showMessage(page, msg, 'error');
            Dashboard.hideLoadingMsg();
        });
    },
    testConnection: function (page) {
        const request = {
            url: ApiClient.getUrl('Jellyfin2PopFeed/PopFeed/TestConnection'),
            dataType: 'json',
            type: 'GET'
        };

        ApiClient.fetch(request).then(function (result) {
            if (result) {
                PopFeedConfigurationPage.showMessage(page, 'Connection is working!', 'success');
            } else {
                PopFeedConfigurationPage.showMessage(page, 'Not connected. Please authenticate first.', 'warning');
            }
        }).catch(function () {
            PopFeedConfigurationPage.showMessage(page, 'Connection test failed. Is the server running?', 'error');
        });
    },
    showMessage: function (page, message, type) {
        const msgDiv = page.querySelector('#statusMessage');
        msgDiv.classList.remove('hide');
        msgDiv.classList.remove('alert-success', 'alert-error', 'alert-warning');
        if (type === 'success') msgDiv.classList.add('alert-success');
        else if (type === 'error') msgDiv.classList.add('alert-error');
        else if (type === 'warning') msgDiv.classList.add('alert-warning');
        msgDiv.innerHTML = message;
    }
};

export default function (view) {
    view.querySelector('#authenticateBtn').addEventListener('click', function () {
        PopFeedConfigurationPage.authenticate(view);
    });

    view.querySelector('#testConnectionBtn').addEventListener('click', function () {
        PopFeedConfigurationPage.testConnection(view);
    });

    view.querySelector('#saveBtn').addEventListener('click', function () {
        PopFeedConfigurationPage.saveConfig(view);
    });

    view.querySelector('#popfeedConfigurationForm').addEventListener('submit', function (e) {
        PopFeedConfigurationPage.saveConfig(view);
        e.preventDefault();
        return false;
    });

    view.addEventListener('viewshow', function () {
        const page = this;
        Dashboard.showLoadingMsg();
        PopFeedConfigurationPage.loadConfig(page);
    });
}