// Admin sidebar drawer toggle (<=900px). Plain JS, same data-open attribute
// pattern as the public header's hamburger (see thrive.js).
(function () {
    "use strict";

    var toggle = document.querySelector(".thrive-admin-menu-toggle");
    var sidebar = document.getElementById("adminSidebar");
    if (!toggle || !sidebar) {
        return;
    }

    toggle.addEventListener("click", function () {
        var isOpen = sidebar.hasAttribute("data-open");
        if (isOpen) {
            sidebar.removeAttribute("data-open");
        } else {
            sidebar.setAttribute("data-open", "");
        }
        toggle.setAttribute("aria-expanded", String(!isOpen));
    });
})();

// Password show/hide toggle on the admin login form.
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
