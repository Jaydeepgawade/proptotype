$(function(){
  $('.nav-toggle').on('click',function(){$('.topbar nav').toggleClass('open');});
  const money=n=>new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',maximumFractionDigits:0}).format(Number(n)||0);
  function updateRisk(){const capital=$('#Capital').val();const each=$('#RiskPerTradePercent').val();const max=$('#MaxTotalRiskPercent').val();$('#riskAmount').text(money(capital*each/100));$('#maxRiskAmount').text(money(capital*max/100));}
  $('#Capital,#RiskPerTradePercent,#MaxTotalRiskPercent').on('input',updateRisk);updateRisk();
  $('#MinimumRewardRiskRatio').on('input',function(){$('#ratioOutput').text($(this).val()+':1');});
  let activeChart=null,chartResize=null;
  $('.show-chart').on('click',function(){
    const button=$(this),dialog=document.getElementById('chartDialog'),container=document.getElementById('priceChart');
    const symbol=button.data('symbol');
    $('#chartTitle').text(symbol+' price chart');$('#chartError').text('');
    if(activeChart){activeChart.remove();activeChart=null;} container.innerHTML='';
    dialog.showModal();
    $.getJSON('/api/v1/market-data/'+encodeURIComponent(symbol)+'?timeframe=1D&take=60')
      .done(function(response){
        activeChart=LightweightCharts.createChart(container,{width:container.clientWidth,height:500,layout:{background:{color:'#ffffff'},textColor:'#526376'},grid:{vertLines:{color:'#eef2f6'},horzLines:{color:'#eef2f6'}},rightPriceScale:{borderColor:'#dce5ee'},timeScale:{borderColor:'#dce5ee',timeVisible:false},crosshair:{mode:LightweightCharts.CrosshairMode.Normal}});
        const candleSeries=activeChart.addSeries(LightweightCharts.CandlestickSeries,{upColor:'#11855b',downColor:'#c43f4b',borderVisible:false,wickUpColor:'#11855b',wickDownColor:'#c43f4b'});
        const volumeSeries=activeChart.addSeries(LightweightCharts.HistogramSeries,{priceFormat:{type:'volume'},priceScaleId:'',lastValueVisible:false,priceLineVisible:false});
        candleSeries.setData(response.candles.map(c=>({time:c.timeUtc.slice(0,10),open:c.open,high:c.high,low:c.low,close:c.close})));
        volumeSeries.setData(response.candles.map(c=>({time:c.timeUtc.slice(0,10),value:c.volume,color:c.close>=c.open?'#11855b55':'#c43f4b55'})));
        volumeSeries.priceScale().applyOptions({scaleMargins:{top:.82,bottom:0}});
        [{price:Number(button.data('entry')),color:'#0b5cab',title:'ENTRY'}, {price:Number(button.data('stop')),color:'#c43f4b',title:'STOP'}, {price:Number(button.data('target')),color:'#11855b',title:'TARGET'}].forEach(line=>candleSeries.createPriceLine({price:line.price,color:line.color,lineWidth:2,lineStyle:LightweightCharts.LineStyle.Dashed,axisLabelVisible:true,title:line.title}));
        activeChart.timeScale().fitContent();
        chartResize=new ResizeObserver(entries=>{if(activeChart)activeChart.applyOptions({width:entries[0].contentRect.width,height:window.innerWidth<600?390:500});});chartResize.observe(container);
      })
      .fail(xhr=>$('#chartError').text(xhr.responseJSON?.message||'Demo chart data could not be loaded.'));
  });
  $('.chart-close').on('click',function(){const dialog=document.getElementById('chartDialog');if(chartResize){chartResize.disconnect();chartResize=null;}if(activeChart){activeChart.remove();activeChart=null;}dialog.close();});
  $('.api-action').on('submit',function(event){
    event.preventDefault();
    const form=$(this), question=form.data('confirm');
    if(question&&!confirm(question))return;
    const button=form.find('button[type=submit]').prop('disabled',true);
    $.ajax({url:form.data('api'),method:'POST',data:form.serialize()})
      .done(function(response){
        if(response.message)sessionStorage.setItem('apiMessage',response.message);
        const redirect=form.data('redirect'); redirect?window.location.assign(redirect):window.location.reload();
      })
      .fail(function(xhr){
        const message=xhr.responseJSON?.message||xhr.responseJSON?.title||'The API request failed.';
        alert(message); button.prop('disabled',false);
      });
  });
  let pendingSetForm=null;
  const setDialog=document.getElementById('setConfirmDialog');
  const formatMoney=value=>new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',minimumFractionDigits:2}).format(Number(value)||0);
  $('.set-order-action').on('submit',function(event){
    event.preventDefault(); const form=$(this); form.find('button[type=submit]').prop('disabled',true);
    $.getJSON(form.data('preview-api')).done(function(preview){
      const signal=preview.signal; pendingSetForm=form;
      $('#setConfirmTitle').text(signal.symbol+' '+signal.side+' order'); $('#setConfirmMessage').text(preview.message);
      $('#setConfirmLevels').text(formatMoney(signal.entryPrice)+' / '+formatMoney(signal.stopLoss)+' / '+formatMoney(signal.targetPrice));
      $('#setConfirmQuantity').text(preview.quantity+' shares'); $('#setConfirmValue').text(formatMoney(preview.requiredOrderValue));
      $('#setConfirmCapital').text(formatMoney(preview.availableStyleCapital)); $('#setConfirmLoss').text(formatMoney(preview.maximumPossibleLoss));
      $('#setConfirmRatio').text(Number(Math.abs(signal.targetPrice-signal.entryPrice)/Math.abs(signal.entryPrice-signal.stopLoss)).toFixed(2)+' : 1');
      setDialog.showModal(); form.find('button[type=submit]').prop('disabled',false);
    }).fail(function(xhr){ alert(xhr.responseJSON?.message||'Unable to prepare this order.'); form.find('button[type=submit]').prop('disabled',false); });
  });
  $('#confirmSetButton').on('click',function(){
    if(!pendingSetForm)return; const button=$(this).prop('disabled',true);
    $.ajax({url:pendingSetForm.data('api'),method:'POST',data:pendingSetForm.serialize()}).done(function(response){
      sessionStorage.setItem('apiMessage',response.message); window.location.assign(pendingSetForm.data('redirect'));
    }).fail(function(xhr){ alert(xhr.responseJSON?.message||'The order could not be set.'); button.prop('disabled',false); setDialog.close(); });
  });
  const apiMessage=sessionStorage.getItem('apiMessage');
  if(apiMessage){sessionStorage.removeItem('apiMessage');$('.shell').prepend($('<div class="alert success">').text(apiMessage));}
  window.setTimeout(()=>$('.alert').fadeOut(),4500);
});
