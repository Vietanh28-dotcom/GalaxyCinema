// ============ Galaxy Cinema - seat map (real-time AJAX) ============
(function () {
  const map = document.getElementById('seat-map');
  if (!map) return;

  const showtimeId = map.dataset.showtimeId;
  const form = document.getElementById('seats-form');
  const seatInputs = document.getElementById('seat-ids-container');
  const totalEl = document.getElementById('seat-total');
  const countEl = document.getElementById('seat-count');
  const continueBtn = document.getElementById('btn-continue');
  const msgBox = document.getElementById('seat-msg');
  const antiForgery = document.querySelector('input[name="__RequestVerificationToken"]');

  const selected = new Map(); // seatId -> seat element
  const seats = Array.from(map.querySelectorAll('.seat'));

  function showMsg(text, isError) {
    if (!msgBox) return;
    msgBox.textContent = text;
    msgBox.classList.toggle('alert-danger', !!isError);
    msgBox.classList.toggle('alert-success', !isError);
    msgBox.classList.remove('d-none');
    if (text) {
      clearTimeout(showMsg._t);
      showMsg._t = setTimeout(() => msgBox.classList.add('d-none'), 5000);
    }
  }

  function fmt(n) { return n.toLocaleString('vi-VN') + ' ₫'; }

  function syncHiddenInputs() {
    if (!seatInputs) return;
    seatInputs.innerHTML = '';
    for (const id of selected.keys()) {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = 'seatIds';
      input.value = id;
      seatInputs.appendChild(input);
    }
  }

  function refreshSummary() {
    let total = 0;
    for (const seat of selected.values()) {
      total += parseFloat(seat.dataset.price || '0');
    }
    if (totalEl) totalEl.textContent = fmt(total);
    if (countEl) countEl.textContent = String(selected.size);
    if (continueBtn) continueBtn.disabled = selected.size === 0;
    syncHiddenInputs();
  }

  async function post(url, data) {
    const body = new URLSearchParams(data);
    body.set('__RequestVerificationToken', antiForgery ? antiForgery.value : '');
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
      body: body.toString()
    });
    return res.json();
  }

  async function lockSeat(seat) {
    try {
      const r = await post('/Booking/LockSeat', { showtimeId, seatId: seat.dataset.id });
      if (r.success) {
        seat.dataset.state = 'mine';
        seat.classList.add('selected');
        seat.classList.remove('available');
        selected.set(seat.dataset.id, seat);
      } else {
        syncSeatFromServer(seat);
        showMsg(r.message || 'Không thể chọn ghế này.', true);
      }
    } catch {
      showMsg('Lỗi kết nối, vui lòng thử lại.', true);
    }
    refreshSummary();
  }

  async function releaseSeat(seat) {
    try {
      const r = await post('/Booking/ReleaseSeat', { showtimeId, seatId: seat.dataset.id });
      if (!r || r.success === false) {
        showMsg('Không thể bỏ chọn ghế này. Vui lòng thử lại.', true);
        return;
      }
    } catch {
      showMsg('Lỗi kết nối, không thể bỏ chọn ghế. Vui lòng thử lại.', true);
      return;
    }
    seat.dataset.state = 'available';
    seat.classList.remove('selected', 'mine');
    selected.delete(seat.dataset.id);
    refreshSummary();
  }

  function syncSeatFromServer(seat) {
    seat.dataset.state = 'locked';
    seat.classList.add('locked-other');
    seat.classList.remove('selected', 'mine', 'available');
  }

  map.addEventListener('click', async function (e) {
    const seat = e.target.closest('.seat');
    if (!seat) return;
    const state = seat.dataset.state;
    if (state === 'booked' || state === 'locked') return;

    if (state === 'mine' || selected.has(seat.dataset.id)) {
      await releaseSeat(seat);
    } else if (state === 'available') {
      await lockSeat(seat);
    }
  });

  // Real-time sync với server
  async function poll() {
    try {
      const res = await fetch('/Booking/GetSeatStatus?showtimeId=' + showtimeId);
      const data = await res.json();
      for (const s of data.seats) {
        const seat = map.querySelector(`.seat[data-id="${s.id}"]`);
        if (!seat) continue;

        const state = s.state;

        if (state === 'booked') {
          const hadIt = selected.delete(s.id);
          if (hadIt) {
            seat.classList.remove('selected');
            showMsg('Ghế ' + s.code + ' vừa được khách khác đặt. Vui lòng chọn ghế khác.', true);
          }
          seat.dataset.state = 'booked';
          seat.classList.add('booked');
          seat.classList.remove('available', 'mine', 'selected', 'locked-other');
        } else if (state === 'locked') {
          const hadIt = selected.delete(s.id);
          if (hadIt) {
            seat.classList.remove('selected');
            showMsg('Ghế ' + s.code + ' đang được khách khác giữ. Hãy chọn ghế khác.', true);
          }
          seat.dataset.state = 'locked';
          seat.classList.add('locked-other');
          seat.classList.remove('available', 'mine', 'selected');
        } else if (state === 'mine') {
          if (!selected.has(s.id)) {
            selected.set(s.id, seat);
            seat.classList.add('selected');
          }
          seat.dataset.state = 'mine';
          seat.classList.remove('locked-other');
        } else {
          // available
          if (selected.has(s.id) && seat.dataset.state === 'mine') {
            // khóa của mình đã hết hạn
            selected.delete(s.id);
            seat.classList.remove('selected');
            showMsg('Thời gian giữ ghế ' + s.code + ' đã hết. Vui lòng chọn lại.', true);
          }
          seat.dataset.state = 'available';
          seat.classList.remove('booked', 'mine', 'selected', 'locked-other');
        }
      }
      refreshSummary();
    } catch { /* tạm bỏ qua lỗi mạng */ }
  }

  if (form) {
    form.addEventListener('submit', function (e) {
      if (selected.size === 0) {
        e.preventDefault();
        showMsg('Vui lòng chọn ít nhất một ghế.', true);
        return;
      }
      syncHiddenInputs();
    });
  }

  // Đồng bộ ghế đã chọn trước đó (khi quay lại trang)
  seats.forEach(s => {
    if (s.dataset.state === 'mine') {
      selected.set(s.dataset.id, s);
      s.classList.add('selected');
    }
  });
  refreshSummary();

  poll();
  setInterval(poll, 4000);
})();