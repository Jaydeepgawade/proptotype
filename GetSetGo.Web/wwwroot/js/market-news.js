$(function () {
    const panel = document.getElementById('marketNews');
    if (!panel) return;
    const button = $('#refreshNews'), message = $('#newsMessage');
    let request;
    function load() {
        if (request) return;
        button.prop('disabled', true); panel.setAttribute('aria-busy', 'true');
        message.text('Checking recent headlines and preparing the AI summary...');
        $('#newsSummary,#newsSources').empty(); $('#newsUpdated').text('');
        $('#newsSummaryHeading,#newsSourcesHeading').prop('hidden', true);
        request = $.ajax({ url: panel.dataset.url, dataType: 'json', timeout: 120000 })
            .done(function (result) {
                message.text(result.message);
                $('#newsUpdated').text('News fetched: ' + new Date(result.fetchedUtc).toLocaleString() + ' | Next refresh after: ' + new Date(result.nextRefreshUtc).toLocaleTimeString());
                const articles = result.articles || [];
                $('#newsSummaryHeading').prop('hidden', result.status !== 'ready');
                (result.summary || []).forEach(function (point) {
                    const item = $('<li>').text(point.text + ' ');
                    point.sourceIds.forEach(id => {
                        if (articles.some(article => article.id === id)) item.append($('<a>').attr('href', '#news-source-' + id).text('[' + id + '] '));
                    });
                    $('#newsSummary').append(item);
                });
                $('#newsSourcesHeading').prop('hidden', !articles.length);
                articles.forEach(function (article) {
                    let url;
                    try { url = new URL(article.url); } catch { return; }
                    if (url.protocol !== 'https:' || url.hostname !== 'news.google.com') return;
                    const item = $('<li>').attr('id', 'news-source-' + article.id);
                    item.append($('<span>').addClass('news-category').text(article.category));
                    item.append($('<a>').attr({ href: url.href, target: '_blank', rel: 'noopener noreferrer' }).text(article.title));
                    item.append($('<small>').text(article.publisher + ' | ' + new Date(article.publishedUtc).toLocaleString()));
                    $('#newsSources').append(item);
                });
            })
            .fail(function (xhr, status) {
                if (status !== 'abort') message.text(xhr.status === 401 ? 'Your session expired. Sign in again to load news.' : xhr.responseJSON?.message || 'News is temporarily unavailable. Please retry.');
            })
            .always(function () { request = null; button.prop('disabled', false); panel.setAttribute('aria-busy', 'false'); });
    }
    button.on('click', load);
    $(window).on('pagehide', () => request?.abort());
    $(window).on('pageshow', function (event) { if (event.originalEvent.persisted) load(); });
    load();
});
