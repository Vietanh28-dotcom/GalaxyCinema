// ============ Galaxy Cinema - star field background ============
(function () {
  const canvas = document.getElementById('starfield');
  if (!canvas) return;

  const ctx = canvas.getContext('2d');
  let width, height;
  const stars = [];
  const SHOOTING_COUNT = 1;
  const shooting = [];

  function resize() {
    width = canvas.width = window.innerWidth;
    height = canvas.height = window.innerHeight;
  }

  function initStars() {
    stars.length = 0;
    const count = Math.min(220, Math.floor((width * height) / 6500));
    for (let i = 0; i < count; i++) {
      stars.push({
        x: Math.random() * width,
        y: Math.random() * height,
        r: Math.random() * 1.4 + 0.3,
        base: Math.random() * 0.5 + 0.25,
        phase: Math.random() * Math.PI * 2,
        speed: Math.random() * 0.9 + 0.3,
        color: Math.random() > 0.82 ? '255,215,0' : (Math.random() > 0.6 ? '0,212,255' : '255,255,255')
      });
    }
  }

  function launchShootingStar() {
    shooting.length = 0;
    const x = Math.random() * width * 0.7 + width * 0.2;
    shooting.push({
      x, y: Math.random() * height * 0.3,
      vx: (Math.random() * 3 + 4) * (Math.random() > 0.5 ? 1 : -1),
      vy: Math.random() * 2 + 3,
      life: 1
    });
  }

  let lastShoot = 0;

  function draw(time) {
    ctx.clearRect(0, 0, width, height);

    for (const s of stars) {
      const tw = Math.sin(time * 0.001 * s.speed + s.phase);
      const alpha = Math.max(0.05, s.base + tw * 0.35);
      ctx.beginPath();
      ctx.arc(s.x, s.y, s.r, 0, Math.PI * 2);
      ctx.fillStyle = `rgba(${s.color},${alpha})`;
      ctx.shadowBlur = s.r > 1.2 ? 6 : 2;
      ctx.shadowColor = `rgba(${s.color},0.8)`;
      ctx.fill();
      ctx.shadowBlur = 0;
    }

    if (time - lastShoot > 9000) {
      launchShootingStar();
      lastShoot = time;
    }

    for (let i = shooting.length - 1; i >= 0; i--) {
      const sh = shooting[i];
      sh.x += sh.vx;
      sh.y += sh.vy;
      sh.life -= 0.018;
      if (sh.life <= 0) { shooting.splice(i, 1); continue; }
      const tail = 12;
      const grad = ctx.createLinearGradient(sh.x, sh.y, sh.x - sh.vx * tail, sh.y - sh.vy * tail);
      grad.addColorStop(0, `rgba(255,255,255,${0.9 * sh.life})`);
      grad.addColorStop(1, 'rgba(255,255,255,0)');
      ctx.strokeStyle = grad;
      ctx.lineWidth = 1.6;
      ctx.beginPath();
      ctx.moveTo(sh.x, sh.y);
      ctx.lineTo(sh.x - sh.vx * tail, sh.y - sh.vy * tail);
      ctx.stroke();
    }

    requestAnimationFrame(draw);
  }

  resize();
  initStars();
  window.addEventListener('resize', () => { resize(); initStars(); });
  requestAnimationFrame(draw);
})();

// ============ Hero slider autoplay ============
(function () {
  const slider = document.querySelector('.hero-slider');
  if (!slider) return;
  const slides = Array.from(slider.querySelectorAll('.hero-slide'));
  const dotsWrap = document.querySelector('.hero-dots');
  if (slides.length === 0) return;

  let current = 0;
  let timer;

  function show(index) {
    slides.forEach((s, i) => s.classList.toggle('active', i === index));
    if (dotsWrap) {
      dotsWrap.querySelectorAll('.hero-dot').forEach((d, i) => d.classList.toggle('active', i === index));
    }
    current = index;
  }

  function next() { show((current + 1) % slides.length); }

  if (dotsWrap) {
    slides.forEach((_, i) => {
      const dot = document.createElement('button');
      dot.className = 'hero-dot' + (i === 0 ? ' active' : '');
      dot.setAttribute('aria-label', 'Chuyá»ƒn slide ' + (i + 1));
      dot.addEventListener('click', () => { show(i); restart(); });
      dotsWrap.appendChild(dot);
    });
  }

  function restart() {
    clearInterval(timer);
    timer = setInterval(next, 6000);
  }

  restart();
})();

// ============ Combo quantity steppers ============
document.addEventListener('click', function (e) {
  const btn = e.target.closest('.btn-qty');
  if (!btn) return;
  const row = btn.closest('.combo-qty');
  const input = row.querySelector('input[name$=".value"], input.qty-input');
  const comboId = row.dataset.comboId;
  const step = btn.dataset.act === 'plus' ? 1 : -1;
  if (input) {
    let val = parseInt(input.value, 10) || 0;
    val = Math.max(0, val + step);
    input.value = val;
    updateComboHidden(comboId, val);
  }
});

function updateComboHidden(comboId, val) {
  const hidden = document.querySelector(`input[name^="quantities[${comboId}]"]`);
  if (hidden) hidden.value = val;
}
/* ---------- Admin theme toggle (sang/toi) ---------- */
(function () {
  const body = document.body;
  const btn = document.getElementById('themeToggle');
  const icon = document.getElementById('themeIcon');
  if (!btn || !body.classList.contains('admin-area')) return;

  function apply(mode) {
    if (mode === 'light') {
      body.classList.add('light-mode');
      if (icon) icon.className = 'bi bi-moon-stars';
    } else {
      body.classList.remove('light-mode');
      if (icon) icon.className = 'bi bi-sun';
    }
  }

  apply(localStorage.getItem('galaxy-theme') === 'light' ? 'light' : 'dark');

  btn.addEventListener('click', function () {
    const next = body.classList.contains('light-mode') ? 'dark' : 'light';
    localStorage.setItem('galaxy-theme', next);
    apply(next);
  });
})();