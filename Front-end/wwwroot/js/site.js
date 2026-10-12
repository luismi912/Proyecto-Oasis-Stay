// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
    const formatCurrency = value => "$" + Math.round(value).toLocaleString("es-CO") + " ";
    const formatDate = value => {
        if (!value) return "—";
        return new Intl.DateTimeFormat("es-CO", { day: "2-digit", month: "short", year: "numeric" }).format(new Date(value + "T12:00:00"));
    };

    document.querySelectorAll("[data-modal-open]").forEach(trigger => {
        trigger.addEventListener("click", event => {
            event.preventDefault();
            const modal = document.getElementById(trigger.dataset.modalOpen);
            if (modal) {
                modal.classList.add("is-open");
                modal.setAttribute("aria-hidden", "false");
                document.body.classList.add("modal-open");
                modal.querySelector("input, select")?.focus();
            }
        });
    });

    const modalFromHash = {
        "#nueva-reserva": "reservation-modal",
        "#crear": "room-modal"
    };
    const modalId = modalFromHash[window.location.hash];
    if (modalId) {
        const trigger = document.querySelector(`[data-modal-open="${modalId}"]`);
        if (trigger) trigger.click();
    }

    const closeModal = modal => {
        modal.classList.remove("is-open");
        modal.setAttribute("aria-hidden", "true");
        document.body.classList.remove("modal-open");
    };

    document.querySelectorAll("[data-modal-close]").forEach(button => {
        button.addEventListener("click", () => closeModal(button.closest(".modal-backdrop")));
    });

    document.querySelectorAll(".modal-backdrop").forEach(backdrop => {
        backdrop.addEventListener("click", event => {
            if (event.target === backdrop) closeModal(backdrop);
        });
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            const openModal = document.querySelector(".modal-backdrop.is-open");
            if (openModal) closeModal(openModal);
        }
    });

    const reservationModal = document.getElementById("reservation-modal");
    if (reservationModal) {
        const inputs = reservationModal.querySelectorAll("input[type=date]");
        const guests = reservationModal.querySelector("select[name='Reservation.Guests']");
        const updateSummary = () => {
            const room = reservationModal.querySelector("input[name='Reservation.RoomNumber']:checked");
            const checkIn = inputs[0]?.value;
            const checkOut = inputs[1]?.value;
            const nights = checkIn && checkOut ? Math.max(1, Math.ceil((new Date(checkOut) - new Date(checkIn)) / 86400000)) : 1;
            const price = Number(room?.dataset.roomPrice || 185000);
            const subtotal = nights * price;
            const tax = subtotal * .19;
            reservationModal.querySelector("#summary-room-number").textContent = room?.value || "102";
            reservationModal.querySelector("#summary-room-type").textContent = room?.dataset.roomType || "Doble Clásica";
            reservationModal.querySelector("#summary-nights").textContent = nights;
            reservationModal.querySelector("#summary-nights-label").textContent = nights;
            reservationModal.querySelector("#summary-guests").textContent = guests?.value || "2";
            reservationModal.querySelector("#summary-checkin").textContent = formatDate(checkIn);
            reservationModal.querySelector("#summary-checkout").textContent = formatDate(checkOut);
            reservationModal.querySelector("#summary-subtotal").textContent = formatCurrency(subtotal);
            reservationModal.querySelector("#summary-tax").textContent = formatCurrency(tax);
            reservationModal.querySelector("#summary-total").firstChild.textContent = formatCurrency(subtotal + tax);
        };
        reservationModal.querySelectorAll("input, select").forEach(input => input.addEventListener("change", updateSummary));
        updateSummary();
    }

    const roomNumber = document.getElementById("room-number");
    const roomType = document.getElementById("room-type");
    if (roomNumber && roomType) {
        const updatePreview = () => {
            document.getElementById("preview-number").textContent = roomNumber.value || "401";
            document.getElementById("preview-type").textContent = roomType.value;
        };
        roomNumber.addEventListener("input", updatePreview);
        roomType.addEventListener("change", updatePreview);
    }
})();
