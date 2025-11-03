(function () {
    const trendCanvas = document.getElementById('registrationsTrendChart');
    const topActivitiesCanvas = document.getElementById('topActivitiesChart');
    const termSelect = document.getElementById('topActivitiesTerm');
    const recentAuditContainer = document.getElementById('recentAuditContainer');
    const pendingFeedbackContainer = document.getElementById('pendingFeedbackContainer');

    let trendChart;
    let topActivitiesChart;

    const showToast = (message, type = 'error') => {
        if (typeof Toastify === 'undefined') {
            return;
        }
        Toastify({
            text: message,
            duration: 3000,
            gravity: 'top',
            position: 'right',
            backgroundColor: type === 'success' ? '#198754' : '#dc3545'
        }).showToast();
    };

    const loadTrend = async () => {
        if (!trendCanvas) {
            return;
        }

        try {
            const resp = await axios.get('/admin/dashboard/registrations-trend', { params: { days: 14 } });
            const labels = resp.data.map(x => new Date(x.day).toLocaleDateString('vi-VN'));
            const values = resp.data.map(x => x.count);

            if (trendChart) {
                trendChart.destroy();
            }

            trendChart = new Chart(trendCanvas, {
                type: 'line',
                data: {
                    labels,
                    datasets: [{
                        label: 'Số đăng ký',
                        data: values,
                        tension: 0.3,
                        fill: false,
                        borderColor: '#0d6efd',
                        backgroundColor: '#0d6efd'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false
                }
            });
        }
        catch (error) {
            console.error(error);
            showToast('Không thể tải dữ liệu đăng ký theo ngày');
        }
    };

    const loadTopActivities = async () => {
        if (!topActivitiesCanvas) {
            return;
        }

        try {
            const termId = termSelect?.value;
            const params = { top: 5 };
            if (termId) {
                params.termId = termId;
            }
            const resp = await axios.get('/admin/dashboard/top-activities', { params });
            const labels = resp.data.map(x => x.title);
            const values = resp.data.map(x => x.count);

            if (topActivitiesChart) {
                topActivitiesChart.destroy();
            }

            topActivitiesChart = new Chart(topActivitiesCanvas, {
                type: 'bar',
                data: {
                    labels,
                    datasets: [{
                        label: 'Số đăng ký',
                        data: values,
                        backgroundColor: '#6610f2'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false
                }
            });
        }
        catch (error) {
            console.error(error);
            showToast('Không thể tải top hoạt động');
        }
    };

    const refreshPartial = async (container) => {
        if (!container) return;
        const url = container.getAttribute('data-url');
        if (!url) return;
        try {
            const resp = await axios.get(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            container.innerHTML = resp.data;
        }
        catch (error) {
            console.error(error);
            showToast('Không thể tải dữ liệu mới');
        }
    };

    document.addEventListener('DOMContentLoaded', () => {
        if (termSelect) {
            const defaultTerm = termSelect.getAttribute('data-current-term');
            if (defaultTerm) {
                termSelect.value = defaultTerm;
            }
            termSelect.addEventListener('change', loadTopActivities);
        }

        document.getElementById('btnRefreshAudit')?.addEventListener('click', () => refreshPartial(recentAuditContainer));
        document.getElementById('btnRefreshFeedback')?.addEventListener('click', () => refreshPartial(pendingFeedbackContainer));

        loadTrend();
        loadTopActivities();
        refreshPartial(recentAuditContainer);
        refreshPartial(pendingFeedbackContainer);
    });
})();
