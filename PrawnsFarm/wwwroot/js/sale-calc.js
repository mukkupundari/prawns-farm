(function () {
    var qty = document.getElementById('qty');
    var rate = document.getElementById('rate');
    var amount = document.getElementById('amount');
    if (!qty || !rate || !amount) return;

    function calc() {
        var q = parseFloat(qty.value) || 0;
        var r = parseFloat(rate.value) || 0;
        if (q > 0 && r > 0) {
            amount.value = (q * r).toFixed(2);
        }
    }
    qty.addEventListener('input', calc);
    rate.addEventListener('input', calc);
})();
