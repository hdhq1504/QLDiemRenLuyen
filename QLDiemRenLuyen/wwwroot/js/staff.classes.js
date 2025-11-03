(function () {
    const form = document.getElementById('addStudentForm');
    if (!form) {
        return;
    }

    const submitBtn = form.querySelector('button[type="submit"]');

    form.addEventListener('submit', () => {
        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Đang lưu...';
        }
    });

    form.addEventListener('reset', () => {
        if (submitBtn) {
            submitBtn.disabled = false;
            submitBtn.innerHTML = '<i class="fa-solid fa-floppy-disk me-1"></i>Lưu';
        }
    });
})();
