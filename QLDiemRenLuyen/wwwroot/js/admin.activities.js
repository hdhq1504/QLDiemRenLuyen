(function () {
    const antiForm = document.getElementById('antiForgeryForm');
    const toastContainer = document.getElementById('toastContainer');
    const modalPlaceholder = document.getElementById('modalPlaceholder');
    const detailPlaceholder = document.getElementById('detailModalPlaceholder');

    const getToken = () => {
        const input = antiForm ? antiForm.querySelector('input[name="__RequestVerificationToken"]') : null;
        return input ? input.value : '';
    };

    const showToast = (message, type = 'success') => {
        if (!toastContainer) {
            alert(message);
            return;
        }
        const wrapper = document.createElement('div');
        wrapper.className = `toast align-items-center text-bg-${type === 'error' ? 'danger' : type === 'warning' ? 'warning' : 'success'} border-0`;
        wrapper.setAttribute('role', 'alert');
        wrapper.setAttribute('aria-live', 'assertive');
        wrapper.setAttribute('aria-atomic', 'true');
        wrapper.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Đóng"></button>
            </div>`;
        toastContainer.appendChild(wrapper);
        const toast = new bootstrap.Toast(wrapper, { delay: 4000 });
        toast.show();
        wrapper.addEventListener('hidden.bs.toast', () => wrapper.remove());
    };

    const handleJsonResponse = async (response) => {
        const contentType = response.headers.get('content-type');
        if (contentType && contentType.includes('application/json')) {
            return await response.json();
        }
        return null;
    };

    const wireEditForm = (modalEl) => {
        const form = modalEl.querySelector('#activityEditForm');
        if (!form) {
            return;
        }
        form.addEventListener('submit', async (event) => {
            event.preventDefault();
            const formData = new FormData(form);
            if (!formData.has('__RequestVerificationToken')) {
                formData.append('__RequestVerificationToken', getToken());
            }

            try {
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: formData
                });

                if (response.ok) {
                    const data = await handleJsonResponse(response);
                    bootstrap.Modal.getInstance(modalEl)?.hide();
                    showToast(data?.message ?? 'Thao tác thành công.');
                    setTimeout(() => window.location.reload(), 500);
                    return;
                }

                if (response.status === 400) {
                    const data = await handleJsonResponse(response);
                    displayFormErrors(form, data?.errors, data?.message);
                    return;
                }

                const errorData = await handleJsonResponse(response);
                showToast(errorData?.message ?? 'Không thể xử lý yêu cầu.', 'error');
            } catch (err) {
                console.error('Submit activity form error', err);
                showToast('Không thể xử lý yêu cầu. Vui lòng thử lại.', 'error');
            }
        });
    };

    const displayFormErrors = (form, errors, message) => {
        const summary = form.querySelector('#activityValidationSummary');
        if (summary) {
            if (message) {
                summary.textContent = message;
                summary.classList.remove('d-none');
            } else {
                summary.classList.add('d-none');
            }
        }
        const fieldMessages = form.querySelectorAll('[data-field]');
        fieldMessages.forEach(el => el.textContent = '');

        if (!errors) {
            return;
        }

        Object.keys(errors).forEach(key => {
            const field = form.querySelector(`[data-field="${key}"]`);
            if (field) {
                field.textContent = errors[key].join('\n');
            }
        });
    };

    const loadModal = async (url, placeholder, modalId, onReady) => {
        if (!placeholder) {
            return;
        }
        try {
            const response = await fetch(url, { method: 'GET' });
            if (!response.ok) {
                showToast('Không thể tải dữ liệu. Vui lòng thử lại.', 'error');
                return;
            }
            const html = await response.text();
            placeholder.innerHTML = html;
            const modalEl = placeholder.querySelector(modalId);
            if (!modalEl) {
                showToast('Không thể hiển thị nội dung.', 'error');
                return;
            }
            const modal = new bootstrap.Modal(modalEl);
            if (typeof onReady === 'function') {
                onReady(modalEl);
            }
            modal.show();
        } catch (err) {
            console.error('Load modal error', err);
            showToast('Không thể tải dữ liệu. Vui lòng thử lại.', 'error');
        }
    };

    const postAction = async (url, confirmMessage) => {
        if (confirmMessage && !window.confirm(confirmMessage)) {
            return;
        }
        const formData = new FormData();
        formData.append('__RequestVerificationToken', getToken());
        try {
            const response = await fetch(url, {
                method: 'POST',
                body: formData
            });
            const data = await handleJsonResponse(response);
            if (response.ok) {
                showToast(data?.message ?? 'Thao tác thành công.');
                setTimeout(() => window.location.reload(), 500);
            } else {
                showToast(data?.message ?? 'Không thể xử lý yêu cầu.', 'error');
            }
        } catch (err) {
            console.error('Post action error', err);
            showToast('Không thể xử lý yêu cầu.', 'error');
        }
    };

    const handleTableAction = (action, id) => {
        switch (action) {
            case 'edit':
                loadModal(`/admin/activities/edit/${id}`, modalPlaceholder, '#activityEditModal', wireEditForm);
                break;
            case 'detail':
                loadModal(`/admin/activities/detail/${id}`, detailPlaceholder, '#activityDetailModal');
                break;
            case 'open':
                postAction(`/admin/activities/open/${id}`);
                break;
            case 'close':
                postAction(`/admin/activities/close/${id}`);
                break;
            case 'cancel':
                postAction(`/admin/activities/cancel/${id}`, 'Bạn có chắc chắn muốn hủy hoạt động này?');
                break;
            case 'full':
                postAction(`/admin/activities/full/${id}`, 'Đánh dấu hoạt động đã đủ chỗ?');
                break;
            case 'approve':
                postAction(`/admin/activities/approve/${id}`, 'Phê duyệt hoạt động này?');
                break;
            case 'reject':
                postAction(`/admin/activities/reject/${id}`, 'Từ chối hoạt động này?');
                break;
            case 'delete':
                postAction(`/admin/activities/delete/${id}`, 'Xóa hoạt động này? Hành động không thể hoàn tác.');
                break;
            default:
                break;
        }
    };

    document.addEventListener('click', (event) => {
        const trigger = event.target.closest('[data-action]');
        if (!trigger) {
            return;
        }
        const action = trigger.getAttribute('data-action');
        const id = trigger.getAttribute('data-id');
        if (!action || !id) {
            return;
        }
        event.preventDefault();
        handleTableAction(action, id);
    });

    const createButton = document.getElementById('btnCreateActivity');
    if (createButton) {
        createButton.addEventListener('click', () => {
            loadModal('/admin/activities/create', modalPlaceholder, '#activityEditModal', wireEditForm);
        });
    }
})();
