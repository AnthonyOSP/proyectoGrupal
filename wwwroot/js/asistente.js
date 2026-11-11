// Asistente virtual de Alerta Vecinal (chat flotante).
//  - Envía la pregunta a /Asistente/Preguntar y muestra la respuesta a medida que llega (streaming).
//  - Al final de la respuesta puede llegar un mensaje de control en JSON entre caracteres STX (\u0002)
//    con las fuentes de la ayuda y las tarjetas (incidencias o reporte preparado).
//  - La conversación se guarda en sessionStorage para seguir viéndola al cambiar de página
//    (el servidor guarda su propia copia del historial en la sesión).
(function () {
    "use strict";

    const raiz = document.getElementById("asistente");
    if (!raiz) return;

    const DELIMITADOR = "\u0002";
    const CLAVE_CONVERSACION = "asistente.conversacion";
    const CLAVE_ABIERTO = "asistente.abierto";
    const MAXIMO_GUARDADOS = 30;

    const lanzador = document.getElementById("asistenteLanzador");
    const panel = document.getElementById("asistentePanel");
    const lista = document.getElementById("asistenteMensajes");
    const sugerencias = document.getElementById("asistenteSugerencias");
    const form = document.getElementById("asistenteForm");
    const entrada = document.getElementById("asistenteEntrada");
    const botonEnviar = document.getElementById("asistenteEnviar");
    const botonCerrar = document.getElementById("asistenteCerrar");
    const botonNueva = document.getElementById("asistenteNueva");

    let conversacion = leer(CLAVE_CONVERSACION, []);
    let ocupado = false;

    // ---------- Almacenamiento (puede fallar en modo privado: el chat funciona igual) ----------

    function leer(clave, porDefecto) {
        try {
            const valor = sessionStorage.getItem(clave);
            return valor ? JSON.parse(valor) : porDefecto;
        } catch (e) {
            return porDefecto;
        }
    }

    function guardar(clave, valor) {
        try { sessionStorage.setItem(clave, JSON.stringify(valor)); } catch (e) { /* sin almacenamiento */ }
    }

    function guardarConversacion() {
        if (conversacion.length > MAXIMO_GUARDADOS) conversacion = conversacion.slice(-MAXIMO_GUARDADOS);
        guardar(CLAVE_CONVERSACION, conversacion);
    }

    // ---------- Abrir y cerrar ----------

    function abrir(enfocar) {
        panel.hidden = false;
        raiz.classList.add("is-abierto");
        lanzador.setAttribute("aria-expanded", "true");
        guardar(CLAVE_ABIERTO, true);
        bajar();
        if (enfocar) entrada.focus();
    }

    function cerrar() {
        panel.hidden = true;
        raiz.classList.remove("is-abierto");
        lanzador.setAttribute("aria-expanded", "false");
        guardar(CLAVE_ABIERTO, false);
        lanzador.focus();
    }

    lanzador.addEventListener("click", () => (panel.hidden ? abrir(true) : cerrar()));
    botonCerrar.addEventListener("click", cerrar);
    panel.addEventListener("keydown", e => { if (e.key === "Escape") cerrar(); });

    // ---------- Formato del texto ----------

    function escapar(texto) {
        return texto.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
    }

    // Markdown mínimo: párrafos, listas ("- " o "1. "), **negrita** y *cursiva*. Todo se escapa antes.
    function formatear(texto) {
        const negrita = linea => escapar(linea)
            .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
            .replace(/(^|[^*\w])\*([^*\s][^*]*?)\*(?![*\w])/g, "$1<em>$2</em>");
        const bloques = texto.trim().split(/\n{2,}/);
        return bloques.map(bloque => {
            const lineas = bloque.split("\n").filter(l => l.trim() !== "");
            if (lineas.length > 0 && lineas.every(l => /^\s*[-*•]\s+/.test(l))) {
                return "<ul>" + lineas.map(l => "<li>" + negrita(l.replace(/^\s*[-*•]\s+/, "")) + "</li>").join("") + "</ul>";
            }
            if (lineas.length > 0 && lineas.every(l => /^\s*\d+[.)]\s+/.test(l))) {
                return "<ol>" + lineas.map(l => "<li>" + negrita(l.replace(/^\s*\d+[.)]\s+/, "")) + "</li>").join("") + "</ol>";
            }
            return "<p>" + lineas.map(negrita).join("<br>") + "</p>";
        }).join("");
    }

    // Separa el texto visible del mensaje de control (que llega al final, entre dos STX).
    function separar(crudo) {
        const inicio = crudo.indexOf(DELIMITADOR);
        if (inicio < 0) return { texto: crudo, control: null, completo: true };

        const fin = crudo.indexOf(DELIMITADOR, inicio + 1);
        const texto = crudo.slice(0, inicio) + (fin >= 0 ? crudo.slice(fin + 1) : "");
        if (fin < 0) return { texto, control: null, completo: false };

        try {
            return { texto, control: JSON.parse(crudo.slice(inicio + 1, fin)), completo: true };
        } catch (e) {
            return { texto, control: null, completo: true };
        }
    }

    // ---------- Mensajes y tarjetas ----------

    function crear(etiqueta, clase, texto) {
        const el = document.createElement(etiqueta);
        if (clase) el.className = clase;
        if (texto != null) el.textContent = texto;
        return el;
    }

    function bajar() {
        lista.scrollTop = lista.scrollHeight;
    }

    function burbuja(rol, extraClase) {
        const div = crear("div", "asistente-msg asistente-msg--" + (rol === "user" ? "usuario" : "bot") + (extraClase ? " " + extraClase : ""));
        lista.appendChild(div);
        return div;
    }

    function pintarMensaje(mensaje) {
        const div = burbuja(mensaje.rol, mensaje.error ? "asistente-msg--error" : "");
        if (mensaje.rol === "user") {
            div.textContent = mensaje.texto;
        } else {
            div.innerHTML = formatear(mensaje.texto);
            if (mensaje.control) pintarControl(div, mensaje.control);
            if (mensaje.control && mensaje.control.id && !mensaje.error) div.appendChild(valoracion(mensaje));
        }
        return div;
    }

    // ---------- Valoración (👍/👎) ----------

    const PULGAR = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M7 10v12"/><path d="M15 5.88 14 10h5.83a2 2 0 0 1 1.92 2.56l-2.33 8A2 2 0 0 1 17.5 22H4a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2h2.76a2 2 0 0 0 1.79-1.11L12 2a3.13 3.13 0 0 1 3 3.88Z"/></svg>';

    function valoracion(mensaje) {
        const caja = crear("div", "asistente-valorar");
        const etiqueta = crear("span", "asistente-valorar__texto", mensaje.voto == null ? "¿Te sirvió?" : "¡Gracias por tu opinión!");
        caja.appendChild(etiqueta);

        [[true, "Sí, me sirvió"], [false, "No me sirvió"]].forEach(([util, nombre]) => {
            const boton = crear("button", "asistente-valorar__boton" + (util ? "" : " asistente-valorar__boton--no"));
            boton.type = "button";
            boton.innerHTML = PULGAR;
            boton.setAttribute("aria-label", nombre);
            boton.title = nombre;
            boton.setAttribute("aria-pressed", String(mensaje.voto === util));
            boton.addEventListener("click", async () => {
                if (mensaje.voto === util) return;
                try {
                    const r = await fetch(raiz.dataset.urlValorar, {
                        method: "POST",
                        headers: { "Content-Type": "application/json", "RequestVerificationToken": raiz.dataset.token },
                        body: JSON.stringify({ id: mensaje.control.id, util })
                    });
                    if (!r.ok) throw new Error();
                    mensaje.voto = util;
                    guardarConversacion();
                    caja.querySelectorAll("button").forEach(b => b.setAttribute("aria-pressed", String(b === boton)));
                    etiqueta.textContent = "¡Gracias por tu opinión!";
                } catch (e) {
                    etiqueta.textContent = "No se pudo guardar tu opinión.";
                }
            });
            caja.appendChild(boton);
        });
        return caja;
    }

    function pintarControl(div, control) {
        if (control.incidencias && control.incidencias.length) {
            const contenedor = crear("div", "asistente-tarjetas");
            control.incidencias.forEach(i => contenedor.appendChild(tarjetaIncidencia(i)));
            div.appendChild(contenedor);
        }
        if (control.borrador) div.appendChild(tarjetaBorrador(control.borrador));
        if (control.contacto) div.appendChild(tarjetaContacto(control.contacto));
        if (control.fuentes && control.fuentes.length) {
            div.appendChild(crear("p", "asistente-fuentes", "Según la ayuda: " + control.fuentes.join(" · ")));
        }
    }

    function tarjetaIncidencia(i) {
        const a = crear("a", "asistente-card");
        a.href = i.url;
        const cabecera = crear("span", "asistente-card__cabecera");
        cabecera.appendChild(crear("span", "asistente-card__numero", "#" + i.id));
        cabecera.appendChild(crear("span", "estado " + (i.estadoClase || ""), i.estado));
        a.appendChild(cabecera);
        a.appendChild(crear("span", "asistente-card__titulo", i.titulo));
        a.appendChild(crear("span", "asistente-card__dato", i.categoria + " · " + i.fecha));
        a.appendChild(crear("span", "asistente-card__dato", i.ubicacion));
        return a;
    }

    function tarjetaBorrador(b) {
        const card = crear("div", "asistente-card asistente-card--borrador");
        card.appendChild(crear("span", "asistente-card__etiqueta", "Reporte preparado"));
        card.appendChild(crear("span", "asistente-card__titulo", b.titulo));

        const datos = crear("dl", "asistente-card__datos");
        [["Categoría", b.categoria || "La eliges en el formulario"], ["Ubicación", b.ubicacion], ["Descripción", b.descripcion]]
            .forEach(([nombre, valor]) => {
                datos.appendChild(crear("dt", null, nombre));
                datos.appendChild(crear("dd", null, valor));
            });
        card.appendChild(datos);

        const boton = crear("a", "btn btn-av-primary asistente-card__boton", "Abrir formulario");
        boton.href = b.url;
        card.appendChild(boton);

        card.appendChild(crear("span", "asistente-card__nota", b.requiereSesion
            ? "Te pediremos iniciar sesión; tus datos se conservan. Revisa todo y pulsa «Enviar reporte»."
            : "Revisa los datos, agrega una foto si quieres y pulsa «Enviar reporte»."));
        return card;
    }

    function tarjetaContacto(c) {
        const card = crear("div", "asistente-card asistente-card--borrador");
        card.appendChild(crear("span", "asistente-card__etiqueta", "Mensaje para el equipo"));
        const datos = crear("dl", "asistente-card__datos");
        datos.appendChild(crear("dt", null, "Mensaje"));
        datos.appendChild(crear("dd", null, c.mensaje));
        card.appendChild(datos);
        const boton = crear("a", "btn btn-av-primary asistente-card__boton", "Abrir Contacto");
        boton.href = c.url;
        card.appendChild(boton);
        card.appendChild(crear("span", "asistente-card__nota", "Revisa el mensaje, completa tu nombre y correo, y pulsa «Enviar mensaje»."));
        return card;
    }

    function pintarTodo() {
        lista.querySelectorAll(".asistente-msg:not(.asistente-msg--bienvenida)").forEach(el => el.remove());
        sugerencias.hidden = conversacion.length > 0;
        conversacion.forEach(pintarMensaje);
        bajar();
    }

    // ---------- Enviar una pregunta ----------

    async function preguntar(texto) {
        texto = texto.trim();
        if (!texto || ocupado) return;

        ocupado = true;
        botonEnviar.disabled = true;
        sugerencias.hidden = true;

        const pregunta = { rol: "user", texto };
        conversacion.push(pregunta);
        guardarConversacion();
        pintarMensaje(pregunta);

        const div = burbuja("assistant", "asistente-msg--escribiendo");
        div.innerHTML = '<span class="asistente-puntos" aria-label="El asistente está escribiendo"><i></i><i></i><i></i></span>';
        bajar();

        let crudo = "";
        let error = false;
        try {
            const respuesta = await fetch(raiz.dataset.urlPreguntar, {
                method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": raiz.dataset.token },
                body: JSON.stringify({ mensaje: texto, pagina: location.pathname })
            });

            error = !respuesta.ok;
            if (!respuesta.body) {
                crudo = await respuesta.text();
            } else {
                const lector = respuesta.body.getReader();
                const decodificador = new TextDecoder();
                for (;;) {
                    const { done, value } = await lector.read();
                    if (done) break;
                    crudo += decodificador.decode(value, { stream: true });
                    const parcial = separar(crudo);
                    if (parcial.texto.trim()) {
                        div.classList.remove("asistente-msg--escribiendo");
                        div.innerHTML = formatear(parcial.texto);
                        bajar();
                    }
                }
                crudo += decodificador.decode();
            }

            if (!crudo.trim()) {
                error = true;
                crudo = respuesta.status === 400
                    ? "La página se actualizó. Recárgala e inténtalo de nuevo."
                    : "No recibimos respuesta. Inténtalo de nuevo.";
            }
        } catch (e) {
            error = true;
            crudo = "No se pudo conectar con el asistente. Revisa tu conexión e inténtalo de nuevo.";
        }

        const final = separar(crudo);
        const mensaje = { rol: "assistant", texto: final.texto.trim(), control: final.control, error };
        div.remove();
        const nuevo = pintarMensaje(mensaje);
        conversacion.push(mensaje);
        guardarConversacion();
        // Si la respuesta (con sus tarjetas) no cabe, se muestra desde su inicio y no desde el final.
        if (nuevo.offsetHeight > lista.clientHeight - 24) lista.scrollTop = nuevo.offsetTop - 12;
        else bajar();

        ocupado = false;
        botonEnviar.disabled = false;
        entrada.focus();
    }

    form.addEventListener("submit", e => {
        e.preventDefault();
        const texto = entrada.value;
        entrada.value = "";
        ajustarAltura();
        preguntar(texto);
    });

    // Enter envía; Shift+Enter hace un salto de línea.
    entrada.addEventListener("keydown", e => {
        if (e.key === "Enter" && !e.shiftKey && !e.isComposing) {
            e.preventDefault();
            form.requestSubmit();
        }
    });

    function ajustarAltura() {
        entrada.style.height = "auto";
        entrada.style.height = Math.min(entrada.scrollHeight, 140) + "px";
    }
    entrada.addEventListener("input", ajustarAltura);

    sugerencias.addEventListener("click", e => {
        const chip = e.target.closest("[data-sugerencia]");
        if (chip) preguntar(chip.dataset.sugerencia);
    });

    botonNueva.addEventListener("click", () => {
        if (ocupado) return;
        conversacion = [];
        guardarConversacion();
        pintarTodo();
        fetch(raiz.dataset.urlLimpiar, { method: "POST", headers: { "RequestVerificationToken": raiz.dataset.token } }).catch(() => { });
        entrada.focus();
    });

    // ---------- Inicio ----------

    pintarTodo();
    if (leer(CLAVE_ABIERTO, false)) abrir(false);
})();
