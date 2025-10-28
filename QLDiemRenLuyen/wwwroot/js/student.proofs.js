(function () {
    const ALLOWED_TYPES = ['application/pdf', 'image/png', 'image/jpeg'];
    const MAX_SIZE = 5 * 1024 * 1024; // 5MB

    document.addEventListener('DOMContentLoaded', function () {
        const page = document.getElementById('student-proofs-page');
        if (!page) {
            return;
        }

        const toastArea = document.getElementById('toastArea');
        const modalContainer = document.getElementById('uploadModalContainer');
        const uploadButton = document.getElementById('btnOpenUpload');
        const filterActivitySelect = document.getElementById('activityId');
        const globalTokenInput = document.querySelector('#student-proofs-page input[name="__RequestVerificationToken"]');
        const globalToken = globalTokenInput ? globalTokenInput.value : '';

        function showToast(message, success) {
            if (!toastArea || typeof bootstrap === 'undefined') {
                alert(message);
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

        async function openUploadModal(activityId) {
            try {
                const url = activityId ? `/student/proofs/upload?activityId=${activityId}` : '/student/proofs/upload';
                const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                const html = await response.text();
                modalContainer.innerHTML = html;
                const modalElement = modalContainer.querySelector('#uploadProofModal');
                if (!modalElement) {
                    showToast('Không thể hiển thị form tải lên.', false);
                    return;
                }
                const bsModal = new bootstrap.Modal(modalElement);
                bindUploadForm(modalElement, bsModal);
                bsModal.show();
            } catch (error) {
                console.error(error);
                showToast('Không thể tải biểu mẫu tải lên.', false);
            }
        }

        function bindUploadForm(modalElement, bsModal) {
            const form = modalElement.querySelector('#uploadProofForm');
            if (!form) {
                return;
            }
            form.addEventListener('submit', async function (evt) {
                evt.preventDefault();
                const submitBtn = form.querySelector('button[type="submit"]');
                const fileInput = form.querySelector('input[type="file"][name="file"]');
                const activitySelect = form.querySelector('select[name="activityId"]');
                const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
                const token = tokenInput ? tokenInput.value : globalToken;

                if (!activitySelect || !activitySelect.value) {
                    showToast('Vui lòng chọn hoạt động.', false);
                    return;
                }

                if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
                    showToast('Vui lòng chọn tệp.', false);
                    return;
                }

                const file = fileInput.files[0];
                if (file.size > MAX_SIZE) {
                    showToast('Tệp vượt quá kích thước tối đa 5MB.', false);
                    return;
                }

                if (!ALLOWED_TYPES.includes(file.type)) {
                    showToast('Chỉ hỗ trợ PDF hoặc hình ảnh (PNG/JPG).', false);
                    return;
                }

                const formData = new FormData(form);

                if (submitBtn) {
                    submitBtn.disabled = true;
                    submitBtn.classList.add('disabled');
                }

                try {
                    const response = await fetch('/student/proofs/upload', {
                        method: 'POST',
                        body: formData,
                        headers: {
                            'RequestVerificationToken': token
                        }
                    });
                    const result = await response.json();
                    if (!response.ok || !result.ok) {
                        const message = result && result.message ? result.message : 'Không thể tải lên minh chứng.';
                        showToast(message, false);
                    } else {
                        showToast(result.message || 'Tải lên thành công.', true);
                        bsModal.hide();
                        setTimeout(function () {
                            window.location.reload();
                        }, 800);
                    }
                } catch (err) {
                    console.error(err);
                    showToast('Không thể kết nối máy chủ.', false);
                } finally {
                    if (submitBtn) {
                        submitBtn.disabled = false;
                        submitBtn.classList.remove('disabled');
                    }
                }
            });
        }

        if (uploadButton && filterActivitySelect) {
            filterActivitySelect.addEventListener('change', function () {
                uploadButton.setAttribute('data-activity-id', filterActivitySelect.value || '');
            });
        }

        if (uploadButton) {
            uploadButton.addEventListener('click', function () {
                let activityId = uploadButton.getAttribute('data-activity-id');
                if (filterActivitySelect) {
                    activityId = filterActivitySelect.value || activityId;
                }
                openUploadModal(activityId);
            });
        }

        page.addEventListener('click', async function (evt) {
            const target = evt.target.closest('.btn-delete-proof');
            if (!target) {
                return;
            }
            evt.preventDefault();
            const deleteUrl = target.getAttribute('data-delete-url');
            if (!deleteUrl) {
                return;
            }
            const confirmDelete = window.confirm('Bạn chắc chắn muốn xóa minh chứng này?');
            if (!confirmDelete) {
                return;
            }

            target.disabled = true;
            target.classList.add('disabled');

            try {
                const response = await fetch(deleteUrl, {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': globalToken,
                        'X-CSRF-TOKEN': globalToken,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({})
                });
                const result = await response.json();
                if (!response.ok || !result.ok) {
                    const message = result && result.message ? result.message : 'Không thể xóa minh chứng.';
                    showToast(message, false);
                } else {
                    showToast(result.message || 'Đã xóa minh chứng.', true);
                    setTimeout(function () {
                        window.location.reload();
                    }, 500);
                }
            } catch (err) {
                console.error(err);
                showToast('Không thể kết nối máy chủ.', false);
            } finally {
                target.disabled = false;
                target.classList.remove('disabled');
            }
        });
    });
})();
