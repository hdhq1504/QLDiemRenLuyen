(function () {
    const tokenInput = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
    const token = tokenInput ? tokenInput.value : '';

    function showToast(message, type = 'success') {
        if (window.Toastify) {
            Toastify({ text: message, duration: 3000, gravity: 'top', position: 'right', backgroundColor: type === 'success' ? '#059669' : '#ef4444' }).showToast();
        } else {
            alert(message);
        }
    }

    function post(url, data) {
        const headers = { 'RequestVerificationToken': token };
        return axios.post(url, data, { headers });
    }

    document.querySelectorAll('.js-mark').forEach(btn => {
        btn.addEventListener('click', () => handleMark(btn, true));
    });

    document.querySelectorAll('.js-unmark').forEach(btn => {
        btn.addEventListener('click', () => handleMark(btn, false));
    });

    function handleMark(button, isMark) {
        const id = button.dataset.id;
        if (!id) return;
        const url = isMark ? `/admin/attendance/mark/${id}` : `/admin/attendance/unmark/${id}`;
        post(url, {}).then(resp => {
            if (resp.data?.ok) {
                showToast(resp.data.message || 'Đã cập nhật.');
                window.location.reload();
            } else {
                showToast(resp.data?.message || 'Không thể cập nhật.', 'error');
            }
        }).catch(() => showToast('Không thể cập nhật.', 'error'));
    }

    const importForm = document.getElementById('importForm');
    if (importForm) {
        importForm.addEventListener('submit', evt => {
            evt.preventDefault();
            const formData = new FormData(importForm);
            if (!formData.get('csvFile')) {
                showToast('Vui lòng chọn tập tin CSV.', 'error');
                return;
            }
            formData.append('__RequestVerificationToken', token);
            post('/admin/attendance/importcsv', formData).then(resp => {
                if (resp.data?.ok) {
                    showToast(resp.data.message || 'Đã nhập thành công.');
                    window.location.reload();
                } else {
                    showToast(resp.data?.message || 'Không thể nhập dữ liệu.', 'error');
                }
            }).catch(() => showToast('Không thể nhập dữ liệu.', 'error'));
        });
    }
})();
