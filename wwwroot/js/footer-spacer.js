(function(){
  function adjustFooterSpacer(){
    try{
      var footer = document.querySelector('footer.galaxy-footer');
      if(!footer) return;

      // Ensure footer is last child of body so it appears after all content
      if(footer.parentElement !== document.body || document.body.lastElementChild !== footer){
        document.body.appendChild(footer);
      }

      // Make footer relative positioned to sit in normal flow
      footer.style.position = 'relative';

      var pageShells = document.querySelectorAll('.page-shell');
      var mainEl = document.querySelector('main');
      var extra = 20; // small gap
      var h = footer.offsetHeight + extra;

      // Ensure main / page-shell reserve space for footer
      if(mainEl) mainEl.style.paddingBottom = h + 'px';
      pageShells.forEach(function(ps){
        ps.style.paddingBottom = h + 'px';
      });
    }catch(e){/* ignore */}
  }
  if(document.readyState === 'complete' || document.readyState === 'interactive'){
    setTimeout(adjustFooterSpacer, 50);
  } else {
    window.addEventListener('DOMContentLoaded', adjustFooterSpacer);
  }
  window.addEventListener('resize', function(){ setTimeout(adjustFooterSpacer, 50); });
})();
