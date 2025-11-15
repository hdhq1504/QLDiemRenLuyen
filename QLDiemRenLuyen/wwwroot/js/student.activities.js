(function () {
    document.addEventListener('DOMContentLoaded', function () {
        const page = document.getElementById('student-activities-page');
        if (!page) {
            return;
        }

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const csrfToken = tokenInput ? tokenInput.value : '';
        const toastArea = document.getElementById('toastArea');
        const modalElement = document.getElementById('activityDetailModal');
        const modalContent = document.getElementById('activityDetailModalContent');

        const statusClassMap = {
            OPEN: 'badge bg-success status-badge',
            CANCELLED: 'badge bg-danger status-badge'
        };

        const stateClassMap = {
            REGISTERED: 'badge bg-primary student-state-badge',
            CHECKED_IN: 'badge bg-info text-dark student-state-badge'
        };

        function getStatusClass(status) {
            const key = (status || '').toUpperCase();
            return statusClassMap[key] || 'badge bg-secondary status-badge';
        }

        function getStateClass(state) {
            const key = (state || '').toUpperCase();
            return stateClassMap[key] || 'badge bg-light text-muted student-state-badge';
        }

        function showToast(message, variant = 'success') {
            if (!toastArea) {
                return;
            }
            const toast = document.createElement('div');
            const normalized = (variant || 'success').toLowerCase();
            const allowed = ['success', 'danger', 'warning', 'info'];
            const tone = allowed.includes(normalized) ? normalized : 'success';
            toast.className = `toast align-items-center text-bg-${tone} border-0 shadow`;
            toast.setAttribute('role', 'alert');
            toast.setAttribute('aria-live', 'assertive');
            toast.setAttribute('aria-atomic', 'true');
            toast.innerHTML = `
                <div class="d-flex">
                    <div class="toast-body">${message}</div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Đóng"></button>
                </div>`;
            toastArea.appendChild(toast);
            const bsToast = bootstrap.Toast.getOrCreateInstance(toast, { delay: 4000 });
            toast.addEventListener('hidden.bs.toast', function () {
                toast.remove();
            });
            bsToast.show();
        }

        function updateCard(activity) {
            if (!activity) {
                return;
            }
            const wrapper = document.querySelector(`[data-activity-id="${activity.id}"]`);
            if (!wrapper) {
                return;
            }
            const statusBadge = wrapper.querySelector('.status-badge');
            if (statusBadge) {
                statusBadge.className = getStatusClass(activity.status);
                statusBadge.dataset.status = activity.status;
                statusBadge.textContent = activity.status;
            }
            const stateBadge = wrapper.querySelector('.student-state-badge');
            if (stateBadge) {
                stateBadge.className = getStateClass(activity.studentState);
                stateBadge.dataset.state = activity.studentState;
                stateBadge.textContent = activity.studentState;
            }
            const registered = wrapper.querySelector('.registered-count');
            if (registered) {
                registered.textContent = activity.registeredCount;
            }
            const maxSeats = wrapper.querySelector('.max-seats');
            if (maxSeats) {
                if (activity.maxSeats !== null && activity.maxSeats !== undefined) {
                    maxSeats.textContent = `Tối đa ${activity.maxSeats}`;
                    maxSeats.dataset.max = activity.maxSeats;
                } else {
                    maxSeats.textContent = 'Không giới hạn';
                    maxSeats.dataset.max = 'unlimited';
                }
            }
            const actionRow = wrapper.querySelector('.mt-auto.d-flex');
            if (actionRow) {
                const detailUrl = actionRow.querySelector('.btn-detail')?.dataset.detailUrl || `/student/activities/${activity.id}`;
                let html = '';
                const status = (activity.status || '').toUpperCase();
                const state = (activity.studentState || '').toUpperCase();
                if (state === 'CHECKED_IN') {
                    html += '<button type="button" class="btn btn-outline-success w-100" disabled><i class="fa fa-check me-1"></i>Đã điểm danh</button>';
                } else if (status === 'OPEN' && state === 'NOT_REGISTERED') {
                    html += `<button type="button" class="btn btn-primary w-100 btn-register" data-action-url="/student/activities/${activity.id}/register"><i class="fa fa-check me-1"></i>Đăng ký</button>`;
                } else if (status === 'OPEN' && state === 'REGISTERED') {
                    html += `<button type="button" class="btn btn-outline-danger w-100 btn-unregister" data-action-url="/student/activities/${activity.id}/unregister"><i class="fa fa-times me-1"></i>Hủy đăng ký</button>`;
                } else {
                    const disabledLabel = status === 'CANCELLED' ? 'Hoạt động đã huỷ' : 'Đã đóng';
                    const disabledIcon = status === 'CANCELLED' ? 'fa-ban' : 'fa-lock';
                    html += `<button type="button" class="btn btn-secondary w-100" disabled><i class="fa ${disabledIcon} me-1"></i>${disabledLabel}</button>`;
                }
                html += `<button type="button" class="btn btn-light border btn-detail" data-detail-url="${detailUrl}" data-bs-toggle="modal" data-bs-target="#activityDetailModal"><i class="fa fa-info-circle me-1"></i>Chi tiết</button>`;
                actionRow.innerHTML = html;
            }
        }

        function updateModalActivity(activity) {
            if (!activity || !modalContent) {
                return;
            }
            if (modalContent.dataset.activityId !== String(activity.id)) {
                return;
            }
            const statusBadge = modalContent.querySelector('.status-badge');
            if (statusBadge) {
                statusBadge.className = getStatusClass(activity.status);
                statusBadge.dataset.status = activity.status;
                statusBadge.textContent = activity.status;
            }
            const stateBadge = modalContent.querySelector('.student-state-badge');
            if (stateBadge) {
                stateBadge.className = getStateClass(activity.studentState);
                stateBadge.dataset.state = activity.studentState;
                stateBadge.textContent = activity.studentState;
            }
            const registered = modalContent.querySelector('.modal-body .fa-users')?.parentElement;
            if (registered) {
                const countEl = registered.querySelector('.registered-count');
                const maxSeats = registered.querySelector('.max-seats');
                if (countEl) {
                    countEl.textContent = activity.registeredCount;
                }
                if (maxSeats) {
                    if (activity.maxSeats !== null && activity.maxSeats !== undefined) {
                        maxSeats.textContent = `Tối đa ${activity.maxSeats}`;
                        maxSeats.dataset.max = activity.maxSeats;
                    } else {
                        maxSeats.textContent = 'Không giới hạn';
                        maxSeats.dataset.max = 'unlimited';
                    }
                }
            }
            const footer = modalContent.querySelector('.modal-footer');
            if (footer) {
                let footerHtml = '';
                const status = (activity.status || '').toUpperCase();
                const state = (activity.studentState || '').toUpperCase();
                if (state === 'CHECKED_IN') {
                    footerHtml += '<button type="button" class="btn btn-outline-success" disabled><i class="fa fa-check me-2"></i>Đã điểm danh</button>';
                } else if (status === 'OPEN' && state === 'NOT_REGISTERED') {
                    footerHtml += `<button type="button" class="btn btn-primary btn-register" data-action-url="/student/activities/${activity.id}/register"><i class="fa fa-check me-2"></i>Đăng ký tham gia</button>`;
                } else if (status === 'OPEN' && state === 'REGISTERED') {
                    footerHtml += `<button type="button" class="btn btn-outline-danger btn-unregister" data-action-url="/student/activities/${activity.id}/unregister"><i class="fa fa-times me-2"></i>Hủy đăng ký</button>`;
                } else {
                    const disabledLabel = status === 'CANCELLED' ? 'Hoạt động đã huỷ' : 'Hoạt động đã đóng';
                    const disabledIcon = status === 'CANCELLED' ? 'fa-ban' : 'fa-lock';
                    footerHtml += `<button type="button" class="btn btn-secondary" disabled><i class="fa ${disabledIcon} me-2"></i>${disabledLabel}</button>`;
                }
                footerHtml += '<button type="button" class="btn btn-light border" data-bs-dismiss="modal">Đóng</button>';
                footer.innerHTML = footerHtml;
            }
        }

        async function postAction(url, button) {
            if (!url) {
                return;
            }
            if (button) {
                button.classList.add('disabled');
                button.setAttribute('disabled', 'disabled');
            }
            try {
                const response = await fetch(url, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json',
                        'X-CSRF-TOKEN': csrfToken,
                        'RequestVerificationToken': csrfToken
                    },
                    body: JSON.stringify({})
                });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                const data = await response.json();
                const successState = typeof data.success === 'boolean' ? data.success : !!data.ok;
                const toastVariant = (data.toastType || (successState ? 'success' : 'danger')).toLowerCase();
                if (!data.ok) {
                    showToast(data.message || 'Có lỗi xảy ra.', toastVariant);
                } else {
                    showToast(data.message || 'Thành công.', toastVariant);
                    if (data.activity) {
                        updateCard(data.activity);
                        updateModalActivity(data.activity);
                    }
                }
            } catch (err) {
                console.error(err);
                showToast('Không thể kết nối máy chủ.', 'danger');
            } finally {
                if (button) {
                    button.classList.remove('disabled');
                    button.removeAttribute('disabled');
                }
            }
        }

        document.body.addEventListener('click', function (evt) {
            const registerBtn = evt.target.closest('.btn-register');
            if (registerBtn) {
                evt.preventDefault();
                postAction(registerBtn.dataset.actionUrl, registerBtn);
                return;
            }
            const unregisterBtn = evt.target.closest('.btn-unregister');
            if (unregisterBtn) {
                evt.preventDefault();
                postAction(unregisterBtn.dataset.actionUrl, unregisterBtn);
                return;
            }
            const reminderBtn = evt.target.closest('.btn-send-reminders');
            if (reminderBtn) {
                evt.preventDefault();
                postAction(reminderBtn.dataset.actionUrl, reminderBtn);
            }
        });

        if (modalElement) {
            modalElement.addEventListener('show.bs.modal', function (event) {
                const trigger = event.relatedTarget;
                if (!trigger) {
                    return;
                }
                const url = trigger.getAttribute('data-detail-url');
                if (!url || !modalContent) {
                    return;
                }
                modalContent.innerHTML = `
                    <div class="modal-header">
                        <h5 class="modal-title">Đang tải...</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button>
                    </div>
                    <div class="modal-body">
                        <div class="text-center text-muted py-4">
                            <div class="spinner-border" role="status"></div>
                            <p class="mt-3">Đang tải thông tin hoạt động...</p>
                        </div>
                    </div>`;
                fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                    .then(resp => resp.text())
                    .then(html => {
                        modalContent.innerHTML = html;
                        const urlParts = url.split('/').filter(Boolean);
                        const idFromUrl = urlParts[urlParts.length - 1] || '';
                        modalContent.dataset.activityId = idFromUrl || trigger.closest('[data-activity-id]')?.dataset.activityId || '';
                    })
                    .catch(() => {
                        modalContent.innerHTML = `
                            <div class="modal-header">
                                <h5 class="modal-title">Lỗi</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button>
                            </div>
                            <div class="modal-body">
                                <p class="text-danger mb-0">Không thể tải dữ liệu hoạt động.</p>
                            </div>`;
                    });
            });
        }
    });
})();