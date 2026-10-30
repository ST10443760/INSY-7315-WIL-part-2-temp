// Revenue page: sorts the spend-per-client table when a column heading is
// clicked (again to reverse), and filters it as the search box is typed in.
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
