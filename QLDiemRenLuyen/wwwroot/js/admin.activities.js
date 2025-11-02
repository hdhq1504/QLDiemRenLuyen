(function () {
    const tokenInput = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
    const token = tokenInput ? tokenInput.value : '';

    function showToast(message, type = 'success') {
        if (window.Toastify) {
            Toastify({
                text: message,
                duration: 3000,
                gravity: 'top',
                position: 'right',
                backgroundColor: type === 'success' ? '#16a34a' : '#ef4444'
            }).showToast();
        } else {
            alert(message);
        }
    }

    function post(url, data) {
        const headers = { 'RequestVerificationToken': token };
        if (data instanceof FormData) {
            return axios.post(url, data, { headers });
        }
        return axios.post(url, data, { headers });
    }

    function handleAction(selector, urlBuilder, successMessage) {
        document.querySelectorAll(selector).forEach(btn => {
            btn.addEventListener('click', () => {
                const id = btn.getAttribute('data-id');
                if (!id) return;
                if (btn.classList.contains('js-delete') && !confirm('Bạn chắc chắn muốn xoá hoạt động này?')) {
                    return;
                }
                post(urlBuilder(id), {}).then(resp => {
                    if (resp.data?.ok) {
                        showToast(successMessage);
                        window.location.reload();
                    } else {
                        showToast(resp.data?.message || 'Thao tác thất bại.', 'error');
                    }
                }).catch(() => showToast('Thao tác thất bại.', 'error'));
            });
        });
    }

    handleAction('.js-open', id => `/admin/activities/open/${id}`, 'Đã mở đăng ký.');
    handleAction('.js-close', id => `/admin/activities/close/${id}`, 'Đã cập nhật trạng thái.');
    handleAction('.js-submit', id => `/admin/activities/submit/${id}`, 'Đã gửi phê duyệt.');
    handleAction('.js-delete', id => `/admin/activities/delete/${id}`, 'Đã xoá hoạt động.');

    const approveModalEl = document.getElementById('approveModal');
    if (!approveModalEl) {
        return;
    }

    const approveModal = new bootstrap.Modal(approveModalEl);
    const approveForm = approveModalEl.querySelector('#approveForm');
    const reasonInput = approveForm.querySelector('textarea[name="reason"]');
    const activityInput = approveForm.querySelector('input[name="activityId"]');
    let currentAction = 'approve';

    document.querySelectorAll('.js-approve').forEach(btn => {
        btn.addEventListener('click', () => {
            currentAction = 'approve';
            activityInput.value = btn.getAttribute('data-id');
            reasonInput.value = '';
            approveModal.show();
        });
    });

    document.querySelectorAll('.js-reject').forEach(btn => {
        btn.addEventListener('click', () => {
            currentAction = 'reject';
            activityInput.value = btn.getAttribute('data-id');
            reasonInput.value = '';
            approveModal.show();
        });
    });

    approveModalEl.querySelector('#btnApproveConfirm')?.addEventListener('click', () => {
        submitDecision('approve');
    });

    approveModalEl.querySelector('#btnRejectConfirm')?.addEventListener('click', () => {
        submitDecision('reject');
    });

    function submitDecision(actionOverride) {
        const action = actionOverride || currentAction;
        const id = activityInput.value;
        if (!id) return;

        const formData = new FormData();
        formData.append('__RequestVerificationToken', token);
        if (action === 'reject') {
            formData.append('reason', reasonInput.value || '');
        }

        const url = action === 'approve'
            ? `/admin/activities/approve/${id}`
            : `/admin/activities/reject/${id}`;

        post(url, formData).then(resp => {
            if (resp.data?.ok) {
                showToast(resp.data.message || 'Thao tác thành công.');
                approveModal.hide();
                window.location.reload();
            } else {
                showToast(resp.data?.message || 'Không thể cập nhật.', 'error');
            }
        }).catch(() => showToast('Không thể cập nhật.', 'error'));
    }
})();
