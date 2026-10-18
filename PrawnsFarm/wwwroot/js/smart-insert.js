/* =====================================================================
   Smart Insert - add Expense/Sale by voice or QR.
   The browser only PRE-FILLS a draft; the server does the authoritative
   validation and only then creates a record.
   ===================================================================== */
(function () {
    var cfg = window.SmartInsertConfig || {};
    var draft = null;

    // ---------- number words -> digits (incl. Indian lakh/crore) ----------
    function normalizeNumbers(str) {
        str = str.toLowerCase();
        // digit + scale (e.g. "2 lakh", "5k")
        str = str.replace(/(\d+(?:\.\d+)?)\s*k\b/g, function (_, n) { return String(+n * 1000); });
        str = str.replace(/(\d+(?:\.\d+)?)\s*(lakhs?|lac)\b/g, function (_, n) { return String(+n * 100000); });
        str = str.replace(/(\d+(?:\.\d+)?)\s*(crores?)\b/g, function (_, n) { return String(+n * 10000000); });

        var units = { one: 1, two: 2, three: 3, four: 4, five: 5, six: 6, seven: 7, eight: 8, nine: 9, ten: 10, eleven: 11, twelve: 12, thirteen: 13, fourteen: 14, fifteen: 15, sixteen: 16, seventeen: 17, eighteen: 18, nineteen: 19 };
        var tens = { twenty: 20, thirty: 30, forty: 40, fifty: 50, sixty: 60, seventy: 70, eighty: 80, ninety: 90 };
        var scales = { hundred: 100, thousand: 1000, lakh: 100000, lakhs: 100000, crore: 10000000, crores: 10000000, million: 1000000 };

        var tokens = str.split(/\s+/);
        var out = [], current = 0, running = 0, inNum = false;
        function flush() { if (inNum) { out.push(String(running + current)); running = 0; current = 0; inNum = false; } }
        tokens.forEach(function (raw) {
            var w = raw.replace(/[^a-z0-9.]/g, '');
            if (w in units) { current += units[w]; inNum = true; }
            else if (w in tens) { current += tens[w]; inNum = true; }
            else if (w === 'hundred') { current = (current || 1) * 100; inNum = true; }
            else if (w in scales) { running += (current || 1) * scales[w]; current = 0; inNum = true; }
            else if (/^\d+(\.\d+)?$/.test(w)) { flush(); out.push(w); }
            else { flush(); out.push(raw); }
        });
        flush();
        return out.join(' ');
    }

    function canonMode(v) {
        if (!v) return null;
        var m = String(v).toLowerCase();
        if (/\bcash\b/.test(m)) return 'Cash';
        if (/\b(upi|gpay|google pay|phonepe|phone pe|paytm)\b/.test(m)) return 'UPI';
        if (/\b(card|credit card|debit card)\b/.test(m)) return 'Card';
        if (/\b(bank|neft|rtgs|imps|transfer)\b/.test(m)) return 'Bank Transfer';
        if (/\b(credit|due|udhaar|udhar)\b/.test(m)) return 'Credit';
        return null;
    }

    function matchCategory(text) {
        var t = ' ' + text.toLowerCase() + ' ';
        var cats = cfg.categories || [];
        for (var i = 0; i < cats.length; i++) {
            if (t.indexOf(cats[i].name.toLowerCase()) >= 0) return cats[i];
        }
        for (var j = 0; j < cats.length; j++) {
            var first = cats[j].name.toLowerCase().split(/[^a-z]+/)[0];
            if (first && first.length > 2 && new RegExp('\\b' + first + '\\b').test(t)) return cats[j];
        }
        return null;
    }

    function matchCategoryByName(name) {
        if (!name) return null;
        var n = name.toLowerCase().trim();
        var cats = cfg.categories || [];
        return cats.filter(function (c) { return c.name.toLowerCase() === n; })[0]
            || cats.filter(function (c) { return c.name.toLowerCase().indexOf(n) >= 0 || n.indexOf(c.name.toLowerCase()) >= 0; })[0]
            || null;
    }

    // ---------- natural language parser (voice) ----------
    function parse(text) {
        var original = text.trim();
        var t = ' ' + normalizeNumbers(original) + ' ';
        var d = { transcript: original };

        if (/\b(expense|spent|spend|paid|pay|bought|buy|purchase|purchased|bill|cost)\b/.test(t)) d.type = 'expense';
        if (/\b(sale|sold|sell|selling|income|revenue|harvest)\b/.test(t)) d.type = 'sale';

        if (/\byesterday\b/.test(t)) d.date = shiftDate(-1);
        else if (/\btoday\b/.test(t)) d.date = cfg.today;

        d.paymentMode = canonMode(t);

        var cat = matchCategory(t);
        if (cat) { d.categoryId = cat.id; d.category = cat.name; }

        // quantity (kg)
        var m = t.match(/(\d+(?:\.\d+)?)\s*(kg|kgs|kilo|kilos|kilogram|kilograms)\b/);
        if (m) d.quantityKg = parseFloat(m[1]);

        // rate per kg
        m = t.match(/(?:at|@|rate(?:\s+of)?)\s*(\d+(?:\.\d+)?)/);
        if (m) d.ratePerKg = parseFloat(m[1]);
        else { m = t.match(/(\d+(?:\.\d+)?)\s*(?:per\s*kg|\/\s*kg|a\s*kg|rupees\s*per\s*kg)/); if (m) d.ratePerKg = parseFloat(m[1]); }

        // explicit amount
        m = t.match(/(?:rs|rupees|inr|amount|total|₹)\s*(\d+(?:\.\d+)?)/) || t.match(/(\d+(?:\.\d+)?)\s*(?:rs|rupees|inr)\b/);
        if (m) d.amount = parseFloat(m[1]);

        // buyer (sales): "to <name>"
        m = t.match(/\bto\s+([a-z][a-z0-9 ]{1,28}?)(?:\s+(?:cash|upi|card|bank|credit|by|paid|rupees|rs|$))/);
        if (m && d.type === 'sale') d.buyer = tidy(m[1]);

        // fallback amount: largest leftover number
        if (d.amount == null) {
            var used = {};
            if (d.quantityKg != null) used[d.quantityKg] = 1;
            if (d.ratePerKg != null) used[d.ratePerKg] = 1;
            var nums = (t.match(/\d+(?:\.\d+)?/g) || []).map(Number).filter(function (n) { return !used[n]; });
            if (nums.length) d.amount = Math.max.apply(null, nums);
        }
        return d;
    }

    function fromObject(o) {
        var d = { transcript: typeof o === 'string' ? o : JSON.stringify(o) };
        d.type = (o.type || '').toLowerCase() || undefined;
        if (o.amount != null && o.amount !== '') d.amount = parseFloat(o.amount);
        var q = o.qty != null ? o.qty : o.quantity;
        if (q != null && q !== '') d.quantityKg = parseFloat(q);
        if (o.rate != null && o.rate !== '') d.ratePerKg = parseFloat(o.rate);
        var mode = o.mode || o.payment || o.paymentmode;
        if (mode) d.paymentMode = canonMode(mode) || mode;
        if (o.date) d.date = o.date;
        if (o.buyer) d.buyer = o.buyer;
        if (o.crop) d.crop = o.crop;
        if (o.desc || o.description) d.description = o.desc || o.description;
        if (o.category) { var c = matchCategoryByName(o.category); if (c) { d.categoryId = c.id; d.category = c.name; } else { d.category = o.category; } }
        return d;
    }

    function parsePayload(text) {
        text = (text || '').trim();
        if (text.charAt(0) === '{') { try { return fromObject(JSON.parse(text)); } catch (e) { /* fall through */ } }
        if (/^pf\|/i.test(text) || (text.indexOf('=') >= 0 && text.indexOf('|') >= 0)) {
            var o = {};
            text.replace(/^pf\|/i, '').split('|').forEach(function (p) {
                var i = p.indexOf('='); if (i > 0) o[p.slice(0, i).trim().toLowerCase()] = p.slice(i + 1).trim();
            });
            return fromObject(o);
        }
        return parse(text);
    }

    // ---------- client validation (mirrors server) ----------
    function validate(d) {
        var errs = [];
        if (d.type !== 'expense' && d.type !== 'sale') { errs.push('Say whether this is an EXPENSE or a SALE.'); return errs; }
        if (d.type === 'expense') {
            if (!(d.amount > 0)) errs.push('Amount is missing - say the expense amount.');
            if (!d.categoryId) errs.push('Category not recognised - say Feed, Labor, Power, etc.');
            if (!d.paymentMode) errs.push('Payment mode / source missing - say Cash, UPI, Card, Bank or Credit.');
        } else {
            var amt = d.amount > 0 ? d.amount : (d.quantityKg > 0 && d.ratePerKg > 0 ? d.quantityKg * d.ratePerKg : 0);
            if (!(amt > 0)) errs.push('Amount missing - say the total, or quantity in kg and rate per kg.');
            if (!d.paymentMode) errs.push('Payment mode / source missing - say Cash, UPI, Card, Bank or Credit.');
        }
        return errs;
    }

    // ---------- UI ----------
    var el = {};
    function $(id) { return document.getElementById(id); }

    function row(label, value, ok) {
        var cls = ok === false ? 'si-miss' : '';
        var val = (value == null || value === '') ? '<span class="si-miss">Not captured</span>' : value;
        return '<tr><th>' + label + '</th><td class="' + cls + '">' + val + '</td></tr>';
    }

    function renderReview(d) {
        draft = d;
        el.reviewEmpty.classList.add('d-none');
        el.reviewBox.classList.remove('d-none');
        el.resultBox.innerHTML = '';

        var isExp = d.type === 'expense';
        var isSale = d.type === 'sale';
        el.rvType.textContent = isExp ? 'EXPENSE' : (isSale ? 'SALE' : 'UNKNOWN TYPE');
        el.rvType.className = 'si-type-badge ' + (isExp ? 'is-exp' : (isSale ? 'is-sale' : 'is-unknown'));

        var amt = d.amount > 0 ? d.amount : (d.quantityKg > 0 && d.ratePerKg > 0 ? d.quantityKg * d.ratePerKg : null);

        var html = '';
        html += row('Farm', cfg.farmName, true);
        html += row('Type', isExp ? 'Expense' : (isSale ? 'Sale' : null), isExp || isSale);
        html += row('Amount', amt ? '₹ ' + Number(amt).toLocaleString('en-IN') : null, amt > 0);
        if (isExp) html += row('Category', d.category, !!d.categoryId);
        if (isSale) {
            html += row('Crop', d.crop || 'Vannamei Prawns', true);
            if (d.quantityKg) html += row('Quantity', d.quantityKg + ' kg', true);
            if (d.ratePerKg) html += row('Rate', '₹ ' + d.ratePerKg + '/kg', true);
            if (d.buyer) html += row('Buyer', d.buyer, true);
        }
        html += row('Payment', d.paymentMode, !!d.paymentMode);
        html += row('Date', d.date || cfg.today, true);
        el.rvRows.innerHTML = html;

        var errs = validate(d);
        if (errs.length) {
            el.rvErrors.classList.remove('d-none');
            el.rvErrors.innerHTML = '<div class="fw-semibold mb-1"><i class="bi bi-exclamation-triangle"></i> I still need:</div><ul class="mb-0">' +
                errs.map(function (e) { return '<li>' + e + '</li>'; }).join('') + '</ul>';
            el.confirmBtn.disabled = true;
        } else {
            el.rvErrors.classList.add('d-none');
            el.confirmBtn.disabled = false;
        }
    }

    function clearReview() {
        draft = null;
        el.reviewBox.classList.add('d-none');
        el.reviewEmpty.classList.remove('d-none');
        el.resultBox.innerHTML = '';
        if (el.transcript) el.transcript.textContent = '';
    }

    function save() {
        if (!draft) return;
        el.confirmBtn.disabled = true;
        el.confirmBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Saving...';

        fetch(cfg.saveUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': cfg.token },
            body: JSON.stringify(draft)
        }).then(function (r) { return r.json(); }).then(function (res) {
            el.confirmBtn.innerHTML = '<i class="bi bi-check-lg"></i> Confirm & Save';
            if (res.ok) {
                el.resultBox.innerHTML = '<div class="alert alert-success mb-0"><i class="bi bi-check-circle"></i> ' + res.message + '</div>';
                clearReview();
            } else {
                el.confirmBtn.disabled = false;
                el.rvErrors.classList.remove('d-none');
                el.rvErrors.innerHTML = '<div class="fw-semibold mb-1"><i class="bi bi-exclamation-triangle"></i> Not saved - I still need:</div><ul class="mb-0">' +
                    (res.errors || []).map(function (e) { return '<li>' + e + '</li>'; }).join('') + '</ul>';
            }
        }).catch(function () {
            el.confirmBtn.disabled = false;
            el.confirmBtn.innerHTML = '<i class="bi bi-check-lg"></i> Confirm & Save';
            el.resultBox.innerHTML = '<div class="alert alert-danger mb-0">Could not reach the server. Please try again.</div>';
        });
    }

    // ---------- Voice ----------
    function setupVoice() {
        var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
        var btn = $('micBtn'), status = $('micStatus');
        if (!SR) {
            if (status) status.textContent = 'Voice is not supported in this browser. Use Chrome or Edge, or use QR.';
            if (btn) btn.disabled = true;
            return;
        }
        var rec = new SR();
        rec.lang = 'en-IN';
        rec.interimResults = true;
        rec.maxAlternatives = 1;
        var listening = false, finalText = '';

        btn.addEventListener('click', function () {
            if (listening) { rec.stop(); return; }
            finalText = '';
            try { rec.start(); } catch (e) { /* already started */ }
        });

        rec.onstart = function () { listening = true; btn.classList.add('listening'); status.textContent = 'Listening... speak now'; };
        rec.onerror = function (e) { status.textContent = 'Mic error: ' + e.error + '. Check permission and try again.'; };
        rec.onend = function () { listening = false; btn.classList.remove('listening'); if (!finalText) status.textContent = 'Tap the mic and speak a short command'; };

        rec.onresult = function (ev) {
            var interim = '';
            for (var i = ev.resultIndex; i < ev.results.length; i++) {
                var tr = ev.results[i][0].transcript;
                if (ev.results[i].isFinal) finalText += tr; else interim += tr;
            }
            el.transcript.textContent = (finalText + ' ' + interim).trim();
            if (finalText) {
                status.textContent = 'Heard: "' + finalText.trim() + '"';
                renderReview(parse(finalText));
            }
        };
    }

    // ---------- QR ----------
    function setupQr() {
        var startBtn = $('qrStart'), stopBtn = $('qrStop');
        if (!startBtn || typeof Html5Qrcode === 'undefined') { if (startBtn) startBtn.disabled = true; return; }
        var scanner = new Html5Qrcode('qrReader');
        var running = false;

        startBtn.addEventListener('click', function () {
            if (running) return;
            scanner.start({ facingMode: 'environment' }, { fps: 10, qrbox: 230 },
                function (decoded) {
                    renderReview(parsePayload(decoded));
                    stop();
                },
                function () { /* ignore per-frame scan failures */ }
            ).then(function () { running = true; startBtn.classList.add('d-none'); stopBtn.classList.remove('d-none'); })
             .catch(function (err) { $('resultBox').innerHTML = '<div class="alert alert-warning mb-0">Camera unavailable: ' + err + '</div>'; });
        });

        function stop() {
            if (!running) return;
            scanner.stop().then(function () { scanner.clear(); }).catch(function () {});
            running = false; startBtn.classList.remove('d-none'); stopBtn.classList.add('d-none');
        }
        stopBtn.addEventListener('click', stop);
    }

    // ---------- helpers ----------
    function tidy(s) { return s.replace(/\s+/g, ' ').trim().replace(/\b\w/g, function (c) { return c.toUpperCase(); }); }
    function shiftDate(days) { var d = new Date(); d.setDate(d.getDate() + days); return d.toISOString().slice(0, 10); }

    document.addEventListener('DOMContentLoaded', function () {
        el.transcript = $('transcript');
        el.reviewEmpty = $('reviewEmpty');
        el.reviewBox = $('reviewBox');
        el.rvType = $('rvType');
        el.rvRows = $('rvRows');
        el.rvErrors = $('rvErrors');
        el.confirmBtn = $('confirmBtn');
        el.clearBtn = $('clearBtn');
        el.resultBox = $('resultBox');

        if (el.confirmBtn) el.confirmBtn.addEventListener('click', save);
        if (el.clearBtn) el.clearBtn.addEventListener('click', clearReview);

        setupVoice();
        setupQr();
    });
})();
