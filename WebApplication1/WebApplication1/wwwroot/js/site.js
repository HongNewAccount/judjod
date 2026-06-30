document.addEventListener('DOMContentLoaded', function() {
    // Sidebar collapse / mobile drawer
    const shell = document.querySelector('[data-sidebar]');
    const toggleBtn = document.querySelector('[data-sidebar-toggle]');
    const overlay = document.querySelector('[data-sidebar-overlay]');
    const isMobile = () => window.matchMedia('(max-width: 768px)').matches;

    if (shell && localStorage.getItem('sidebarCollapsed') === '1' && !isMobile()) {
        shell.classList.add('collapsed');
    }

    if (toggleBtn && shell) {
        toggleBtn.addEventListener('click', function () {
            if (isMobile()) {
                const isOpen = shell.classList.toggle('mobile-open');
                document.body.style.overflow = isOpen ? 'hidden' : '';
            } else {
                const isCollapsed = shell.classList.toggle('collapsed');
                localStorage.setItem('sidebarCollapsed', isCollapsed ? '1' : '0');
            }
        });
    }

    if (overlay && shell) {
        overlay.addEventListener('click', function () {
            shell.classList.remove('mobile-open');
            document.body.style.overflow = '';
        });
    }

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && shell && shell.classList.contains('mobile-open')) {
            shell.classList.remove('mobile-open');
            document.body.style.overflow = '';
        }
    });

    window.addEventListener('resize', function () {
        if (!isMobile() && shell) {
            shell.classList.remove('mobile-open');
            document.body.style.overflow = '';
        }
    });

    // Bootstrap form validation
    const forms = document.querySelectorAll('.needs-validation');
    Array.from(forms).forEach(form => {
        form.addEventListener('submit', event => {
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
            }
            form.classList.add('was-validated');
        }, false);
    });

    // Auto-hide alerts after 5 seconds
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        if (!alert.classList.contains('alert-permanent')) {
            setTimeout(() => {
                const bsAlert = new bootstrap.Alert(alert);
                bsAlert.close();
            }, 5000);
        }
    });

    // Initialize tooltips if Bootstrap tooltips are available
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl));
});
