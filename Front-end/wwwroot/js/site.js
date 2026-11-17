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

        // Reabre el modal con lo digitado cuando la API rechazó la reserva [JIRA US-03 ESC3]
        if (reservationModal.dataset.openOnLoad === "true") {
            reservationModal.classList.add("is-open");
            reservationModal.setAttribute("aria-hidden", "false");
            document.body.classList.add("modal-open");
        }

        // Autocompletado por documento: solo presentación, la búsqueda la hace la API [JIRA US-03 ESC2]
        const docInput = reservationModal.querySelector("input[name='Reservation.Document']");
        const docType = reservationModal.querySelector("select[name='Reservation.DocumentType']");
        const autocompletar = async () => {
            const documento = docInput?.value.trim();
            if (!documento) return;
            try {
                const r = await fetch(`?handler=Huesped&tipoDocumento=${encodeURIComponent(docType.value)}&documento=${encodeURIComponent(documento)}`);
                if (!r.ok) return;
                const h = await r.json();
                const poner = (nombre, valor) => { const el = reservationModal.querySelector(`[name='Reservation.${nombre}']`); if (el && valor) el.value = valor; };
                poner("GuestName", h.nombre); poner("Phone", h.telefono); poner("Email", h.correo);
            } catch { /* sin conexión: el recepcionista digita los datos */ }
        };
        // Paso 3: habitaciones reales desde la API de disponibilidad (solo presentación) [JIRA US-01]
        const contenedor = reservationModal.querySelector("#room-options");
        const alerta = reservationModal.querySelector("#availability-alert");
        const escapar = t => String(t).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
        let consulta = 0;
        const cargarHabitaciones = async () => {
            const turno = ++consulta;
            const ini = inputs[0]?.value, fin = inputs[1]?.value;
            if (!ini || !fin) return;
            const previa = contenedor.querySelector("input:checked")?.value ?? contenedor.dataset.selected;
            alerta.hidden = true;
            try {
                const tipo = reservationModal.querySelector("#availability-type")?.value ?? "";
                const r = await fetch(`?handler=Disponibilidad&fechaInicio=${ini}&fechaFin=${fin}&huespedes=${guests.value}&tipo=${encodeURIComponent(tipo)}`);
                const j = await r.json();
                if (turno !== consulta) return; // respuesta vieja
                if (!r.ok) {
                    contenedor.innerHTML = "";
                    alerta.textContent = j.detail || "No se pudo consultar la disponibilidad.";
                    alerta.hidden = false;
                    updateSummary();
                    return;
                }
                if (j.habitaciones.length === 0) {
                    const alt = j.alternativas.length ? " Alternativas con cupo: " + j.alternativas.map(a => `${a.nombre} ($${Math.round(a.precioBase).toLocaleString("es-CO")}/noche)`).join(", ") + "." : "";
                    alerta.textContent = (j.mensaje || "") + alt;
                    alerta.hidden = false;
                }
                const hay = j.habitaciones.some(h => String(h.numero) === String(previa));
                contenedor.innerHTML = j.habitaciones.map((h, i) => `<label class="room-option"><input name="Reservation.RoomNumber" type="radio" value="${h.numero}" ${(hay ? String(h.numero) === String(previa) : i === 0) ? "checked" : ""} data-room-price="${h.precioNoche}" data-room-type="${escapar(h.tipo)}" /><span><strong>Hab. ${h.numero}</strong><small>${escapar(h.tipo)}</small></span><b>$${Math.round(h.precioNoche).toLocaleString("es-CO")}<small>/ noche</small></b></label>`).join("");
                contenedor.querySelectorAll("input").forEach(i => i.addEventListener("change", updateSummary));
                updateSummary();
            } catch { contenedor.innerHTML = ""; }
        };
        inputs.forEach(i => i.addEventListener("change", cargarHabitaciones));
        guests?.addEventListener("change", cargarHabitaciones);
        reservationModal.querySelector("#availability-type")?.addEventListener("change", cargarHabitaciones);
        cargarHabitaciones();

        docInput?.addEventListener("change", autocompletar);
        docType?.addEventListener("change", autocompletar);
    }

    // Reasignar habitación [JIRA US-04 ESC3][D-21]: solo presentación; la API decide capacidad, estado y Upgrade
    const reasignarModal = document.getElementById("reasignar-modal");
    if (reasignarModal) {
        const lista = reasignarModal.querySelector("#reasignar-lista");
        const aviso = reasignarModal.querySelector("#reasignar-aviso");
        const errorBox = reasignarModal.querySelector("#reasignar-error");
        const form = reasignarModal.querySelector("#reasignar-form");
        const confirmar = reasignarModal.querySelector("#reasignar-confirmar");
        const enviar = reasignarModal.querySelector("#reasignar-enviar");
        const esc = t => String(t).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
        const reiniciar = () => { confirmar.value = "false"; aviso.hidden = true; errorBox.hidden = true; enviar.textContent = "✓ Reasignar"; };

        document.querySelectorAll("[data-reasignar]").forEach(boton => boton.addEventListener("click", async () => {
            const d = boton.dataset;
            reiniciar();
            form.querySelector("#reasignar-id").value = d.id;
            reasignarModal.querySelector("#reasignar-reserva").textContent = "#" + d.id;
            reasignarModal.querySelector("#reasignar-actual").textContent = "Hab. " + d.habitacion;
            lista.innerHTML = '<p class="availability-loading">Consultando disponibilidad…</p>';
            try {
                const r = await fetch(`?handler=Disponibilidad&fechaInicio=${d.inicio}&fechaFin=${d.fin}&huespedes=${d.huespedes}`);
                const j = await r.json();
                if (!r.ok) { lista.innerHTML = ""; errorBox.textContent = j.detail || "No se pudo consultar la disponibilidad."; errorBox.hidden = false; return; }
                const otras = j.habitaciones.filter(h => String(h.numero) !== String(d.habitacion));
                if (otras.length === 0) { aviso.textContent = j.mensaje || "No hay habitaciones disponibles."; aviso.hidden = false; }
                lista.innerHTML = otras.map(h => `<label class="room-option"><input name="numeroHabitacionNueva" type="radio" value="${h.numero}" required /><span><strong>Hab. ${h.numero}</strong><small>${esc(h.tipo)}</small></span><b>$${Math.round(h.precioNoche).toLocaleString("es-CO")}<small>/ noche</small></b></label>`).join("");
                lista.querySelectorAll("input").forEach(i => i.addEventListener("change", reiniciar));
            } catch { lista.innerHTML = ""; errorBox.textContent = "No se pudo consultar la disponibilidad."; errorBox.hidden = false; }
        }));

        form.addEventListener("submit", async event => {
            event.preventDefault();
            if (!form.querySelector("input[name='numeroHabitacionNueva']:checked")) { errorBox.textContent = "Seleccione la habitación nueva."; errorBox.hidden = false; return; }
            errorBox.hidden = true;
            try {
                const r = await fetch(form.action, { method: "POST", body: new FormData(form) });
                const j = await r.json();
                if (j.ok) { window.location.reload(); return; }
                if (j.upgrade) { // aviso de Upgrade con confirmación [D-32]
                    aviso.textContent = j.detail; aviso.hidden = false;
                    confirmar.value = "true"; enviar.textContent = "✓ Confirmar Upgrade";
                    return;
                }
                errorBox.textContent = j.detail; errorBox.hidden = false;
            } catch { errorBox.textContent = "No se pudo reasignar la habitación."; errorBox.hidden = false; }
        });
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
