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

// Auto-submits a filter form (e.g. the schedule's date picker) as soon as
// its value changes, so most people never need the fallback submit button.
// The button stays in the markup for no-JS and keyboard users who just
// press Enter instead.
(function () {
    "use strict";

    document.querySelectorAll("form[data-auto-submit]").forEach(function (form) {
        form.querySelectorAll("input, select").forEach(function (field) {
            field.addEventListener("change", function () {
                form.submit();
            });
        });
    });
})();

// Password show/hide toggle. Lives here (not thrive-admin.js) because the
// admin Login form renders through the public _Layout while signed out, so
// only this shared bundle is guaranteed to be loaded on it.
(function () {
    "use strict";

    document.querySelectorAll("[data-password-toggle]").forEach(function (toggle) {
        var input = document.getElementById(toggle.getAttribute("data-password-toggle"));
        var showIcon = toggle.querySelector("[data-icon-show]");
        var hideIcon = toggle.querySelector("[data-icon-hide]");
        if (!input) {
            return;
        }

        toggle.addEventListener("click", function () {
            var isHidden = input.type === "password";
            input.type = isHidden ? "text" : "password";
            toggle.setAttribute("aria-label", isHidden ? "Hide password" : "Show password");
            if (showIcon && hideIcon) {
                showIcon.hidden = isHidden;
                hideIcon.hidden = !isHidden;
            }
        });
    });
})();
