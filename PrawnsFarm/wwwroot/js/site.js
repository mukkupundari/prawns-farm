window.PrawnsFarm = (function () {
    var inr = new Intl.NumberFormat('en-IN');
    function money(v) { return '\u20B9 ' + inr.format(Math.round(v)); }

    var doughnutColors = ['#16a34a', '#a855f7', '#3b82f6', '#f59e0b', '#ef4444', '#06b6d4', '#14b8a6', '#64748b'];

    var pieLegend = {
        position: 'bottom',
        labels: { boxWidth: 12, padding: 10, font: { size: 11 } }
    };

    function renderDashboard(trend, category, payment) {
        // Expenses vs Sales bar chart
        var trendEl = document.getElementById('trendChart');
        if (trendEl && trend.labels.length) {
            new Chart(trendEl, {
                type: 'bar',
                data: {
                    labels: trend.labels,
                    datasets: [
                        { label: 'Expenses', data: trend.expenses, backgroundColor: '#ef4444', borderRadius: 5, maxBarThickness: 34 },
                        { label: 'Sales', data: trend.sales, backgroundColor: '#16a34a', borderRadius: 5, maxBarThickness: 34 }
                    ]
                },
                options: {
                    responsive: true, maintainAspectRatio: false,
                    plugins: {
                        legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 11 } } },
                        tooltip: { callbacks: { label: function (c) { return c.dataset.label + ': ' + money(c.raw); } } }
                    },
                    scales: { y: { beginAtZero: true, ticks: { callback: function (v) { return money(v); }, font: { size: 10 } } }, x: { ticks: { font: { size: 10 } } } }
                }
            });
        }

        // Category doughnut
        var catEl = document.getElementById('categoryChart');
        if (catEl && category.labels.length) {
            new Chart(catEl, {
                type: 'doughnut',
                data: { labels: category.labels, datasets: [{ data: category.values, backgroundColor: category.colors, borderWidth: 2, borderColor: '#fff' }] },
                options: { responsive: true, maintainAspectRatio: false, cutout: '58%', plugins: { legend: pieLegend, tooltip: { callbacks: { label: function (c) { return c.label + ': ' + money(c.raw); } } } } }
            });
        }

        // Payment doughnut
        var payEl = document.getElementById('paymentChart');
        if (payEl && payment.labels.length) {
            new Chart(payEl, {
                type: 'doughnut',
                data: { labels: payment.labels, datasets: [{ data: payment.values, backgroundColor: doughnutColors, borderWidth: 2, borderColor: '#fff' }] },
                options: { responsive: true, maintainAspectRatio: false, cutout: '58%', plugins: { legend: pieLegend, tooltip: { callbacks: { label: function (c) { return c.label + ': ' + money(c.raw); } } } } }
            });
        }
    }

    // ---- Count-up animation for KPI numbers (runs once after login) ----
    function countUp() {
        var els = document.querySelectorAll('.kpi-value[data-countup]');
        els.forEach(function (el) {
            var target = parseFloat(el.getAttribute('data-countup')) || 0;
            var isMoney = el.getAttribute('data-money') === '1';
            var dur = 1300;
            var start = null;

            function fmt(v) { return isMoney ? money(v) : inr.format(Math.round(v)); }

            function step(ts) {
                if (start === null) start = ts;
                var p = Math.min((ts - start) / dur, 1);
                // easeOutCubic for a smooth settle
                var eased = 1 - Math.pow(1 - p, 3);
                el.textContent = fmt(target * eased);
                if (p < 1) requestAnimationFrame(step);
                else el.textContent = fmt(target);
            }
            el.textContent = fmt(0);
            requestAnimationFrame(step);
        });
    }

    // ---- Optional 3D chart (Three.js, lazy-loaded) ----
    function loadScript(src) {
        return new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = src; s.onload = resolve; s.onerror = reject;
            document.head.appendChild(s);
        });
    }

    function setup3D(trend) {
        var btn = document.getElementById('toggle3d');
        var stage = document.getElementById('threeDStage');
        if (!btn || !stage) return;
        var started = false;

        btn.addEventListener('click', function () {
            if (started) return;
            started = true;
            btn.disabled = true;
            btn.querySelector('span').textContent = 'Loading 3D...';

            loadScript('https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.min.js')
                .then(function () {
                    stage.innerHTML = '';
                    render3DBars(stage, trend);
                    btn.querySelector('span').textContent = '3D Enabled';
                })
                .catch(function () {
                    started = false; btn.disabled = false;
                    btn.querySelector('span').textContent = 'Enable 3D';
                    stage.innerHTML = '<div class="threeD-hint"><i class="bi bi-wifi-off"></i><p>Could not load the 3D engine. Check your internet connection and try again.</p></div>';
                });
        });
    }

    function render3DBars(container, trend) {
        var THREE = window.THREE;
        var w = container.clientWidth, h = container.clientHeight || 340;

        var scene = new THREE.Scene();
        scene.fog = new THREE.Fog(0x0b3b2e, 18, 46);

        var camera = new THREE.PerspectiveCamera(55, w / h, 0.1, 100);
        camera.position.set(0, 12, 22);

        var renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
        renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        renderer.setSize(w, h);
        container.appendChild(renderer.domElement);

        scene.add(new THREE.AmbientLight(0xffffff, 0.75));
        var dir = new THREE.DirectionalLight(0xffffff, 0.9);
        dir.position.set(10, 20, 12);
        scene.add(dir);

        var group = new THREE.Group();
        scene.add(group);

        var labels = trend.labels || [];
        var exp = trend.expenses || [];
        var sal = trend.sales || [];
        var n = Math.max(labels.length, 1);
        var maxV = 1;
        exp.concat(sal).forEach(function (v) { if (v > maxV) maxV = v; });

        var spacing = 3.2;
        var startX = -((n - 1) * spacing) / 2;

        // ground
        var ground = new THREE.Mesh(
            new THREE.PlaneGeometry(n * spacing + 8, 14),
            new THREE.MeshStandardMaterial({ color: 0x0e4a39, roughness: 1 })
        );
        ground.rotation.x = -Math.PI / 2;
        ground.position.y = 0;
        group.add(ground);

        function bar(x, z, value, color) {
            var hgt = Math.max((value / maxV) * 10, 0.05);
            var geo = new THREE.BoxGeometry(1.1, hgt, 1.1);
            var mat = new THREE.MeshStandardMaterial({ color: color, metalness: 0.2, roughness: 0.45 });
            var mesh = new THREE.Mesh(geo, mat);
            mesh.position.set(x, hgt / 2, z);
            group.add(mesh);
        }

        for (var i = 0; i < n; i++) {
            var x = startX + i * spacing;
            bar(x - 0.65, -1.2, exp[i] || 0, 0xef4444); // expenses (red)
            bar(x + 0.65, 1.2, sal[i] || 0, 0x22c55e);  // sales (green)
        }

        // Simple drag-to-rotate + gentle auto-spin
        var autoRotate = true, dragging = false, lastX = 0, targetRot = 0.6;
        group.rotation.y = targetRot;

        renderer.domElement.style.cursor = 'grab';
        renderer.domElement.addEventListener('pointerdown', function (e) { dragging = true; autoRotate = false; lastX = e.clientX; renderer.domElement.style.cursor = 'grabbing'; });
        window.addEventListener('pointerup', function () { dragging = false; renderer.domElement.style.cursor = 'grab'; });
        window.addEventListener('pointermove', function (e) { if (dragging) { targetRot += (e.clientX - lastX) * 0.01; lastX = e.clientX; } });

        function resize() {
            var nw = container.clientWidth, nh = container.clientHeight || 340;
            camera.aspect = nw / nh; camera.updateProjectionMatrix(); renderer.setSize(nw, nh);
        }
        window.addEventListener('resize', resize);

        (function animate() {
            requestAnimationFrame(animate);
            if (autoRotate) targetRot += 0.0035;
            group.rotation.y += (targetRot - group.rotation.y) * 0.1;
            camera.lookAt(0, 4, 0);
            renderer.render(scene, camera);
        })();
    }

    return { renderDashboard: renderDashboard, money: money, countUp: countUp, setup3D: setup3D };
})();

document.addEventListener('DOMContentLoaded', function () {
    // Mobile sidebar toggle
    var toggle = document.getElementById('menuToggle');
    var sidebar = document.querySelector('.sidebar');
    if (toggle && sidebar) {
        toggle.addEventListener('click', function (e) {
            e.stopPropagation();
            sidebar.classList.toggle('open');
        });
    }

    // Floating quick-add button
    var fab = document.getElementById('fabWrap');
    var fabBtn = document.getElementById('fabToggle');
    if (fab && fabBtn) {
        fabBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            fab.classList.toggle('open');
        });
    }

    // Close popovers when clicking elsewhere
    document.addEventListener('click', function () {
        if (fab) fab.classList.remove('open');
        if (sidebar && window.innerWidth <= 992) sidebar.classList.remove('open');
    });
});
