(function () {
    const cfg = window.studentProfilePage || {};
    const tokenInput = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
    const profileRoot = document.querySelector('[data-profile]');
    const modalPlaceholder = document.getElementById('editProfileModalPlaceholder');
    const toastContainer = document.querySelector('.toast-container');
    const editBtn = document.getElementById('editProfileBtn');
    const avatarBtn = document.getElementById('changeAvatarBtn');
    const avatarInput = document.getElementById('avatarInput');

    if (!tokenInput || !profileRoot) {
        console.warn('Thiếu token hoặc profile root.');
        return;
    }

    const getToken = () => tokenInput.value;

    const showToast = (message, success = true) => {
        if (!toastContainer) return;
        const toastEl = document.createElement('div');
        toastEl.className = `toast align-items-center text-bg-${success ? 'success' : 'danger'} border-0`;
        toastEl.setAttribute('role', 'alert');
        toastEl.setAttribute('aria-live', 'assertive');
        toastEl.setAttribute('aria-atomic', 'true');
        toastEl.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>`;
        toastContainer.appendChild(toastEl);
        const toast = new bootstrap.Toast(toastEl, { delay: 4000 });
        toast.show();
        toastEl.addEventListener('hidden.bs.toast', () => toastEl.remove());
    };

    const updateProfileUI = (profile) => {
        if (!profile) return;
        const setText = (selector, value) => {
            const el = profileRoot.querySelector(selector);
            if (el) {
                el.textContent = value && value.trim ? value.trim() : (value ?? '-');
            }
        };

        setText('[data-fullname]', profile.fullName || '-');
        setText('[data-email]', profile.email || '-');
        setText('[data-role]', profile.roleName || '-');
        setText('[data-student-code]', profile.studentCode || '-');
        setText('[data-class]', profile.className || '-');
        setText('[data-department]', profile.departmentName || '-');
        setText('[data-gender]', profile.gender || '-');
        setText('[data-phone]', profile.phone || '-');
        setText('[data-address]', profile.address || '-');
        const dobEl = profileRoot.querySelector('[data-dob]');
        if (dobEl) {
            dobEl.textContent = profile.dob ? new Date(profile.dob).toLocaleDateString('vi-VN') : '-';
        }
    };

    const bindModalEvents = () => {
        const form = document.getElementById('editProfileForm');
        const saveBtn = document.getElementById('saveProfileBtn');
        if (!form || !saveBtn) return;

        saveBtn.addEventListener('click', async () => {
            const formData = new FormData(form);
            if (!formData.has('__RequestVerificationToken')) {
                formData.append('__RequestVerificationToken', getToken());
            }

            saveBtn.disabled = true;
            saveBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Đang lưu...';
            try {
                const response = await fetch(cfg.updateUrl, {
                    method: 'POST',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: formData
                });

                const result = await response.json().catch(() => null);
                if (!response.ok || !result) {
                    throw new Error(result?.message || 'Có lỗi xảy ra');
                }

                if (result.ok) {
                    updateProfileUI(result.profile);
                    showToast(result.message ?? 'Cập nhật thành công', true);
                    const modalEl = document.getElementById('editProfileModal');
                    if (modalEl) {
                        bootstrap.Modal.getInstance(modalEl)?.hide();
                    }
                } else {
                    showToast(result.message ?? 'Không thể cập nhật', false);
                }
            } catch (error) {
                console.error(error);
                showToast(error.message || 'Không thể cập nhật', false);
            } finally {
                saveBtn.disabled = false;
                saveBtn.innerHTML = '<i class="fa-solid fa-floppy-disk me-1"></i> Lưu thay đổi';
            }
        });
    };

    const openEditModal = async () => {
        try {
            const response = await fetch(cfg.editUrl, {
                method: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });
            if (!response.ok) {
                throw new Error('Không thể tải biểu mẫu');
            }
            const html = await response.text();
            modalPlaceholder.innerHTML = html;
            const modalEl = document.getElementById('editProfileModal');
            const modal = new bootstrap.Modal(modalEl);
            modal.show();
            bindModalEvents();
        } catch (error) {
            console.error(error);
            showToast(error.message || 'Không thể tải biểu mẫu', false);
        }
    };

    const uploadAvatar = async (file) => {
        const formData = new FormData();
        formData.append('avatar', file);
        formData.append('__RequestVerificationToken', getToken());

        try {
            const response = await fetch(cfg.avatarUrl, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            });
            const result = await response.json().catch(() => null);
            if (!response.ok || !result) {
                throw new Error(result?.message || 'Không thể đổi ảnh');
            }
            if (result.ok) {
                const img = document.querySelector('[data-avatar]');
                if (img && result.avatarUrl) {
                    img.src = result.avatarUrl + '?v=' + new Date().getTime();
                }
                showToast(result.message ?? 'Đổi ảnh thành công', true);
            } else {
                showToast(result.message ?? 'Không thể đổi ảnh', false);
            }
        } catch (error) {
            console.error(error);
            showToast(error.message || 'Không thể đổi ảnh', false);
        }
    };

    if (editBtn) {
        editBtn.addEventListener('click', openEditModal);
    }

    if (avatarBtn && avatarInput) {
        avatarBtn.addEventListener('click', () => avatarInput.click());
        avatarInput.addEventListener('change', () => {
            const file = avatarInput.files && avatarInput.files[0];
            if (!file) return;
            if (file.size > 2 * 1024 * 1024) {
                showToast('Ảnh vượt quá 2MB', false);
                avatarInput.value = '';
                return;
            }
            const ext = file.name.split('.').pop()?.toLowerCase();
            if (!['jpg', 'jpeg', 'png'].includes(ext || '')) {
                showToast('Định dạng ảnh không hỗ trợ', false);
                avatarInput.value = '';
                return;
            }
            uploadAvatar(file);
        });
    }
})();
