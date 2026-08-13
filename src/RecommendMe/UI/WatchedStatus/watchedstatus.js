define([
    'connectionManager',
    'dialogHelper',
    'globalize',
    'loading',
    'formDialogStyle',
    'emby-button',
    'emby-scroller',
    'emby-dialogclosebutton'
], function (connectionManager, dialogHelper, globalize, loading) {

    function formatResume(ticks) {
        if (!ticks || ticks <= 0) {
            return '';
        }

        const totalSeconds = Math.floor(ticks / 10000000);
        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const seconds = totalSeconds % 60;
        return hours + ':' + String(minutes).padStart(2, '0') + ':' + String(seconds).padStart(2, '0');
    }

    function formatLastPlayed(value) {
        if (!value) {
            return '';
        }

        const date = new Date(value);
        return isNaN(date.getTime()) ? '' : date.toLocaleString();
    }

    function addCell(row, value, header) {
        const cell = document.createElement(header ? 'th' : 'td');
        cell.textContent = value;
        cell.style.padding = '0.65em 0.9em';
        cell.style.textAlign = 'left';
        cell.style.borderBottom = '1px solid rgba(255,255,255,.16)';
        row.appendChild(cell);
    }

    function render(page, result) {
        page.querySelector('.watchedStatusItemName').textContent = result.ItemName || '';
        const message = page.querySelector('.watchedStatusMessage');
        const container = page.querySelector('.watchedStatusTableContainer');

        if (!result.Allowed || !result.Users || !result.Users.length) {
            message.textContent = result.Message || 'No accessible users were found.';
            message.classList.remove('hide');
            container.innerHTML = '';
            return;
        }

        const table = document.createElement('table');
        table.style.width = '100%';
        table.style.borderCollapse = 'collapse';
        const head = document.createElement('thead');
        const headerRow = document.createElement('tr');
        addCell(headerRow, 'User', true);
        addCell(headerRow, 'Watched', true);
        addCell(headerRow, 'Last Played', true);
        addCell(headerRow, 'Resume Position', true);
        head.appendChild(headerRow);
        table.appendChild(head);

        const body = document.createElement('tbody');
        result.Users.forEach(function (user) {
            const row = document.createElement('tr');
            addCell(row, user.UserName || '', false);
            addCell(row, user.Watched ? 'Y' : 'N', false);
            addCell(row, formatLastPlayed(user.LastPlayed), false);
            addCell(row, formatResume(user.ResumePositionTicks), false);
            body.appendChild(row);
        });
        table.appendChild(body);
        container.innerHTML = '';
        container.appendChild(table);
    }

    return {
        show: function (itemId) {
            return new Promise(function (resolve, reject) {
                loading.show();
                const xhr = new XMLHttpRequest();
                xhr.open('GET', 'components/recommendme/watchedstatus.template.html', true);
                xhr.onload = function () {
                    const dlg = dialogHelper.createDialog({
                        removeOnClose: true,
                        size: 'fullscreen-border',
                        scrollY: true,
                        autoFocus: false
                    });
                    dlg.classList.add('formDialog');
                    dlg.innerHTML = globalize.translateDocument(this.response);
                    dlg.addEventListener('close', function () {
                        loading.hide();
                        resolve();
                    });
                    dialogHelper.open(dlg);

                    const apiClient = connectionManager.currentApiClient();
                    apiClient.getJSON(apiClient.getUrl('RecommendMe/WatchedStatus/' + itemId))
                        .then(function (result) {
                            render(dlg, result);
                            loading.hide();
                        }, function (error) {
                            loading.hide();
                            dialogHelper.close(dlg);
                            reject(error);
                        });
                };
                xhr.onerror = function (error) {
                    loading.hide();
                    reject(error);
                };
                xhr.send();
            });
        }
    };
});
