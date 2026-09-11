$(function () {
    const container = document.getElementById('tradeChart');
    if (!container) return;
    const message = $('#tradeChartMessage'), retry = $('#retryTradeChart');
    let chart, observer, request;
    const levelMoney = value => new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value) || 0);
    function orderLevels() {
        return [['entry', 'ENTRY', '#0b5cab'], ['stop', 'STOP-LOSS', '#c43f4b'], ['target', 'TARGET', '#11855b']]
            .map(([key, title, color]) => ({ price: Number(container.dataset[key]), title, color }))
            .filter(line => Number.isFinite(line.price) && line.price > 0);
    }
    function addOrderLevelLines(series, levels) {
        levels.forEach(line => series.createPriceLine({ price: line.price, title: `${line.title} ${levelMoney(line.price)}`, color: line.color, lineWidth: 2, lineStyle: LightweightCharts.LineStyle.Dashed, axisLabelVisible: true }));
    }
    function cleanup() {
        if (observer) { observer.disconnect(); observer = null; }
        if (chart) { chart.remove(); chart = null; }
    }
    function load() {
        cleanup(); retry.prop('hidden', true); message.text('Loading demo candles...');
        request = $.getJSON(container.dataset.url).done(function (response) {
            if (!response.candles?.length) { message.text('No demo candles are available for this symbol.'); return; }
            try {
                chart = LightweightCharts.createChart(container, {
                    width: container.clientWidth, height: container.clientHeight,
                    layout: { background: { color: '#ffffff' }, textColor: '#526376' },
                    grid: { vertLines: { color: '#eef2f6' }, horzLines: { color: '#eef2f6' } },
                    rightPriceScale: { borderColor: '#dce5ee' }, timeScale: { borderColor: '#dce5ee' }
                });
                const candles = chart.addSeries(LightweightCharts.CandlestickSeries, { upColor: '#11855b', downColor: '#c43f4b', borderVisible: false, wickUpColor: '#11855b', wickDownColor: '#c43f4b' });
                candles.setData(response.candles.map(c => ({ time: c.timeUtc.slice(0, 10), open: c.open, high: c.high, low: c.low, close: c.close })));
                const levels = orderLevels();
                const volume = chart.addSeries(LightweightCharts.HistogramSeries, { priceFormat: { type: 'volume' }, priceScaleId: '', lastValueVisible: false, priceLineVisible: false });
                volume.setData(response.candles.map(c => ({ time: c.timeUtc.slice(0, 10), value: c.volume, color: c.close >= c.open ? '#11855b44' : '#c43f4b44' })));
                volume.priceScale().applyOptions({ scaleMargins: { top: .85, bottom: 0 } });
                addOrderLevelLines(candles, levels);
                candles.applyOptions({ autoscaleInfoProvider: original => { const info = original(); if (!info || !levels.length) return info; const prices = levels.map(line => line.price); return { ...info, priceRange: { minValue: Math.min(info.priceRange.minValue, ...prices), maxValue: Math.max(info.priceRange.maxValue, ...prices) } }; } });
                chart.timeScale().fitContent();
                observer = new ResizeObserver(() => chart?.applyOptions({ width: container.clientWidth, height: container.clientHeight }));
                observer.observe(container);
                message.text('Demo daily candles with volume. Order levels are shown for reference.');
            } catch { cleanup(); message.text('The demo chart could not be displayed.'); retry.prop('hidden', false); }
        }).fail(function (xhr, status) {
            if (status !== 'abort') { message.text(xhr.responseJSON?.message || 'Unable to load demo data. Please retry.'); retry.prop('hidden', false); }
        });
    }
    retry.on('click', load);
    $(window).on('pagehide', function () { request?.abort(); cleanup(); });
    $(window).on('pageshow', function (event) { if (event.originalEvent.persisted) load(); });
    load();
});



