// Mobile nav toggle for the public header. Plain JS, no framework:
// the brief asks for no icon font / JS icon library and to keep the
// public bundle light.
(function () {
    "use strict";

    var toggle = document.querySelector(".thrive-nav-toggle");
    var list = document.getElementById("thrive-nav-list");
    if (!toggle || !list) {
        return;
    }

    toggle.addEventListener("click", function () {
        var isOpen = list.hasAttribute("data-open");
        if (isOpen) {
            list.removeAttribute("data-open");
        } else {
            list.setAttribute("data-open", "");
        }
        toggle.setAttribute("aria-expanded", String(!isOpen));
    });
})();

// Disables a submit button and swaps its label while a form is submitting,
// so a slow request (e.g. a real SendGrid call) can't be double-submitted.
(function () {
    "use strict";

    document.querySelectorAll("form[data-busy-text]").forEach(function (form) {
        form.addEventListener("submit", function () {
            var button = form.querySelector("button[type=submit]");
            if (!button || button.disabled) {
                return;
            }
            button.dataset.originalText = button.textContent;
            button.textContent = form.dataset.busyText || "Please wait…";
            button.disabled = true;
        });
    });
})();
