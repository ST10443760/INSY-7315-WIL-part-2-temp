// Card checkout: spaces the card number as it's typed and names the brand,
// adds the slash to MM/YY, keeps the CVC to digits, fills a test card from
// the helper list, and shows a processing state that blocks a second click.
// Formatting only - the server checks everything again, and nothing typed
// here is ever stored in the browser.
(function () {
    "use strict";

    var form = document.querySelector("form[data-checkout-form]");
    if (form) {
        var numberInput = form.querySelector("[data-card-number]");
        var brandLabel = form.querySelector("[data-card-brand]");
        var expiryInput = form.querySelector("[data-card-expiry]");
        var cvcInput = form.querySelector("[data-card-cvc]");
        var nameInput = form.querySelector("#CardholderName");
        var payButton = form.querySelector("[data-pay-button]");
        var payLabel = form.querySelector("[data-pay-label]");

        // Same rules as SimulatedCardGateway.DetectBrand on the server.
        function detectBrand(digits) {
            if (/^4/.test(digits)) {
                return "Visa";
            }
            if (/^3[47]/.test(digits)) {
                return "American Express";
            }
            var two = parseInt(digits.slice(0, 2), 10);
            var four = parseInt(digits.slice(0, 4), 10);
            if ((two >= 51 && two <= 55) || (digits.length >= 4 && four >= 2221 && four <= 2720)) {
                return "Mastercard";
            }
            return "";
        }

        // Amex is grouped 4-6-5; everything else in fours.
        function formatNumber(digits, brand) {
            if (brand === "American Express") {
                return [digits.slice(0, 4), digits.slice(4, 10), digits.slice(10, 15)].filter(Boolean).join(" ");
            }
            return digits.replace(/(\d{4})(?=\d)/g, "$1 ");
        }

        function onNumberInput() {
            var digits = numberInput.value.replace(/\D/g, "");
            var brand = detectBrand(digits);
            digits = digits.slice(0, brand === "American Express" ? 15 : 19);
            numberInput.value = formatNumber(digits, brand);
            brandLabel.textContent = brand;
            cvcInput.maxLength = brand === "American Express" ? 4 : 3;
            cvcInput.placeholder = brand === "American Express" ? "1234" : "123";
        }

        function onExpiryInput(event) {
            var digits = expiryInput.value.replace(/\D/g, "").slice(0, 4);
            // A single digit 2-9 can only be a month like 02-09.
            if (digits.length === 1 && digits > "1") {
                digits = "0" + digits;
            }
            var deleting = event && event.inputType && event.inputType.indexOf("delete") === 0;
            if (digits.length > 2 || (digits.length === 2 && !deleting)) {
                expiryInput.value = digits.slice(0, 2) + "/" + digits.slice(2);
            } else {
                expiryInput.value = digits;
            }
        }

        numberInput.addEventListener("input", onNumberInput);
        expiryInput.addEventListener("input", onExpiryInput);
        cvcInput.addEventListener("input", function () {
            cvcInput.value = cvcInput.value.replace(/\D/g, "").slice(0, cvcInput.maxLength);
        });

        document.querySelectorAll("[data-test-card]").forEach(function (button) {
            button.addEventListener("click", function () {
                numberInput.value = button.getAttribute("data-test-card");
                onNumberInput();
                if (!expiryInput.value) {
                    var nextYear = (new Date().getFullYear() + 2) % 100;
                    expiryInput.value = "12/" + String(nextYear).padStart(2, "0");
                }
                if (!cvcInput.value) {
                    cvcInput.value = "123";
                }
                if (!nameInput.value) {
                    nameInput.focus();
                } else {
                    payButton.focus();
                }
            });
        });

        var originalLabel = payLabel.textContent;

        form.addEventListener("submit", function (event) {
            if (form.hasAttribute("data-submitting")) {
                event.preventDefault();
                return;
            }
            form.setAttribute("data-submitting", "");
            payButton.setAttribute("data-processing", "");
            payButton.setAttribute("aria-disabled", "true");
            payLabel.textContent = "Processing payment…";
            // Disabled on the next tick so this submit still goes through.
            window.setTimeout(function () {
                payButton.disabled = true;
            }, 0);
        });

        // Coming back with the browser's Back button can restore the page
        // mid-processing; reset it so the client can try again.
        window.addEventListener("pageshow", function (event) {
            if (event.persisted) {
                form.removeAttribute("data-submitting");
                payButton.removeAttribute("data-processing");
                payButton.removeAttribute("aria-disabled");
                payButton.disabled = false;
                payLabel.textContent = originalLabel;
            }
        });
    }

    document.querySelectorAll("[data-print]").forEach(function (button) {
        button.addEventListener("click", function () {
            window.print();
        });
    });
})();
