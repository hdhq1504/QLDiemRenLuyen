(function () {
    document.addEventListener('DOMContentLoaded', function () {
        const page = document.getElementById('student-notifications-page');
        if (!page) {
            return;
        }

        const tokenInput = document.querySelector('#notificationsAntiforgeryForm input[name="__RequestVerificationToken"]');
        const csrfToken = tokenInput ? tokenInput.value : '';
        const toastArea = document.getElementById('toastArea');
        const statusSelect = document.getElementById('status');
        const btnMarkAllRead = document.getElementById('btnMarkAllRead');
        const unreadCountEl = document.getElementById('unreadCount');
        const modalElement = document.getElementById('notificationDetailModal');
        const modalContent = document.getElementById('notificationDetailModalContent');

        function showToast(message, success) {
            if (!toastArea) {
                return;
            }
            const toast = document.createElement('div');
            toast.className = `toast align-items-center text-bg-${success ? 'success' : 'danger'} border-0 shadow`;
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

        function updateUnreadCount(value) {
            if (!unreadCountEl) {
                return;
            }
            const num = Number(value);
            unreadCountEl.textContent = Number.isFinite(num) && num >= 0 ? num : 0;
        }

        function buildActionButtons(id, isRead) {
            const viewBtn = `<button type="button" class="btn btn-outline-primary btn-view-notification" data-detail-url="/student/notifications/detail/${id}" data-notification-id="${id}" data-bs-toggle="modal" data-bs-target="#notificationDetailModal"><i class="fa fa-eye"></i></button>`;
            const toggleBtn = isRead
                ? `<button type="button" class="btn btn-outline-warning btn-mark-unread" data-action-url="/student/notifications/mark-unread/${id}" data-notification-id="${id}"><i class="fa fa-undo"></i></button>`
                : `<button type="button" class="btn btn-outline-success btn-mark-read" data-action-url="/student/notifications/mark-read/${id}" data-notification-id="${id}"><i class="fa fa-check"></i></button>`;
            return viewBtn + toggleBtn;
        }

        function updateRowState(id, isRead) {
            const row = document.querySelector(`tr[data-notification-id="${id}"]`);
            if (!row) {
                return;
            }
            row.dataset.isRead = isRead ? '1' : '0';
            if (isRead) {
                row.classList.remove('notification-row-unread');
            } else {
                row.classList.add('notification-row-unread');
            }
            const badge = row.querySelector('.notification-status');
            if (badge) {
                badge.className = isRead ? 'badge bg-secondary notification-status' : 'badge bg-warning text-dark notification-status';
                badge.dataset.status = isRead ? 'read' : 'unread';
                badge.textContent = isRead ? 'Đã đọc' : 'Chưa đọc';
            }
            const group = row.querySelector('.btn-group');
            if (group) {
                group.innerHTML = buildActionButtons(id, isRead);
            }
        }

        function updateModalState(id, isRead) {
            if (!modalContent) {
                return;
            }
            const body = modalContent.querySelector('.modal-body');
            if (!body || body.dataset.notificationId !== String(id)) {
                return;
            }
            body.dataset.isRead = isRead ? '1' : '0';
            const footer = modalContent.querySelector('.modal-footer');
            if (footer) {
                let html = '';
                if (!isRead) {
                    html += `<button type="button" class="btn btn-success btn-mark-read" data-action-url="/student/notifications/mark-read/${id}" data-notification-id="${id}"><i class="fa fa-check me-2"></i>Đánh dấu đã đọc</button>`;
                } else {
                    html += `<button type="button" class="btn btn-outline-warning btn-mark-unread" data-action-url="/student/notifications/mark-unread/${id}" data-notification-id="${id}"><i class="fa fa-undo me-2"></i>Để lại chưa đọc</button>`;
                }
                html += '<button type="button" class="btn btn-light border" data-bs-dismiss="modal">Đóng</button>';
                footer.innerHTML = html;
            }
        }

        function extractIdFromButton(button) {
            if (!button) {
                return null;
            }
            const id = button.dataset.notificationId;
            if (id) {
                return id;
            }
            const row = button.closest('[data-notification-id]');
            return row ? row.dataset.notificationId : null;
        }

        async function postAction(url) {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'RequestVerificationToken': csrfToken,
                    'X-CSRF-TOKEN': csrfToken
                },
                body: JSON.stringify({})
            });
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            return response.json();
        }

        async function handleToggle(button, makeRead) {
            const url = button.dataset.actionUrl;
            const id = extractIdFromButton(button);
            if (!url || !id) {
                return;
            }
            button.disabled = true;
            button.classList.add('disabled');
            try {
                const data = await postAction(url);
                if (!data.ok) {
                    showToast(data.message || 'Có lỗi xảy ra.', false);
                    return;
                }
                updateUnreadCount(data.unread);
                const newState = typeof data.isRead === 'boolean' ? data.isRead : makeRead;
                updateRowState(id, newState);
                updateModalState(id, newState);
                showToast(data.message || 'Thao tác thành công.', true);
            } catch (err) {
                console.error(err);
                showToast('Không thể kết nối máy chủ.', false);
            } finally {
                button.disabled = false;
                button.classList.remove('disabled');
            }
        }

        async function handleMarkAll() {
            const url = '/student/notifications/mark-all-read';
            btnMarkAllRead.disabled = true;
            btnMarkAllRead.classList.add('disabled');
            try {
                const data = await postAction(url);
                if (!data.ok) {
                    showToast(data.message || 'Có lỗi xảy ra.', false);
                    return;
                }
                updateUnreadCount(data.unread);
                document.querySelectorAll('tr[data-notification-id]').forEach(function (row) {
                    const id = row.dataset.notificationId;
                    if (id) {
                        updateRowState(id, true);
                    }
                });
                const body = modalContent ? modalContent.querySelector('.modal-body') : null;
                if (body && body.dataset.notificationId) {
                    updateModalState(body.dataset.notificationId, true);
                }
                showToast(data.message || 'Đã cập nhật thành công.', true);
            } catch (err) {
                console.error(err);
                showToast('Không thể kết nối máy chủ.', false);
            } finally {
                btnMarkAllRead.disabled = false;
                btnMarkAllRead.classList.remove('disabled');
            }
        }

        async function loadDetail(button) {
            if (!modalContent) {
                return;
            }
            const url = button.dataset.detailUrl;
            const id = extractIdFromButton(button);
            if (!url || !id) {
                return;
            }
            modalContent.innerHTML = '<div class="modal-body p-5 text-center text-muted"><div class="spinner-border mb-3" role="status"></div><div>Đang tải...</div></div>';
            try {
                const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                const html = await response.text();
                modalContent.innerHTML = html;
                modalContent.dataset.notificationId = id;
            } catch (err) {
                console.error(err);
                modalContent.innerHTML = '<div class="modal-body p-4 text-danger">Không tải được nội dung thông báo.</div>';
            }
        }

        if (statusSelect) {
            statusSelect.addEventListener('change', function () {
                const form = document.getElementById('notificationFilterForm');
                if (form) {
                    form.submit();
                }
            });
        }

        if (btnMarkAllRead) {
            btnMarkAllRead.addEventListener('click', function (evt) {
                evt.preventDefault();
                handleMarkAll();
            });
        }

        page.addEventListener('click', function (evt) {
            const button = evt.target.closest('button');
            if (!button) {
                return;
            }
            if (button.classList.contains('btn-view-notification')) {
                loadDetail(button);
            } else if (button.classList.contains('btn-mark-read')) {
                evt.preventDefault();
                handleToggle(button, true);
            } else if (button.classList.contains('btn-mark-unread')) {
                evt.preventDefault();
                handleToggle(button, false);
            }
        });

        if (modalElement) {
            modalElement.addEventListener('click', function (evt) {
                const button = evt.target.closest('button');
                if (!button) {
                    return;
                }
                if (button.classList.contains('btn-mark-read')) {
                    evt.preventDefault();
                    handleToggle(button, true);
                } else if (button.classList.contains('btn-mark-unread')) {
                    evt.preventDefault();
                    handleToggle(button, false);
                }
            });
        }
    });
})();