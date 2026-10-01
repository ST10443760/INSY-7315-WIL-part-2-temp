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
