(function () {
    const page = document.getElementById('student-feedback-page');
    if (!page) {
        return;
    }

    const modalContainer = document.getElementById('feedbackModalContainer');
    const detailContainer = document.getElementById('feedbackDetailContainer');
    const toastArea = document.getElementById('toastArea');
    const globalTokenInput = document.querySelector('input[name="__RequestVerificationToken"]');

    const getToken = () => (globalTokenInput ? globalTokenInput.value : '');

    const showToast = (message, type = 'success') => {
        if (!toastArea) {
            return;
        }
        const wrapper = document.createElement('div');
        wrapper.className = 'toast align-items-center text-white border-0';
        wrapper.classList.add(type === 'error' ? 'bg-danger' : 'bg-success');
        wrapper.setAttribute('role', 'alert');
        wrapper.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Đóng"></button>
            </div>`;
        toastArea.appendChild(wrapper);
        const toast = new bootstrap.Toast(wrapper, { delay: 4000 });
        toast.show();
        wrapper.addEventListener('hidden.bs.toast', () => wrapper.remove());
    };

    const extractErrors = (data) => {
        if (!data) {
            return null;
        }
        if (typeof data.message === 'string' && data.message) {
            return data.message;
        }
        if (data.errors) {
            const messages = Object.values(data.errors)
                .filter(Boolean)
                .flat()
                .filter(Boolean);
            if (messages.length > 0) {
                return messages.join('<br/>');
            }
        }
        return null;
    };

    const bindEditModal = () => {
        const modalEl = document.getElementById('feedbackEditModal');
        if (!modalEl) {
            return;
        }
        const form = modalEl.querySelector('#feedbackEditForm');
        if (!form) {
            return;
        }
        const errorsBox = form.querySelector('#feedbackEditErrors');
        const statusInput = form.querySelector('input[name="SubmitStatus"]');

        const handleSubmit = async (status) => {
            if (!statusInput) {
                return;
            }
            statusInput.value = status;
            const formData = new FormData(form);
            if (!formData.has('__RequestVerificationToken')) {
                formData.append('__RequestVerificationToken', getToken());
            }

            if (errorsBox) {
                errorsBox.classList.add('d-none');
                errorsBox.innerHTML = '';
            }

            try {
                const response = await fetch(form.getAttribute('action'), {
                    method: 'POST',
                    body: formData,
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.ok) {
                    const errorData = await response.json().catch(() => ({}));
                    const msg = extractErrors(errorData) || 'Không thể lưu phản hồi.';
                    if (errorsBox) {
                        errorsBox.classList.remove('d-none');
                        errorsBox.innerHTML = msg;
                    }
                    return;
                }

                const data = await response.json();
                if (!data.ok) {
                    const msg = extractErrors(data) || 'Không thể lưu phản hồi.';
                    if (errorsBox) {
                        errorsBox.classList.remove('d-none');
                        errorsBox.innerHTML = msg;
                    }
                    return;
                }

                const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
                modal.hide();
                showToast(data.message || 'Thao tác thành công.');
                setTimeout(() => window.location.reload(), 500);
            } catch (error) {
                console.error('feedback submit error', error);
                if (errorsBox) {
                    errorsBox.classList.remove('d-none');
                    errorsBox.innerHTML = 'Không thể lưu phản hồi. Vui lòng thử lại.';
                }
            }
        };

        modalEl.querySelectorAll('button[data-submit]').forEach((btn) => {
            btn.addEventListener('click', () => {
                const status = btn.getAttribute('data-submit') || 'SUBMITTED';
                handleSubmit(status);
            });
        });
    };

    const openModal = async (url, container, modalId, onShown) => {
        if (!container) {
            return;
        }
        try {
            const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) {
                showToast('Không thể tải dữ liệu. Vui lòng thử lại.', 'error');
                return;
            }
            const html = await response.text();
            container.innerHTML = html;
            const modalEl = document.getElementById(modalId);
            if (!modalEl) {
                showToast('Không thể khởi tạo modal.', 'error');
                return;
            }
            const modal = new bootstrap.Modal(modalEl);
            modalEl.addEventListener('shown.bs.modal', () => {
                if (onShown) {
                    onShown();
                }
            }, { once: true });
            modal.show();
        } catch (error) {
            console.error('open modal error', error);
            showToast('Không thể tải dữ liệu.', 'error');
        }
    };

    const handleDelete = async (id) => {
        if (!id) {
            return;
        }
        if (!confirm('Bạn có chắc muốn xóa phản hồi này?')) {
            return;
        }

        const formData = new FormData();
        formData.append('__RequestVerificationToken', getToken());

        try {
            const response = await fetch(`/student/feedback/delete/${id}`, {
                method: 'POST',
                body: formData,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                const data = await response.json().catch(() => ({}));
                showToast(extractErrors(data) || 'Không thể xóa phản hồi.', 'error');
                return;
            }

            const data = await response.json();
            if (!data.ok) {
                showToast(extractErrors(data) || 'Không thể xóa phản hồi.', 'error');
                return;
            }

            showToast(data.message || 'Đã xóa phản hồi.');
            setTimeout(() => window.location.reload(), 500);
        } catch (error) {
            console.error('delete feedback error', error);
            showToast('Không thể xóa phản hồi. Vui lòng thử lại.', 'error');
        }
    };

    const bindEvents = () => {
        const createBtn = document.getElementById('btnCreateFeedback');
        if (createBtn) {
            createBtn.addEventListener('click', () => {
                openModal('/student/feedback/create', modalContainer, 'feedbackEditModal', bindEditModal);
            });
        }

        page.addEventListener('click', (ev) => {
            const target = ev.target.closest('button');
            if (!target) {
                return;
            }

            if (target.classList.contains('btn-feedback-edit')) {
                const id = target.getAttribute('data-id');
                openModal(`/student/feedback/edit/${id}`, modalContainer, 'feedbackEditModal', bindEditModal);
            } else if (target.classList.contains('btn-feedback-detail')) {
                const id = target.getAttribute('data-id');
                openModal(`/student/feedback/detail/${id}`, detailContainer, 'feedbackDetailModal');
            } else if (target.classList.contains('btn-feedback-delete')) {
                const id = target.getAttribute('data-id');
                handleDelete(id);
            }
        });
    };

    bindEvents();
})();
