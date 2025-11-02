(function () {
    const tokenInput = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
    const token = tokenInput ? tokenInput.value : '';

    function showToast(message, type = 'success') {
        if (window.Toastify) {
            Toastify({ text: message, duration: 3000, gravity: 'top', position: 'right', backgroundColor: type === 'success' ? '#2563eb' : '#ef4444' }).showToast();
        } else {
            alert(message);
        }
    }

    function post(url) {
        return axios.post(url, {}, { headers: { 'RequestVerificationToken': token } });
    }

    document.querySelectorAll('.js-cancel').forEach(btn => {
        btn.addEventListener('click', () => {
            const id = btn.dataset.id;
            if (!id) return;
            if (!confirm('Xác nhận huỷ đăng ký này?')) {
                return;
            }
            post(`/admin/registrations/cancel/${id}`).then(resp => {
                if (resp.data?.ok) {
                    showToast(resp.data.message || 'Đã huỷ đăng ký.');
                    window.location.reload();
                } else {
                    showToast(resp.data?.message || 'Không thể huỷ.', 'error');
                }
            }).catch(() => showToast('Không thể huỷ.', 'error'));
        });
    });

    document.querySelectorAll('.js-reregister').forEach(btn => {
        btn.addEventListener('click', () => {
            const id = btn.dataset.id;
            if (!id) return;
            post(`/admin/registrations/reregister/${id}`).then(resp => {
                if (resp.data?.ok) {
                    showToast(resp.data.message || 'Đã cập nhật.');
                    window.location.reload();
                } else {
                    showToast(resp.data?.message || 'Không thể cập nhật.', 'error');
                }
            }).catch(() => showToast('Không thể cập nhật.', 'error'));
        });
    });
})();
