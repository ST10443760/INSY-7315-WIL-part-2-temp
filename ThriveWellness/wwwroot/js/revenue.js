// Revenue page: tooltips on the net revenue graph's points, and the
// spend-per-client table - sorted when a column heading is clicked (again
// to reverse), filtered as the search box is typed in.
// The table arrives already sorted by total spent, so without JavaScript it
// still reads correctly.
(function () {
    "use strict";

    document.querySelectorAll("table[data-sortable-table]").forEach(function (table) {
        var body = table.tBodies[0];
        var headers = Array.prototype.slice.call(table.tHead.rows[0].cells);

        headers.forEach(function (th, column) {
            var button = th.querySelector("button");
            if (!button) {
                return;
            }
            button.addEventListener("click", function () {
                var numeric = button.getAttribute("data-sort-type") === "number";
                var ascending = th.getAttribute("aria-sort") !== "ascending";

                var rows = Array.prototype.slice.call(body.rows);
                rows.sort(function (a, b) {
                    var x = a.cells[column].getAttribute("data-sort-value");
                    var y = b.cells[column].getAttribute("data-sort-value");
                    var result = numeric ? parseFloat(x) - parseFloat(y) : x.localeCompare(y);
                    return ascending ? result : -result;
                });
                rows.forEach(function (row) {
                    body.appendChild(row);
                });

                headers.forEach(function (other) {
                    other.setAttribute("aria-sort", "none");
                });
                th.setAttribute("aria-sort", ascending ? "ascending" : "descending");
            });
        });
    });

    // Revenue graph: hovering or focusing (Tab) a point shows the period and
    // its net amount in a tooltip above it; leaving it, or Escape, hides it.
    document.querySelectorAll("[data-chart]").forEach(function (chart) {
        var tooltip = chart.querySelector("[data-chart-tooltip]");
        if (!tooltip) {
            return;
        }

        function show(point) {
            tooltip.textContent = "";
            var period = document.createElement("strong");
            period.textContent = point.getAttribute("data-period");
            tooltip.appendChild(period);
            tooltip.appendChild(document.createTextNode(point.getAttribute("data-amount")));
            tooltip.hidden = false;

            var box = chart.getBoundingClientRect();
            var dot = point.getBoundingClientRect();
            var x = dot.left + dot.width / 2 - box.left;
            // Keep the tooltip inside the card at either end of the line.
            var half = tooltip.offsetWidth / 2;
            x = Math.min(Math.max(x, half + 4), box.width - half - 4);
            tooltip.style.left = x + "px";
            tooltip.style.top = (dot.top - box.top) + "px";
        }

        function hide() {
            tooltip.hidden = true;
        }

        chart.querySelectorAll("[data-chart-point]").forEach(function (point) {
            point.addEventListener("mouseenter", function () { show(point); });
            point.addEventListener("focus", function () { show(point); });
            point.addEventListener("mouseleave", hide);
            point.addEventListener("blur", hide);
            point.addEventListener("keydown", function (event) {
                if (event.key === "Escape") {
                    hide();
                }
            });
        });
    });

    document.querySelectorAll("input[data-table-search]").forEach(function (input) {
        var table = document.getElementById(input.getAttribute("data-table-search"));
        var noMatch = document.getElementById(table.id.replace("Table", "NoMatch"));
        input.addEventListener("input", function () {
            var term = input.value.trim().toLowerCase();
            var shown = 0;
            Array.prototype.forEach.call(table.tBodies[0].rows, function (row) {
                var match = row.getAttribute("data-search-text").indexOf(term) !== -1;
                row.hidden = !match;
                if (match) {
                    shown++;
                }
            });
            if (noMatch) {
                noMatch.hidden = shown !== 0;
            }
        });
    });
})();
