(function () {
    const classList = document.getElementById('lecturerClassList');
    const termSelect = document.getElementById('lecturerTermSelect');
    const scoresWrapper = document.getElementById('lecturerScoresWrapper');
    const scoresTableBody = document.querySelector('#lecturerScoresTable tbody');
    const hint = document.getElementById('lecturerDashboardHint');
    let selectedClassId = null;

    const renderScores = (data) => {
        scoresTableBody.innerHTML = '';
        if (!data || data.length === 0) {
            scoresTableBody.innerHTML = '<tr><td colspan="4" class="text-center text-muted">Chưa có dữ liệu điểm.</td></tr>';
            return;
        }

        for (const item of data) {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${item.studentId}</td>
                <td>${item.fullName}</td>
                <td>${item.total.toFixed(2)}</td>
                <td><span class="badge ${item.status === 'APPROVED' ? 'bg-success' : 'bg-warning text-dark'}">${item.status ?? 'PROVISIONAL'}</span></td>`;
            scoresTableBody.appendChild(row);
        }
    };

    const loadScores = async () => {
        if (!selectedClassId || !termSelect?.value) {
            return;
        }

        try {
            const resp = await axios.get(`/lecturer/classes/${encodeURIComponent(selectedClassId)}/scores`, {
                params: { termId: termSelect.value }
            });
            renderScores(resp.data);
            hint?.classList.add('d-none');
            scoresWrapper?.classList.remove('d-none');
        }
        catch (error) {
            console.error(error);
            alert('Không thể tải điểm của lớp.');
        }
    };

    const handleClassClick = (event) => {
        const button = event.target.closest('[data-class-id]');
        if (!button) return;
        selectedClassId = button.getAttribute('data-class-id');

        // Bỏ active các button khác
        classList?.querySelectorAll('.list-group-item-action').forEach(el => el.classList.remove('active'));
        button.classList.add('active');
        loadScores();
    };

    document.addEventListener('DOMContentLoaded', () => {
        classList?.addEventListener('click', handleClassClick);
        termSelect?.addEventListener('change', loadScores);

        // Nếu đã có lớp mặc định trong danh sách thì trigger click đầu tiên
        const firstClassBtn = classList?.querySelector('[data-class-id]');
        if (firstClassBtn) {
            firstClassBtn.click();
        }
    });
})();