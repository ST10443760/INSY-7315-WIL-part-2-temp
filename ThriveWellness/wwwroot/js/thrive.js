// Generic mobile menu controller: a hamburger toggle (icon swaps to an X
// while open) plus four ways to close - tapping the toggle again, an
// explicit close button inside the panel, tapping a backdrop outside it,
// or Escape - and auto-close if the viewport grows past the breakpoint
// while open. Drives both the public nav menu and the admin sidebar
// drawer, which are functionally identical, just different selectors and
// breakpoints. Lives here, not thrive-admin.js, and is loaded on both
// layouts - an earlier bug (the Login page's password toggle) came from a
// handler that only loaded in the admin-only script but was needed on a
// page using the public layout.
(function () {
    "use strict";

    function createMenuController(config) {
        var toggle = document.querySelector(config.toggleSelector);
        var panel = document.querySelector(config.panelSelector);
        var backdrop = document.querySelector(config.backdropSelector);
        var closeButton = document.querySelector(config.closeButtonSelector);
        if (!toggle || !panel) {
            return;
        }

        var iconClosed = toggle.querySelector("[data-icon-closed]");
        var iconOpen = toggle.querySelector("[data-icon-open]");

        function isOpen() {
            return panel.hasAttribute("data-open");
        }

        function setOpen(open) {
            if (open) {
                panel.setAttribute("data-open", "");
            } else {
                panel.removeAttribute("data-open");
            }
            // The admin drawer hides off-screen with a transform rather
            // than display:none, so its links/buttons stay focusable
            // while "closed" unless marked inert - harmless to set on the
            // public menu too, which is already display:none when closed.
            panel.inert = !open;
            toggle.setAttribute("aria-expanded", String(open));
            toggle.setAttribute("aria-label", open ? "Close menu" : "Open menu");
            if (iconClosed && iconOpen) {
                iconClosed.hidden = open;
                iconOpen.hidden = !open;
            }
            if (backdrop) {
                backdrop.hidden = !open;
            }
            document.body.classList.toggle("thrive-menu-open", open);
        }

        function close(returnFocus) {
            if (!isOpen()) {
                return;
            }
            setOpen(false);
            if (returnFocus) {
                toggle.focus();
            }
        }

        toggle.addEventListener("click", function () {
            setOpen(!isOpen());
        });

        if (closeButton) {
            closeButton.addEventListener("click", function () {
                close(true);
            });
        }

        if (backdrop) {
            backdrop.addEventListener("click", function () {
                close(true);
            });
        }

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                close(true);
            }
        });

        window.addEventListener("resize", function () {
            if (isOpen() && window.innerWidth > config.breakpoint) {
                // The viewport grew past the breakpoint - close without
                // moving focus, since the toggle itself is about to be
                // hidden by the desktop layout anyway.
                setOpen(false);
            }
        });
    }

    createMenuController({
        toggleSelector: ".thrive-nav-toggle",
        panelSelector: "#thrive-nav-list",
        backdropSelector: "#thrive-nav-backdrop",
        closeButtonSelector: "#thrive-nav-close",
        breakpoint: 768
    });

    createMenuController({
        toggleSelector: ".thrive-admin-menu-toggle",
        panelSelector: "#adminSidebar",
        backdropSelector: "#adminSidebarBackdrop",
        closeButtonSelector: "#adminSidebarClose",
        breakpoint: 900
    });
})();

// The signed-in profile menu in the public header (wide screens only - the
// mobile panel shows the same links as a plain section instead). A button
// with aria-haspopup/aria-expanded/aria-controls that opens a small menu:
// click, Enter or Space toggles it (opening from the keyboard moves focus
// to the first item); Arrow Up/Down move between items; Escape closes it
// and returns focus to the button; clicking outside it or tabbing away
// closes it. Separate from the mobile menu controller above, and it stops
// its own Escape from reaching that controller's handler.
(function () {
    "use strict";

    document.querySelectorAll("[data-profile-menu]").forEach(function (root) {
        var trigger = root.querySelector("[aria-haspopup]");
        var menu = trigger ? document.getElementById(trigger.getAttribute("aria-controls")) : null;
        if (!trigger || !menu) {
            return;
        }

        function items() {
            return Array.prototype.slice.call(menu.querySelectorAll("[role=menuitem]"));
        }

        function isOpen() {
            return !menu.hidden;
        }

        // Keeps the panel on screen: it's right-aligned to the button by
        // CSS; if a narrow window still pushes it past the left edge, it's
        // nudged back in.
        function keepOnScreen() {
            menu.style.right = "";
            var rect = menu.getBoundingClientRect();
            if (rect.left < 8) {
                menu.style.right = (rect.left - 8) + "px";
            }
        }

        function open(focusIndex) {
            menu.hidden = false;
            trigger.setAttribute("aria-expanded", "true");
            keepOnScreen();
            if (focusIndex !== undefined) {
                var list = items();
                var index = focusIndex < 0 ? list.length - 1 : focusIndex;
                if (list[index]) {
                    list[index].focus();
                }
            }
        }

        function close(returnFocus) {
            if (!isOpen()) {
                return;
            }
            menu.hidden = true;
            trigger.setAttribute("aria-expanded", "false");
            if (returnFocus) {
                trigger.focus();
            }
        }

        trigger.addEventListener("click", function (event) {
            if (isOpen()) {
                close(false);
            } else {
                // detail is 0 when the click came from Enter or Space.
                open(event.detail === 0 ? 0 : undefined);
            }
        });

        trigger.addEventListener("keydown", function (event) {
            if (event.key === "ArrowDown") {
                event.preventDefault();
                open(0);
            } else if (event.key === "ArrowUp") {
                event.preventDefault();
                open(-1);
            }
        });

        menu.addEventListener("keydown", function (event) {
            var list = items();
            var current = list.indexOf(document.activeElement);
            if (event.key === "ArrowDown") {
                event.preventDefault();
                list[(current + 1) % list.length].focus();
            } else if (event.key === "ArrowUp") {
                event.preventDefault();
                list[(current - 1 + list.length) % list.length].focus();
            } else if (event.key === "Home") {
                event.preventDefault();
                list[0].focus();
            } else if (event.key === "End") {
                event.preventDefault();
                list[list.length - 1].focus();
            }
        });

        root.addEventListener("keydown", function (event) {
            if (event.key === "Escape" && isOpen()) {
                event.stopPropagation();
                close(true);
            }
        });

        document.addEventListener("click", function (event) {
            if (isOpen() && !root.contains(event.target)) {
                close(false);
            }
        });

        // Tabbing (or otherwise moving focus) out of the menu closes it.
        // relatedTarget isn't always filled in, so where focus actually
        // landed is checked once it has moved. Focus dropping to the page
        // itself (a click on blank space) is left to the click handler.
        root.addEventListener("focusout", function (event) {
            if (event.relatedTarget) {
                if (isOpen() && !root.contains(event.relatedTarget)) {
                    close(false);
                }
                return;
            }
            window.setTimeout(function () {
                var active = document.activeElement;
                if (isOpen() && active && active !== document.body && !root.contains(active)) {
                    close(false);
                }
            }, 0);
        });

        window.addEventListener("resize", function () {
            if (isOpen()) {
                keepOnScreen();
            }
        });
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
